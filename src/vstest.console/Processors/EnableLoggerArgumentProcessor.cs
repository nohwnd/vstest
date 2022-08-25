// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Linq;

using Microsoft.VisualStudio.TestPlatform.CommandLine.Processors.Utilities;
using Microsoft.VisualStudio.TestPlatform.Common.Interfaces;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

using CommandLineResources = Microsoft.VisualStudio.TestPlatform.CommandLine.Resources.Resources;

namespace Microsoft.VisualStudio.TestPlatform.CommandLine.Processors;

/// <summary>
/// An argument processor that allows the user to enable a specific logger
/// from the command line using the --Logger|/Logger command line switch.
/// </summary>
internal class EnableLoggerArgumentProcessor : ArgumentProcessor<string[]>
{
    public EnableLoggerArgumentProcessor()
        : base(new string[] { "-l", "--logger" }, typeof(EnableLoggerArgumentExecutor))
    {
        // REVEW: There was a comment somewhere saying that this should always run to setup loggers even when user provides none.
        AlwaysExecute = true;
        Priority = ArgumentProcessorPriority.Logging;
#if NETFRAMEWORK
        HelpContentResourceName = CommandLineResources.EnableLoggersArgumentHelp;
#else
        HelpContentResourceName = CommandLineResources.EnableLoggerArgumentsInNetCore;
#endif
        HelpPriority = HelpContentPriority.EnableLoggerArgumentProcessorHelpPriority;
    }
}

internal class EnableLoggerArgumentExecutor : IArgumentExecutor
{
    private readonly IRunSettingsProvider _runSettingsManager;

    public EnableLoggerArgumentExecutor(IRunSettingsProvider runSettingsManager)
    {
        ValidateArg.NotNull(runSettingsManager, nameof(runSettingsManager));
        _runSettingsManager = runSettingsManager;
    }

    public void Initialize(string? argument)
    {
        string exceptionMessage = string.Format(CultureInfo.CurrentCulture, CommandLineResources.LoggerUriInvalid, argument);

        // Throw error in case logger argument null or empty.
        if (argument.IsNullOrWhiteSpace())
        {
            throw new CommandLineException(exceptionMessage);
        }

        // Get logger argument list.
        var loggerArgumentList = ArgumentProcessorUtilities.GetArgumentList(argument, ArgumentProcessorUtilities.SemiColonArgumentSeparator, exceptionMessage);

        // Get logger identifier.
        var loggerIdentifier = loggerArgumentList[0];
        if (loggerIdentifier.Contains("="))
        {
            throw new CommandLineException(exceptionMessage);
        }

        // Get logger parameters
        var loggerParameterArgs = loggerArgumentList.Skip(1);
        var loggerParameters = ArgumentProcessorUtilities.GetArgumentParameters(loggerParameterArgs, ArgumentProcessorUtilities.EqualNameValueSeparator, exceptionMessage);

        // Add logger to run settings.
        LoggerUtilities.AddLoggerToRunSettings(loggerIdentifier, loggerParameters, _runSettingsManager);
    }

    public ArgumentProcessorResult Execute()
    {
        // Nothing to do since we enabled the logger in the initialize method.
        return ArgumentProcessorResult.Success;
    }
}


