// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.Common.Interfaces;
using Microsoft.VisualStudio.TestPlatform.Common.Utilities;
using Microsoft.VisualStudio.TestPlatform.Utilities;

using CommandLineResources = Microsoft.VisualStudio.TestPlatform.CommandLine.Resources.Resources;

namespace Microsoft.VisualStudio.TestPlatform.CommandLine.Processors;

/// <summary>
/// Argument Executor for the "-e|--Environment|/e|/Environment" command line argument.
/// </summary>
internal class EnvironmentArgumentProcessor : ArgumentProcessor<string[]>
{
    public EnvironmentArgumentProcessor()
        : base(new string[] { "-e", "--environment" }, typeof(EnvironmentArgumentExecutor))
    {
        Priority = ArgumentProcessorPriority.Normal;
        HelpContentResourceName = CommandLineResources.EnvironmentArgumentHelp;
        HelpPriority = HelpContentPriority.EnvironmentArgumentProcessorHelpPriority;
    }
}

internal class EnvironmentArgumentExecutor : IArgumentExecutor
{
    /// <summary>
    /// Used when warning about overriden environment variables.
    /// </summary>
    private readonly IOutput _output;

    /// <summary>
    /// Used when setting Environemnt variables.
    /// </summary>
    private readonly IRunSettingsProvider _runSettingsProvider;

    /// <summary>
    /// Used when checking and forcing InIsolation mode.
    /// </summary>
    private readonly CommandLineOptions _commandLineOptions;
    public EnvironmentArgumentExecutor(CommandLineOptions commandLineOptions, IRunSettingsProvider runSettingsProvider, IOutput output)
    {
        _commandLineOptions = commandLineOptions;
        _output = output;
        _runSettingsProvider = runSettingsProvider;
    }

    /// <summary>
    /// Set the environment variables in RunSettings.xml
    /// </summary>
    /// <param name="argument">
    /// Environment variable to set.
    /// </param>
    public void Initialize(string? argument)
    {
        TPDebug.Assert(!StringUtils.IsNullOrWhiteSpace(argument));
        TPDebug.Assert(_output != null);
        TPDebug.Assert(_commandLineOptions != null);
        TPDebug.Assert(!StringUtils.IsNullOrWhiteSpace(_runSettingsProvider.ActiveRunSettings?.SettingsXml));

        var key = argument;
        var value = string.Empty;

        if (key.Contains("="))
        {
            value = key.Substring(key.IndexOf("=") + 1);
            key = key.Substring(0, key.IndexOf("="));
        }

        var node = _runSettingsProvider.QueryRunSettingsNode($"RunConfiguration.EnvironmentVariables.{key}");
        if (node != null)
        {
            _output.Warning(true, CommandLineResources.EnvironmentVariableXIsOverriden, key);
        }

        _runSettingsProvider.UpdateRunSettingsNode($"RunConfiguration.EnvironmentVariables.{key}", value);

        if (!_commandLineOptions.InIsolation)
        {
            _commandLineOptions.InIsolation = true;
            _runSettingsProvider.UpdateRunSettingsNode(InIsolationArgumentExecutor.RunSettingsPath, "true");
        }
    }

    // Nothing to do here, the work was done in initialization.
    public ArgumentProcessorResult Execute() => ArgumentProcessorResult.Success;
}
