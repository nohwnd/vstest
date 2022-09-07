// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using Microsoft.VisualStudio.TestPlatform.CommandLine.Processors;
using Microsoft.VisualStudio.TestPlatform.CommandLine.Publisher;
using Microsoft.VisualStudio.TestPlatform.CommandLine2;
using Microsoft.VisualStudio.TestPlatform.Common;
using Microsoft.VisualStudio.TestPlatform.Common.Interfaces;
using Microsoft.VisualStudio.TestPlatform.Common.Utilities;
using Microsoft.VisualStudio.TestPlatform.CoreUtilities.Tracing;
using Microsoft.VisualStudio.TestPlatform.CoreUtilities.Tracing.Interfaces;
using Microsoft.VisualStudio.TestPlatform.Execution;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.PlatformAbstractions;
using Microsoft.VisualStudio.TestPlatform.PlatformAbstractions.Interfaces;
using Microsoft.VisualStudio.TestPlatform.Utilities;
using Microsoft.VisualStudio.TestPlatform.Utilities.Helpers;
using Microsoft.VisualStudio.TestPlatform.Utilities.Helpers.Interfaces;

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
    private readonly ITestPlatformEventSource _testPlatformEventSource;
    private readonly IProcessHelper _processHelper;
    private readonly IEnvironment _environment;
    private readonly IFeatureFlag? _featureFlag;
    private readonly IRunSettingsProvider _runsettingsManager;

    public SharedDependencyDictionary SharedDependencies { get; } = new();

    public Executor(IOutput output) : this(output, TestPlatformEventSource.Instance, new ProcessHelper(), new PlatformEnvironment())
    {
    }

    internal Executor(IOutput output, ITestPlatformEventSource testPlatformEventSource, IProcessHelper processHelper, IEnvironment environment)
    {
        DebuggerBreakpoint.AttachVisualStudioDebugger(WellKnownDebugEnvironmentVariables.VSTEST_RUNNER_DEBUG_ATTACHVS);
        DebuggerBreakpoint.WaitForDebugger(WellKnownDebugEnvironmentVariables.VSTEST_RUNNER_DEBUG);

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

        Output = output;
        _testPlatformEventSource = testPlatformEventSource;
        _processHelper = processHelper;
        _environment = environment;
        _featureFlag = FeatureFlag.Instance;
        _runsettingsManager = RunSettingsManager.Instance;

        _runsettingsManager.AddDefaultRunSettings();
    }

    /// <summary>
    /// Instance to use for sending output.
    /// </summary>
    private IOutput Output { get; set; }

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

        IReadOnlyList<ArgumentProcessor> argumentProcessors = ArgumentProcessorFactory.GetProcessorList();

        ParseResult parseResult = new Parser().Parse(args, argumentProcessors);

        var serviceProvider = new ServiceProvider();
        var initializeInvocationContext = new InvocationContext(serviceProvider, parseResult);

        var argumentProcessorsCopy = argumentProcessors.ToList();

        var argOrder = argumentProcessors.OrderBy(a => a.Priority).ToList().Select(p => $"{p.Name} - {p.Priority}").ToList();

        serviceProvider.AddService(_ => argumentProcessorsCopy);
        serviceProvider.AddService(_ => initializeInvocationContext);
        serviceProvider.AddService(_ => SharedDependencies);

        serviceProvider.AddService(_ => Output);
        serviceProvider.AddService(_ => _processHelper);
        serviceProvider.AddService(_ => _environment);
        serviceProvider.AddService(_ => _featureFlag);
        serviceProvider.AddService<IFileHelper>(_ => new FileHelper());

        var isTelemetryOptedIn = Environment.GetEnvironmentVariable("VSTEST_TELEMETRY_OPTEDIN")?.Equals("1", StringComparison.Ordinal) == true;
        var metricsPublisher = MetricsPublisherFactory.GetMetricsPublisher(isTelemetryOptedIn, CommandLineOptions.Instance.IsDesignMode).GetAwaiter().GetResult();

        serviceProvider.AddService(_ => metricsPublisher);
        // Using instance because it is used in many other places, so we should eradicate it there first
        // to make sure we use the same instance no matter what the usage is.
        serviceProvider.AddService(_ => CommandLineOptions.Instance);


        _runsettingsManager.AddDefaultRunSettings();
        serviceProvider.AddService(_ => _runsettingsManager);
        serviceProvider.AddService(_ => RunSettingsHelper.Instance);

        // On syntax error print the error, and help.
        if (parseResult.Errors.Any())
        {
            var noLogoProcessor = new SplashScreenArgumentProcessor();
            var noLogo = parseResult.GetValueFor(noLogoProcessor);
            if (!noLogo)
            {
                var logoExecutor = ArgumentProcessorFactory.CreateExecutor(noLogoProcessor, initializeInvocationContext, noLogoProcessor.ExecutorType);
                logoExecutor.Initialize(parseResult);
                logoExecutor.Execute();
            }
            Output.Error(appendPrefix: false, string.Join(Environment.NewLine, parseResult.Errors));
            var helpArgumentProcessor = new HelpArgumentProcessor();
            var helpExecutor = ArgumentProcessorFactory.CreateExecutor(helpArgumentProcessor, initializeInvocationContext, helpArgumentProcessor.ExecutorType);
            helpExecutor.Initialize(parseResult);
            helpExecutor.Execute();

            _testPlatformEventSource.VsTestConsoleStop();
            return 1;
        }

        // Get the argument processors for the arguments, and initialize them.
        var initializeExitCode = RunIntialize(initializeInvocationContext, out List<(ArgumentProcessor, IArgumentExecutor)> processorsAndExecutors);
        if (initializeExitCode != 0)
        {
            if (!parseResult.Errors.Any())
            {
                Output.Error(appendPrefix: false, "Initialize failed but no error was reported.");
            }
            else
            {
                Output.Error(appendPrefix: false, string.Join("\n", parseResult.Errors));
            }
            //var executor = new HelpArgumentExecutor(Output, argumentProcessors.ToList());
            //executor.Initialize(parseResult);
            //executor.Execute();

            EqtTrace.Verbose("Executor.Execute: Exiting with exit code of {0}", initializeExitCode);
            _testPlatformEventSource.VsTestConsoleStop();
            return initializeExitCode;
        }

        // TODO: some of the argument processors are adding parameters for the lattter argument processors to pick them up,
        // this sucks, and we should not do that, or we should guard against it by adding those parameters to a special group
        // and verifying it is empty. This is probably why there is a initialize and execute phase.
        //
        //// Verify that the arguments are valid.
        //exitCode |= IdentifyDuplicateArguments(argumentProcessors);

        InvocationContext executeInvocationContext = new(serviceProvider, parseResult);
        var executeExitCode = RunExecute(executeInvocationContext, processorsAndExecutors);

        // REVIEW:  Use the test run result aggregator to update the exit code. <- yeah sure, but why here, why is the command not simply outputting this?
        // exitCode |= (TestRunResultAggregator.Instance.Outcome == TestOutcome.Passed) ? 0 : 1;

        _testPlatformEventSource.VsTestConsoleStop();

        _testPlatformEventSource.MetricsDisposeStart();

        // Disposing Metrics Publisher when VsTestConsole ends
        metricsPublisher.Dispose();

        _testPlatformEventSource.MetricsDisposeStop();

        EqtTrace.Verbose("Executor.Execute: Exiting with exit code of {0}", executeExitCode);
        return executeExitCode;
    }

    /// <summary>
    /// Get the list of argument processors for the arguments.
    /// </summary>
    /// <param name="args">Arguments provided to perform execution with.</param>
    /// <param name="processors">List of argument processors for the arguments.</param>
    /// <returns>0 if all of the processors were created successfully and 1 otherwise.</returns>
    private int RunIntialize(InvocationContext invocationContext, out List<(ArgumentProcessor, IArgumentExecutor)> processorsAndExecutors)
    {
        processorsAndExecutors = new List<(ArgumentProcessor, IArgumentExecutor)>();
        var argumentProcessors = ArgumentProcessorFactory.GetProcessorList(_featureFlag);
        argumentProcessors.Sort((p1, p2) => Comparer<ArgumentProcessorPriority>.Default.Compare(p1.Priority, p2.Priority));

        // Ensure we have an action argument.
        EnsureActionArgumentIsPresent(argumentProcessors);

        foreach (var processor in argumentProcessors)
        {
            object? value = null;
            if (processor.AlwaysExecute || invocationContext.ParseResult.TryGetValueFor(processor, out value))
            {
                IArgumentExecutor executor = ArgumentProcessorFactory.CreateExecutor(processor, invocationContext, processor.ExecutorType);
                processorsAndExecutors.Add((processor, executor));

                try
                {
                    executor.Initialize(invocationContext.ParseResult);
                    // Only run initialization for processors that are before the matched command.
                    if (processor.IsCommand)
                    {
                        break;
                    }
                }
                catch (Exception ex)
                {
                    if (ex is CommandLineException or TestPlatformException or SettingsException or InvalidOperationException)
                    {
                        EqtTrace.Error("ExecuteArgumentProcessor: failed to execute argument process: {0}", ex);
                        Output.Error(false, ex.Message);

                        // Send inner exception only when its message is different to avoid duplicate.
                        if (ex is TestPlatformException &&
                            ex.InnerException != null &&
                            !string.Equals(ex.InnerException.Message, ex.Message, StringComparison.CurrentCultureIgnoreCase))
                        {
                            Output.Error(false, ex.InnerException.Message);
                        }
                    }
                    else
                    {
                        // Let it throw - User must see crash and report it with stack trace!
                        // No need for recoverability as user will start a new vstest.console anyway
                        throw;
                    }

                    return 1;
                }
            }
        }

        //for (var index = 0; index < args.Length; index++)
        //{
        //    //var arg = args[index];
        //    //// If argument is '--', following arguments are key=value pairs for run settings.
        //    //if (arg.Equals("--"))
        //    //{
        //    //    var cliRunSettingsProcessor = processorFactory.CreateArgumentProcessor(arg, args.Skip(index + 1).ToArray());
        //    //    processors.Add(cliRunSettingsProcessor!);
        //    //    break;
        //    //}

        //    string arg = null;
        //    var processor = processorFactory.CreateArgumentProcessor(arg);

        //    if (processor != null)
        //    {
        //        processors.Add(processor);
        //    }
        //    else
        //    {
        //        // No known processor was found, report an error and continue
        //        Output.Error(false, string.Format(CultureInfo.CurrentCulture, CommandLineResources.NoArgumentProcessorFound, arg));

        //        // Add the help processor
        //        if (result == 0)
        //        {
        //            result = 1;
        //            processors.Add(processorFactory.CreateArgumentProcessor(HelpArgumentProcessor.CommandName)!);
        //        }
        //    }
        //}

        //// Add the internal argument processors that should always be executed.
        //// Examples: processors to enable loggers that are statically configured, and to start logging,
        //// should always be executed.
        //var processorsToAlwaysExecute = processorFactory.GetArgumentProcessorsToAlwaysExecute();
        //foreach (var processor in processorsToAlwaysExecute)
        //{
        //    // TODO: this just makes sure we don't add duplicates. But we won't need it later when we simply go over every
        //    // processort and try to bind parameter to it, or run it with all parameters
        //    //if (processors.Any(i => i.Metadata.Value.CommandName == processor.Metadata.Value.CommandName))
        //    //{
        //    //    continue;
        //    //}

        //    // We need to initialize the argument executor if it's set to always execute. This ensures it will be initialized with other executors.
        //    processors.Add(ArgumentProcessorFactory.WrapLazyProcessorToInitializeOnInstantiation(processor));
        //}


        //// Instantiate and initialize the processors in priority order.
        //processors.Sort((p1, p2) => Comparer<ArgumentProcessorPriority>.Default.Compare(p1.Priority, p2.Priority));
        //foreach (var processor in processors)
        //{
        //    IArgumentExecutor? executorInstance;
        //    try
        //    {
        //        // Ensure the instance is created.  Note that the Lazy not only instantiates
        //        // the argument processor, but also initializes it.
        //        // TODO: this is where we need to initialize the executor and do stuff.
        //        executorInstance = null;
        //    }
        //    catch (Exception ex)
        //    {
        //        if (ex is CommandLineException or TestPlatformException or SettingsException)
        //        {
        //            Output.Error(false, ex.Message);
        //            result = 1;
        //            _showHelp = false;
        //        }
        //        else if (ex is TestSourceException)
        //        {
        //            Output.Error(false, ex.Message);
        //            result = 1;
        //            _showHelp = false;
        //            break;
        //        }
        //        else
        //        {
        //            // Let it throw - User must see crash and report it with stack trace!
        //            // No need for recoverability as user will start a new vstest.console anyway
        //            throw;
        //        }
        //    }
        //}

        // TODO: nope, we will just do this by ParseError command that will be the first.
        // If some argument was invalid, add help argument processor in beginning(i.e. at highest priority)
        //if (result == 1 && _showHelp && processors.First() != HelpArgumentProcessor.CommandName)
        //{
        //    processors.Insert(0, processorFactory.CreateArgumentProcessor(HelpArgumentProcessor.CommandName)!);
        //}
        return 0;
    }

    private int RunExecute(InvocationContext invocationContext, List<(ArgumentProcessor, IArgumentExecutor)> processorsAndExecutors)
    {
        foreach (var (processor, executor) in processorsAndExecutors)
        {
            var result = ExecuteArgumentProcessor(executor);
            // Return when any invocation failed, or when we processed a command, and hence should not continue executing the next
            // executors in the list.
            if (result == ArgumentProcessorResult.Fail || result == ArgumentProcessorResult.Abort || processor.IsCommand)
            {
                return result is ArgumentProcessorResult.Fail or ArgumentProcessorResult.Abort ? 1 : 0;
            }
        }

        throw new InvalidOperationException("Every invocation should encounter at least one command, and so this code should never be reached.\n"
            + $"Invoked:\n\t'{string.Join("'\t\n'", processorsAndExecutors.Select(pe => pe.Item1.GetType().Name))}'");
    }

    /// <summary>
    /// Ensures that an action argument is present and if one is not, then the default action argument is added.
    /// </summary>
    /// <param name="argumentProcessors">The arguments that are being processed.</param>
    /// <param name="processorFactory">A factory for creating argument processors.</param>
    // TODO: this method is kinda pointless, since we don't configure this dynamically, and we don't re-check all the additional conditions here, like
    // making sure there is a command that has execute always, so adding any command will satisfy this method. And if we don't check we will see it in any interactive
    // test that we fail.
    private static void EnsureActionArgumentIsPresent(List<ArgumentProcessor> argumentProcessors)
    {
        ValidateArg.NotNull(argumentProcessors, nameof(argumentProcessors));

        if (!argumentProcessors.Any((processor) => processor.IsCommand))
        {
            throw new InvalidOperationException("There has to be at least one command processor.");
        }
    }

    /// <summary>
    /// Executes the argument processor
    /// </summary>
    /// <param name="executor">Argument processor to execute.</param>
    /// <param name="exitCode">Exit status of Argument processor</param>
    /// <returns> true if continue execution, false otherwise.</returns>
    private ArgumentProcessorResult ExecuteArgumentProcessor(IArgumentExecutor executor)
    {
        try
        {
            // TODO: Only executor that could return null is ResponseFileArgumentProcessor, maybe it could be updated
            // to follow a pattern similar to other processors and avoid returning null.
            return executor.Execute();
        }
        catch (Exception ex)
        {
            if (ex is CommandLineException or TestPlatformException or SettingsException or InvalidOperationException)
            {
                EqtTrace.Error("ExecuteArgumentProcessor: failed to execute argument process: {0}", ex);
                Output.Error(false, ex.Message);

                // Send inner exception only when its message is different to avoid duplicate.
                if (ex is TestPlatformException &&
                    ex.InnerException != null &&
                    !string.Equals(ex.InnerException.Message, ex.Message, StringComparison.CurrentCultureIgnoreCase))
                {
                    Output.Error(false, ex.InnerException.Message);
                }
            }
            else
            {
                // Let it throw - User must see crash and report it with stack trace!
                // No need for recoverability as user will start a new vstest.console anyway
                throw;
            }

            return ArgumentProcessorResult.Fail;
        }
    }
}

internal class SharedDependencyDictionary : Dictionary<Type, WeakReference>
{
}
