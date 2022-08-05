// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestPlatform.CommandLine.Internal;
using Microsoft.VisualStudio.TestPlatform.CommandLine.Processors;
using Microsoft.VisualStudio.TestPlatform.CommandLine.TestPlatformHelpers;
using Microsoft.VisualStudio.TestPlatform.CommandLine2;
using Microsoft.VisualStudio.TestPlatform.Common;
using Microsoft.VisualStudio.TestPlatform.Common.Utilities;
using Microsoft.VisualStudio.TestPlatform.CoreUtilities.Tracing;
using Microsoft.VisualStudio.TestPlatform.CoreUtilities.Tracing.Interfaces;
using Microsoft.VisualStudio.TestPlatform.Execution;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.PlatformAbstractions;
using Microsoft.VisualStudio.TestPlatform.PlatformAbstractions.Interfaces;
using Microsoft.VisualStudio.TestPlatform.Utilities;

using CommandLineResources = Microsoft.VisualStudio.TestPlatform.CommandLine.Resources.Resources;

// General Flow:
// Create a command processor for each argument.
//   If there is no matching command processor for an argument, output error, display help and exit.
//   If throws during creation, output error and exit.
// If the help command processor has been requested, execute the help processor and exit.
// Order the command processors by priority.
// Allow command processors to validate against other command processors which are present.
//   If throws during validation, output error and exit.
// Process each command processor.
//   If throws during validation, output error and exit.
//   If the default (RunTests) command processor has no test containers output an error and exit
//   If the default (RunTests) command processor has no tests to run output an error and exit

// Commands metadata:
//  *Command line argument.
//   Priority.
//   Help output.
//   Required
//   Single or multiple

namespace Microsoft.VisualStudio.TestPlatform.CommandLine;

/// <summary>
/// Performs the execution based on the arguments provided.
/// </summary>
internal class Executor
{
    private const string NonARM64RunnerName = "vstest.console.exe";
    private readonly IOutput _output;
    private readonly ITestPlatformEventSource _testPlatformEventSource;
    private readonly IProcessHelper _processHelper;
    private readonly IEnvironment _environment;
    private bool _showHelp;

    internal Executor(IOutput output, ITestPlatformEventSource testPlatformEventSource, IProcessHelper processHelper, IEnvironment environment)
    {
        DebuggerBreakpoint.AttachVisualStudioDebugger("VSTEST_RUNNER_DEBUG_ATTACHVS");
        DebuggerBreakpoint.WaitForDebugger("VSTEST_RUNNER_DEBUG");

        // TODO: Get rid of this by making vstest.console code properly async.
        // The current implementation of vstest.console is blocking many threads that just wait
        // for completion in non-async way. Because threadpool is setting the limit based on processor count,
        // we exhaust the threadpool threads quickly when we set maxCpuCount to use as many workers as we have threads.
        //
        // This setting allow the threadpool to start start more threads than it normally would without any delay.
        // This won't pre-start the threads, it just pushes the limit of how many are allowed to start without waiting,
        // and in effect makes callbacks processed earlier, because we don't have to wait that much to receive the callback.
        // The correct fix would be to re-visit all code that offloads work to threadpool and avoid blocking any thread,
        // and also use async await when we need to await a completion of an action. But that is a far away goal, so this
        // is a "temporary" measure to remove the threadpool contention.
        //
        // The increase to 5* (1* is the standard + 4*) the standard limit is arbitrary. I saw that making it 2* did not help
        // and there are usually 2-3 threads blocked by waiting for other actions, so 5 seemed like a good limit.
        var additionalThreadsCount = Environment.ProcessorCount * 4;
        ThreadPool.GetMinThreads(out var workerThreads, out var completionPortThreads);
        ThreadPool.SetMinThreads(workerThreads + additionalThreadsCount, completionPortThreads + additionalThreadsCount);

        _output = output;
        _testPlatformEventSource = testPlatformEventSource;
        _processHelper = processHelper;
        _environment = environment;

        _showHelp = true;
    }

