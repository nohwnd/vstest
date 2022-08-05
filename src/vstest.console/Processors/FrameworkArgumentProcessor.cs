// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Globalization;

using Microsoft.VisualStudio.TestPlatform.Common;
using Microsoft.VisualStudio.TestPlatform.Common.Interfaces;
using Microsoft.VisualStudio.TestPlatform.Common.Utilities;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.Utilities;

using CommandLineResources = Microsoft.VisualStudio.TestPlatform.CommandLine.Resources.Resources;

namespace Microsoft.VisualStudio.TestPlatform.CommandLine.Processors;

/// <summary>
///  An argument processor that allows the user to specify the target platform architecture
///  for test run.
/// </summary>
// TODO: Add validator for Framework input.
internal class FrameworkArgumentProcessor : ArgumentProcessor<string>
{
    public FrameworkArgumentProcessor()
        : base(new string[] { "-f", "--framework" }, typeof(FrameworkArgumentExecutor))
    {
        Priority = ArgumentProcessorPriority.AutoUpdateRunSettings;
        HelpContentResourceName = CommandLineResources.FrameworkArgumentHelp;
        HelpPriority = HelpContentPriority.FrameworkArgumentProcessorHelpPriority;
    }
}

internal class FrameworkArgumentExecutor : IArgumentExecutor
{
    /// <summary>
    /// Used for getting sources.
    /// </summary>
    private readonly CommandLineOptions _commandLineOptions;

    private readonly IRunSettingsProvider _runSettingsManager;

    public const string RunSettingsPath = "RunConfiguration.TargetFrameworkVersion";

    public FrameworkArgumentExecutor(CommandLineOptions options, IRunSettingsProvider runSettingsManager)
    {
        ValidateArg.NotNull(options, nameof(options));
        ValidateArg.NotNull(runSettingsManager, nameof(runSettingsManager));
        _commandLineOptions = options;
        _runSettingsManager = runSettingsManager;
    }

    public void Initialize(string? argument)
    {
        if (argument.IsNullOrWhiteSpace())
        {
            throw new CommandLineException(CommandLineResources.FrameworkVersionRequired);
        }

        var validFramework = Framework.FromString(argument);
        _commandLineOptions.TargetFrameworkVersion = validFramework ?? throw new CommandLineException(
            string.Format(CultureInfo.CurrentCulture, CommandLineResources.InvalidFrameworkVersion, argument));

        if (_commandLineOptions.TargetFrameworkVersion != Framework.DefaultFramework
            && !StringUtils.IsNullOrWhiteSpace(_commandLineOptions.SettingsFile)
            && MSTestSettingsUtilities.IsLegacyTestSettingsFile(_commandLineOptions.SettingsFile))
        {
            // Legacy testsettings file support only default target framework.
            IOutput output = ConsoleOutput.Instance;
            output.Warning(
                false,
                CommandLineResources.TestSettingsFrameworkMismatch,
                _commandLineOptions.TargetFrameworkVersion.ToString(),
                Framework.DefaultFramework.ToString());
        }
        else
        {
            _runSettingsManager.UpdateRunSettingsNode(RunSettingsPath,
                validFramework.ToString());
        }

        EqtTrace.Info("Using .Net Framework version:{0}", _commandLineOptions.TargetFrameworkVersion);
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
