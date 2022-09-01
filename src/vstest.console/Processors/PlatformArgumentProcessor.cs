// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Globalization;
using System.Linq;

using Microsoft.VisualStudio.TestPlatform.Common.Interfaces;
using Microsoft.VisualStudio.TestPlatform.Common.Utilities;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.PlatformAbstractions;
using Microsoft.VisualStudio.TestPlatform.Utilities.Helpers.Interfaces;

using CommandLineResources = Microsoft.VisualStudio.TestPlatform.CommandLine.Resources.Resources;

namespace Microsoft.VisualStudio.TestPlatform.CommandLine.Processors;

/// <summary>
///  An argument processor that allows the user to specify the target platform architecture
///  for test run.
/// </summary>
internal class PlatformArgumentProcessor : ArgumentProcessor<PlatformArchitecture>
{
    public PlatformArgumentProcessor()
        : base(new string[] { "-a", "--arch", "--platform" }, typeof(PlatformArgumentExecutor))
    {
        Priority = ArgumentProcessorPriority.AutoUpdateRunSettings;
        HelpContentResourceName = CommandLineResources.PlatformArgumentHelp;
        HelpPriority = HelpContentPriority.PlatformArgumentProcessorHelpPriority;
    }
}

/// <summary>
/// Argument Executor for the "/Platform" command line argument.
/// </summary>
internal class PlatformArgumentExecutor : IArgumentExecutor
{
    /// <summary>
    /// Used for getting sources.
    /// </summary>
    private readonly CommandLineOptions _commandLineOptions;

    private readonly IRunSettingsProvider _runSettingsManager;
    private readonly IRunSettingsHelper _runsettingsHelper;
    public const string RunSettingsPath = "RunConfiguration.TargetPlatform";

    /// <summary>
    /// Default constructor.
    /// </summary>
    /// <param name="options"> The options. </param>
    /// <param name="runSettingsManager"> The runsettings manager. </param>
    public PlatformArgumentExecutor(CommandLineOptions options, IRunSettingsProvider runSettingsManager, IRunSettingsHelper runSettingsHelper)
    {
        ValidateArg.NotNull(options, nameof(options));
        ValidateArg.NotNull(runSettingsManager, nameof(runSettingsManager));
        ValidateArg.NotNull(runSettingsHelper, nameof(runSettingsHelper));
        _commandLineOptions = options;
        _runSettingsManager = runSettingsManager;
        _runsettingsHelper = runSettingsHelper;
    }

    /// <summary>
    /// Initializes with the argument that was provided with the command.
    /// </summary>
    /// <param name="argument">Argument that was provided with the command.</param>
    public void Initialize(ParseResult parseResult)
    {
        var platform = parseResult.GetValueFor(new PlatformArgumentProcessor());

        _runsettingsHelper.IsDefaultTargetArchitecture = false;
        // TODO: Yet another place where we convert PlatformArchitecture to Architecture.
        _commandLineOptions.TargetArchitecture = (Architecture)Enum.Parse(typeof(Architecture), platform.ToString());
        _runSettingsManager.UpdateRunSettingsNode(RunSettingsPath, platform.ToString());

        EqtTrace.Info("Using platform:{0}", _commandLineOptions.TargetArchitecture);
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