    /// <summary>
    /// Performs the execution based on the arguments provided.
    /// </summary>
    /// <param name="args">
    /// Arguments provided to perform execution with.
    /// </param>
    /// <returns>
    /// Exit Codes - Zero (for successful command execution), One (for bad command)
    /// </returns>
    internal int Execute(params string[]? args)
    {
        _testPlatformEventSource.VsTestConsoleStart();

        IReadOnlyList<ArgumentProcessor> argumentProcessors = ArgumentProcessorFactory.DefaultArgumentProcessors;

        var parser = new Parser().Parse(args, argumentProcessors);

        // todo: exit quickly for parse error.
        //// Quick exit for syntax error
        //if (exitCode == 1 && !argumentProcessors.Any(processor => processor is HelpArgumentProcessor))
        //{
        //    _testPlatformEventSource.VsTestConsoleStop();
        //    return exitCode;
        //}


        // TODO: leave this to the parser, we don't enable logging before parsing version anyway, so doing this manually is unnecessary.
        var isDiag = args != null && args.Any(arg => arg.RemoveArgumentPrefix().StartsWith("diag", StringComparison.OrdinalIgnoreCase));

        // If User specifies --nologo via dotnet, do not print splat screen
        if (args != null && args.Length != 0 && args.Contains("--nologo"))
        {
            // Sanitizing this list, as I don't think we should write Argument processor for this.
            args = args.Where(val => val != "--nologo").ToArray();
        }
        else
        {
            // If we're postprocessing we don't need to show the splash
            if (!ArtifactProcessingPostProcessModeProcessor.ContainsPostProcessCommand(args))
            {
                PrintSplashScreen(isDiag);
            }
        }

        int exitCode = 0;

        // If we have no arguments, set exit code to 1, add a message, and include the help processor in the args.
        if (args == null || args.Length == 0 || args.Any(StringUtils.IsNullOrWhiteSpace))
        {
            _output.Error(true, CommandLineResources.NoArgumentsProvided);
            args = new string[] { HelpArgumentProcessor.CommandName };
            exitCode = 1;
        }

        if (!isDiag)
        {
            // This takes a path to log directory and log.txt file. Same as the --diag parameter, e.g. VSTEST_DIAG="logs\log.txt"
            var diag = Environment.GetEnvironmentVariable("VSTEST_DIAG");
            // This takes Verbose, Info (not Information), Warning, and Error.
            var diagVerbosity = Environment.GetEnvironmentVariable("VSTEST_DIAG_VERBOSITY");
            if (!StringUtils.IsNullOrWhiteSpace(diag))
            {
                var verbosity = TraceLevel.Verbose;
                if (diagVerbosity != null)
                {
                    if (Enum.TryParse<TraceLevel>(diagVerbosity, ignoreCase: true, out var parsedVerbosity))
                    {
                        verbosity = parsedVerbosity;
                    }
                }

                args = args.Concat(new[] { $"--diag:{diag};TraceLevel={verbosity}" }).ToArray();
            }
        }




        //// Flatten arguments and process response files.
        // exitCode |= FlattenArguments(args, out var flattenedArguments);
        string[] flattenedArguments = new string[0];

        // Get the argument processors for the arguments, and initialize them.
        exitCode |= GetArgumentProcessors(flattenedArguments, out List<ArgumentProcessor> argumentProcessors2);

        // TODO: some of the argument processors are adding parameters for the lattter argument processors to pick them up,
        // this sucks, and we should not do that, or we should guard against it by adding those parameters to a special group
        // and verifying it is empty. This is probably why there is a initialize and execute phase.
        //
        //// Verify that the arguments are valid.
        //exitCode |= IdentifyDuplicateArguments(argumentProcessors);

        // Quick exit for syntax error
        if (exitCode == 1 && !argumentProcessors.Any(processor => processor is HelpArgumentProcessor))
        {
            _testPlatformEventSource.VsTestConsoleStop();
            return exitCode;
        }

        // Execute all argument processors
        foreach (var processor in argumentProcessors)
        {
            if (!ExecuteArgumentProcessor(processor, ref exitCode))
            {
                break;
            }
        }

        // Use the test run result aggregator to update the exit code.
        exitCode |= (TestRunResultAggregator.Instance.Outcome == TestOutcome.Passed) ? 0 : 1;

        EqtTrace.Verbose("Executor.Execute: Exiting with exit code of {0}", exitCode);

        _testPlatformEventSource.VsTestConsoleStop();

        _testPlatformEventSource.MetricsDisposeStart();

        // Disposing Metrics Publisher when VsTestConsole ends
        TestRequestManager.Instance.Dispose();

        _testPlatformEventSource.MetricsDisposeStop();
        return exitCode;
    }

