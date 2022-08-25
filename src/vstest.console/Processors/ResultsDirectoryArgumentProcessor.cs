// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Globalization;
using System.IO;
using System.Security;

using Microsoft.VisualStudio.TestPlatform.Common;
using Microsoft.VisualStudio.TestPlatform.Common.Interfaces;
using Microsoft.VisualStudio.TestPlatform.Common.Utilities;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

using CommandLineResources = Microsoft.VisualStudio.TestPlatform.CommandLine.Resources.Resources;

namespace Microsoft.VisualStudio.TestPlatform.CommandLine.Processors;

/// <summary>
/// Allows the user to specify a path to save test results.
/// </summary>
internal class ResultsDirectoryArgumentProcessor : ArgumentProcessor<DirectoryInfo>
{
    public ResultsDirectoryArgumentProcessor()
        : base(new[] { "--ResultsDirectory", "--results-directory" }, typeof(ResultsDirectoryArgumentExecutor))
    {
        Priority = ArgumentProcessorPriority.AutoUpdateRunSettings;
        HelpContentResourceName = CommandLineResources.ResultsDirectoryArgumentHelp;
        HelpPriority = HelpContentPriority.ResultsDirectoryArgumentProcessorHelpPriority;
    }
}

internal class ResultsDirectoryArgumentExecutor : IArgumentExecutor
{
    /// <summary>
    /// Used for getting sources.
    /// </summary>
    private readonly CommandLineOptions _commandLineOptions;

    private readonly IRunSettingsProvider _runSettingsManager;

    public const string RunSettingsPath = "RunConfiguration.ResultsDirectory";

    /// <summary>
    /// Default constructor.
    /// </summary>
    /// <param name="options"> The options. </param>
    /// <param name="testPlatform">The test platform</param>
    public ResultsDirectoryArgumentExecutor(CommandLineOptions options, IRunSettingsProvider runSettingsManager)
    {
        ValidateArg.NotNull(options, nameof(options));
        ValidateArg.NotNull(runSettingsManager, nameof(runSettingsManager));

        _commandLineOptions = options;
        _runSettingsManager = runSettingsManager;
    }

    /// <summary>
    /// Initializes with the argument that was provided with the command.
    /// </summary>
    /// <param name="argument">Argument that was provided with the command.</param>
    public void Initialize(string? argument)
    {
        if (argument.IsNullOrWhiteSpace())
        {
            throw new CommandLineException(CommandLineResources.ResultsDirectoryValueRequired);
        }

        try
        {
            if (!Path.IsPathRooted(argument))
            {
                argument = Path.GetFullPath(argument);
            }

            var di = Directory.CreateDirectory(argument);
        }
        catch (Exception ex) when (ex is NotSupportedException or SecurityException or ArgumentException or PathTooLongException or IOException)
        {
            throw new CommandLineException(string.Format(CultureInfo.CurrentCulture, CommandLineResources.InvalidResultsDirectoryPathCommand, argument, ex.Message));
        }

        _commandLineOptions.ResultsDirectory = argument;
        _runSettingsManager.UpdateRunSettingsNode(RunSettingsPath, argument);
    }

    /// <summary>
    /// Executes the argument processor.
    /// </summary>
    /// <returns> The <see cref="ArgumentProcessorResult"/>. </returns>
    public ArgumentProcessorResult Execute()
    {
        // Nothing to do since we updated the parameter during initialize parameter
        return ArgumentProcessorResult.Success;
    }
}

