// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;

using Microsoft.VisualStudio.TestPlatform.CommandLine.Internal;
using Microsoft.VisualStudio.TestPlatform.CommandLine.Processors.Utilities;
using Microsoft.VisualStudio.TestPlatform.CommandLine2;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.PlatformAbstractions.Interfaces;
using Microsoft.VisualStudio.TestPlatform.Utilities;
using Microsoft.VisualStudio.TestPlatform.Utilities.Helpers.Interfaces;

using CommandLineResources = Microsoft.VisualStudio.TestPlatform.CommandLine.Resources.Resources;

namespace Microsoft.VisualStudio.TestPlatform.CommandLine.Processors;

// TODO: Add nullable? Because this can be empty? Or rather default value factory?
// TODO: Add validator for directory or file (FileSystemInfo)
internal class EnableDiagArgumentProcessor : ArgumentProcessor<string>
{
    public EnableDiagArgumentProcessor()
        // TODO: maybe environment variables could be just part of the binding logic and
        // we would just add them as aliases?
        : base(new[]
        {
            "-d", "--diag" ,
            // --diag verbosity?
        }, typeof(EnableDiagArgumentExecutor))
    {
        // Execute this always because we want to check environment variables.
        AlwaysExecute = true;

        Priority = ArgumentProcessorPriority.Diag;
        HelpContentResourceName = CommandLineResources.EnableDiagUsage;
        HelpPriority = HelpContentPriority.EnableDiagArgumentProcessorHelpPriority;
    }
}

/// <summary>
/// The argument executor.
/// </summary>
internal class EnableDiagArgumentExecutor : IArgumentExecutor
{
    private readonly InvocationContext _context;
    private readonly IFileHelper _fileHelper;
    private readonly IProcessHelper _processHelper;
    private readonly IOutput _output;

    /// <summary>
    /// Parameter for trace level
    /// </summary>
    public const string TraceLevelParam = "tracelevel";

    /// <summary>
    /// Default constructor.
    /// </summary>
    /// <param name="fileHelper">The file helper.</param>
    public EnableDiagArgumentExecutor(InvocationContext invocationContext, IFileHelper fileHelper, IProcessHelper processHelper, IOutput output)
    {
        _context = invocationContext;
        _fileHelper = fileHelper;
        _processHelper = processHelper;
        _output = output;
    }

    /// <summary>
    /// Initializes with the argument that was provided with the command.
    /// </summary>
    /// <param name="argument">Argument that was provided with the command.</param>
    public void Initialize(ParseResult parseResult)
    {
        var diagParameterProvided = parseResult.TryGetValueFor(new EnableDiagArgumentProcessor(), out var argument);

        if (!diagParameterProvided)
        {
            // This takes a path to log directory and log.txt file. Same as the --diag parameter, e.g. VSTEST_DIAG="logs\log.txt"
            var path = Environment.GetEnvironmentVariable("VSTEST_DIAG");
            // This takes Verbose, Info (not Information), Warning, and Error.
            var diagVerbosity = Environment.GetEnvironmentVariable("VSTEST_DIAG_VERBOSITY");
            if (path.IsNullOrWhiteSpace())
            {
                // Path is not provided via env variable, diag is not enabled.
                return;
            }
            else
            {
                var verbosity = TraceLevel.Verbose;
                if (diagVerbosity != null)
                {
                    if (Enum.TryParse<TraceLevel>(diagVerbosity, ignoreCase: true, out var parsedVerbosity))
                    {
                        verbosity = parsedVerbosity;
                    }
                }

                argument = $"{path};TraceLevel={verbosity}";
            }
        }


        string exceptionMessage = string.Format(CultureInfo.CurrentCulture, CommandLineResources.InvalidDiagArgument, argument);

        // Throw error if argument is null or empty.
        if (argument.IsNullOrWhiteSpace())
        {
            throw new CommandLineException(exceptionMessage);
        }

        // Get diag argument list.
        var diagArgumentList = ArgumentProcessorUtilities.GetArgumentList(argument, ArgumentProcessorUtilities.SemiColonArgumentSeparator, exceptionMessage);

        // Get diag file path.
        // Note: Even though semi colon is valid file path, we are not respecting the file name having semi-colon [As we are separating arguments based on semi colon].
        var diagFilePathArg = diagArgumentList[0];
        var diagFilePath = GetDiagFilePath(diagFilePathArg);

        // Get diag parameters.
        var diagParameterArgs = diagArgumentList.Skip(1);
        var diagParameters = ArgumentProcessorUtilities.GetArgumentParameters(diagParameterArgs, ArgumentProcessorUtilities.EqualNameValueSeparator, exceptionMessage);

        // Initialize diag logging.
        InitializeDiagLogging(diagFilePath, diagParameters);

        // Write version to the log here, because that is the
        // first place where we know if we log or not.
        EqtTrace.Verbose($"Version: {Product.Version} Current process architecture: {_processHelper.GetCurrentProcessArchitecture()}");
        // https://docs.microsoft.com/en-us/dotnet/api/system.reflection.assembly.location?view=net-6.0#remarks
        // In .NET 5 and later versions, for bundled assemblies, the value returned is an empty string.
        string objectTypeLocation = typeof(object).Assembly.Location;
        if (!objectTypeLocation.IsNullOrEmpty())
        {
            EqtTrace.Verbose($"Runtime location: {Path.GetDirectoryName(objectTypeLocation)}");
        }
    }