    /// <summary>
    /// Get the list of argument processors for the arguments.
    /// </summary>
    /// <param name="args">Arguments provided to perform execution with.</param>
    /// <param name="processors">List of argument processors for the arguments.</param>
    /// <returns>0 if all of the processors were created successfully and 1 otherwise.</returns>
    private int GetArgumentProcessors(string[] args, out List<ArgumentProcessor> processors)
    {
        processors = new List<ArgumentProcessor>();
        int result = 0;
        var processorFactory = ArgumentProcessorFactory.Create();
        for (var index = 0; index < args.Length; index++)
        {
            //var arg = args[index];
            //// If argument is '--', following arguments are key=value pairs for run settings.
            //if (arg.Equals("--"))
            //{
            //    var cliRunSettingsProcessor = processorFactory.CreateArgumentProcessor(arg, args.Skip(index + 1).ToArray());
            //    processors.Add(cliRunSettingsProcessor!);
            //    break;
            //}

            string arg = null;
            var processor = processorFactory.CreateArgumentProcessor(arg);

            if (processor != null)
            {
                processors.Add(processor);
            }
            else
            {
                // No known processor was found, report an error and continue
                _output.Error(false, string.Format(CultureInfo.CurrentCulture, CommandLineResources.NoArgumentProcessorFound, arg));

                // Add the help processor
                if (result == 0)
                {
                    result = 1;
                    processors.Add(processorFactory.CreateArgumentProcessor(HelpArgumentProcessor.CommandName)!);
                }
            }
        }

        // Add the internal argument processors that should always be executed.
        // Examples: processors to enable loggers that are statically configured, and to start logging,
        // should always be executed.
        var processorsToAlwaysExecute = processorFactory.GetArgumentProcessorsToAlwaysExecute();
        foreach (var processor in processorsToAlwaysExecute)
        {
            // TODO: this just makes sure we don't add duplicates. But we won't need it later when we simply go over every
            // processort and try to bind parameter to it, or run it with all parameters
            //if (processors.Any(i => i.Metadata.Value.CommandName == processor.Metadata.Value.CommandName))
            //{
            //    continue;
            //}

            // We need to initialize the argument executor if it's set to always execute. This ensures it will be initialized with other executors.
            processors.Add(ArgumentProcessorFactory.WrapLazyProcessorToInitializeOnInstantiation(processor));
        }

        // Initialize Runsettings with defaults
        RunSettingsManager.Instance.AddDefaultRunSettings();

        // Ensure we have an action argument.
        EnsureActionArgumentIsPresent(processors, processorFactory);

        // Instantiate and initialize the processors in priority order.
        processors.Sort((p1, p2) => Comparer<ArgumentProcessorPriority>.Default.Compare(p1.Priority, p2.Priority));
        foreach (var processor in processors)
        {
            IArgumentExecutor? executorInstance;
            try
            {
                // Ensure the instance is created.  Note that the Lazy not only instantiates
                // the argument processor, but also initializes it.
                // TODO: this is where we need to initialize the executor and do stuff.
                executorInstance = null;
            }
            catch (Exception ex)
            {
                if (ex is CommandLineException or TestPlatformException or SettingsException)
                {
                    _output.Error(false, ex.Message);
                    result = 1;
                    _showHelp = false;
                }
                else if (ex is TestSourceException)
                {
                    _output.Error(false, ex.Message);
                    result = 1;
                    _showHelp = false;
                    break;
                }
                else
                {
                    // Let it throw - User must see crash and report it with stack trace!
                    // No need for recoverability as user will start a new vstest.console anyway
                    throw;
                }
            }
        }

        // TODO: nope, we will just do this by ParseError command that will be the first.
        // If some argument was invalid, add help argument processor in beginning(i.e. at highest priority)
        //if (result == 1 && _showHelp && processors.First() != HelpArgumentProcessor.CommandName)
        //{
        //    processors.Insert(0, processorFactory.CreateArgumentProcessor(HelpArgumentProcessor.CommandName)!);
        //}
        return result;
    }

