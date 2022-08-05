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
    public PlatformArgumentExecutor(CommandLineOptions options, IRunSettingsProvider runSettingsManager)
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
            throw new CommandLineException(CommandLineResources.PlatformTypeRequired);
        }

        var validPlatforms = Enum.GetValues(typeof(Architecture)).Cast<Architecture>()
            .Where(e => e is not Architecture.AnyCPU and not Architecture.Default)
            .ToList();

        var validPlatform = Enum.TryParse(argument, true, out Architecture platform);
        if (validPlatform)
        {
            // Ensure that the case-insensitively parsed enum is in the list of valid platforms.
            // This filters out:
            //  - values that parse correctly but the enum does not define them (e.g. "1" parses as valid enum value 1)
            //  - the Default or AnyCpu that are not valid target to provide via settings
            validPlatform = validPlatforms.Contains(platform);
        }

        if (validPlatform)
        {
            _runsettingsHelper.IsDefaultTargetArchitecture = false;
            _commandLineOptions.TargetArchitecture = platform;
            _runSettingsManager.UpdateRunSettingsNode(RunSettingsPath, platform.ToString());
        }
        else
        {
            throw new CommandLineException(
                string.Format(CultureInfo.CurrentCulture, CommandLineResources.InvalidPlatformType, argument, string.Join(", ", validPlatforms)));
        }

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