    /// <summary>
    /// Executes the argument processor.
    /// </summary>
    /// <returns>The <see cref="ArgumentProcessorResult"/>.</returns>
    public ArgumentProcessorResult Execute()
    {
        // Nothing to do since we updated the parameter during initialize parameter
        return ArgumentProcessorResult.Success;
    }

    /// <summary>
    /// Initialize diag logging.
    /// </summary>
    /// <param name="diagFilePath">Diag file path.</param>
    /// <param name="diagParameters">Diag parameters</param>
    private void InitializeDiagLogging(string diagFilePath, Dictionary<string, string> diagParameters)
    {
        // Get trace level from diag parameters.
        var traceLevel = GetDiagTraceLevel(diagParameters);

        // Initialize trace.
        // Trace initialized is false in case of any exception at time of initialization like Catch exception(UnauthorizedAccessException, PathTooLongException...)
        var traceInitialized = EqtTrace.InitializeTrace(diagFilePath, traceLevel);

        // Show console warning in case trace is not initialized.
        if (!traceInitialized && !StringUtils.IsNullOrEmpty(EqtTrace.ErrorOnInitialization))
        {
            _output.Warning(false, EqtTrace.ErrorOnInitialization);
        }
    }

    /// <summary>
    /// Gets diag trace level.
    /// </summary>
    /// <param name="diagParameters">Diag parameters.</param>
    /// <returns>Diag trace level.</returns>
    private static PlatformTraceLevel GetDiagTraceLevel(Dictionary<string, string> diagParameters)
    {
        // If diag parameters is null, set value of trace level as verbose.
        if (diagParameters == null)
        {
            return PlatformTraceLevel.Verbose;
        }

        // Get trace level from diag parameters.
        var traceLevelExists = diagParameters.TryGetValue(TraceLevelParam, out var traceLevelStr);
        if (traceLevelExists && Enum.TryParse(traceLevelStr, true, out PlatformTraceLevel traceLevel))
        {
            return traceLevel;
        }

        // Default value of diag trace level is verbose.
        return PlatformTraceLevel.Verbose;
    }

    /// <summary>
    /// Gets diag file path.
    /// </summary>
    /// <param name="diagFilePathArgument">Diag file path argument.</param>
    /// <returns>Diag file path.</returns>
    private string GetDiagFilePath(string diagFilePathArgument)
    {
        // Remove double quotes if present.
        diagFilePathArgument = diagFilePathArgument.Replace("\"", "");

        // If we provide a directory we don't need to create the base directory.
        if (!diagFilePathArgument.EndsWith(@"\") && !diagFilePathArgument.EndsWith("/"))
        {
            // Create base directory for diag file path (if doesn't exist)
            CreateDirectoryIfNotExists(diagFilePathArgument);
        }

        // return full diag file path. (This is done so that vstest and testhost create logs at same location.)
        return Path.GetFullPath(diagFilePathArgument);
    }

    /// <summary>
    /// Create directory if not exists.
    /// </summary>
    /// <param name="filePath">File path.</param>
    private void CreateDirectoryIfNotExists(string filePath)
    {
        // Create the base directory of file path if doesn't exist.
        // Directory could be empty if just a filename is provided. E.g. log.txt
        var directory = Path.GetDirectoryName(filePath);
        if (!StringUtils.IsNullOrEmpty(directory) && !_fileHelper.DirectoryExists(directory))
        {
            _fileHelper.CreateDirectory(directory);
        }
    }
}