    /// <summary>
    /// Ensures that an action argument is present and if one is not, then the default action argument is added.
    /// </summary>
    /// <param name="argumentProcessors">The arguments that are being processed.</param>
    /// <param name="processorFactory">A factory for creating argument processors.</param>
    private static void EnsureActionArgumentIsPresent(List<ArgumentProcessor> argumentProcessors, ArgumentProcessorFactory processorFactory)
    {
        ValidateArg.NotNull(argumentProcessors, nameof(argumentProcessors));
        ValidateArg.NotNull(processorFactory, nameof(processorFactory));

        // Determine if any of the argument processors are actions.
        var isActionIncluded = argumentProcessors.Any((processor) => processor.IsCommand);

        // If no action arguments have been provided, then add the default action argument.
        if (!isActionIncluded)
        {
            argumentProcessors.Add(processorFactory.CreateDefaultActionArgumentProcessor());
        }
    }

    /// <summary>
    /// Executes the argument processor
    /// </summary>
    /// <param name="processor">Argument processor to execute.</param>
    /// <param name="exitCode">Exit status of Argument processor</param>
    /// <returns> true if continue execution, false otherwise.</returns>
    private bool ExecuteArgumentProcessor(ArgumentProcessor processor, ref int exitCode)
    {
        var continueExecution = true;
        ArgumentProcessorResult result;
        try
        {
            // TODO: Only executor that could return null is ResponseFileArgumentProcessor, maybe it could be updated
            // to follow a pattern similar to other processors and avoid returning null.
            // TODO: invoke the actual executor.
            IArgumentExecutor executor = null;
            result = executor.Execute();
        }
        catch (Exception ex)
        {
            if (ex is CommandLineException or TestPlatformException or SettingsException or InvalidOperationException)
            {
                EqtTrace.Error("ExecuteArgumentProcessor: failed to execute argument process: {0}", ex);
                _output.Error(false, ex.Message);
                result = ArgumentProcessorResult.Fail;

                // Send inner exception only when its message is different to avoid duplicate.
                if (ex is TestPlatformException &&
                    ex.InnerException != null &&
                    !string.Equals(ex.InnerException.Message, ex.Message, StringComparison.CurrentCultureIgnoreCase))
                {
                    _output.Error(false, ex.InnerException.Message);
                }
            }
            else
            {
                // Let it throw - User must see crash and report it with stack trace!
                // No need for recoverability as user will start a new vstest.console anyway
                throw;
            }
        }

        TPDebug.Assert(
            result is >= ArgumentProcessorResult.Success and <= ArgumentProcessorResult.Abort,
            "Invalid argument processor result.");

        if (result == ArgumentProcessorResult.Fail)
        {
            exitCode = 1;
        }

        if (result == ArgumentProcessorResult.Abort)
        {
            continueExecution = false;
        }
        return continueExecution;
    }

    /// <summary>
    /// Displays the Company and Copyright splash title info immediately after launch
    /// </summary>
    private void PrintSplashScreen(bool isDiag)
    {
        string? assemblyVersion = Product.Version;
        if (!isDiag)
        {
            var end = Product.Version?.IndexOf("-release");

            if (end >= 0)
            {
                assemblyVersion = Product.Version?.Substring(0, end.Value);
            }
        }

        string assemblyVersionAndArchitecture = $"{assemblyVersion} ({_processHelper.GetCurrentProcessArchitecture().ToString().ToLowerInvariant()})";
        string commandLineBanner = string.Format(CultureInfo.CurrentCulture, CommandLineResources.MicrosoftCommandLineTitle, assemblyVersionAndArchitecture);
        _output.WriteLine(commandLineBanner, OutputLevel.Information);
        _output.WriteLine(CommandLineResources.CopyrightCommandLineTitle, OutputLevel.Information);
        PrintWarningIfRunningEmulatedOnArm64();
        _output.WriteLine(string.Empty, OutputLevel.Information);
    }

    /// <summary>
    /// Display a warning if we're running the runner on ARM64 but with a different current process architecture.
    /// </summary>
    private void PrintWarningIfRunningEmulatedOnArm64()
    {
        var currentProcessArchitecture = _processHelper.GetCurrentProcessArchitecture();
        if (Path.GetFileName(_processHelper.GetCurrentProcessFileName()) == NonARM64RunnerName &&
            _environment.Architecture == PlatformArchitecture.ARM64 &&
            currentProcessArchitecture != PlatformArchitecture.ARM64)
        {
            _output.Warning(false, CommandLineResources.WarningEmulatedOnArm64, currentProcessArchitecture.ToString().ToLowerInvariant());
        }
    }
}
