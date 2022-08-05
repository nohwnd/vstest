// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;

using Microsoft.VisualStudio.TestPlatform.Common.Interfaces;
using Microsoft.VisualStudio.TestPlatform.Common.Utilities;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

using CommandLineResources = Microsoft.VisualStudio.TestPlatform.CommandLine.Resources.Resources;

namespace Microsoft.VisualStudio.TestPlatform.CommandLine.Processors;

/// <summary>
/// Parallel Option Argument processor that allows the user to specify if tests are to be run in parallel.
/// </summary>
internal class ParallelArgumentProcessor : ArgumentProcessor<bool>
{
    public ParallelArgumentProcessor()
        : base("--parallel", typeof(ParallelArgumentExecutor))
    {
        Priority = ArgumentProcessorPriority.AutoUpdateRunSettings;
        HelpContentResourceName = CommandLineResources.ParallelArgumentProcessorHelp;
        HelpPriority = HelpContentPriority.ParallelArgumentProcessorHelpPriority;
    }
}

internal class ParallelArgumentExecutor : IArgumentExecutor
{
    /// <summary>
    /// Used for getting sources.
    /// </summary>
    private readonly CommandLineOptions _commandLineOptions;

    private readonly IRunSettingsProvider _runSettingsManager;

    public const string RunSettingsPath = "RunConfiguration.MaxCpuCount";

    /// <summary>
    /// Default constructor.
    /// </summary>
    /// <param name="options"> The options. </param>
    /// <param name="runSettingsManager"> The runsettings manager. </param>
    public ParallelArgumentExecutor(CommandLineOptions options, IRunSettingsProvider runSettingsManager)
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
        // parallel does not require any argument, throws exception if argument specified
        if (!argument.IsNullOrWhiteSpace())
        {
            throw new CommandLineException(
                string.Format(CultureInfo.CurrentCulture, CommandLineResources.InvalidParallelCommand, argument));
        }

        _commandLineOptions.Parallel = true;
        _runSettingsManager.UpdateRunSettingsNode(RunSettingsPath, "0");
    }

    /// <summary>
    /// The output path is already set, return success.
    /// </summary>
    /// <returns> The <see cref="ArgumentProcessorResult"/> Success </returns>
    public ArgumentProcessorResult Execute()
    {
        return ArgumentProcessorResult.Success;
    }
}
