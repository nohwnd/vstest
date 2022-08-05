// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

using CommandLineResources = Microsoft.VisualStudio.TestPlatform.CommandLine.Resources.Resources;

namespace Microsoft.VisualStudio.TestPlatform.CommandLine.Processors;

/// <summary>
/// An argument processor that allows the user to disable fakes
/// from the command line using the --DisableAutoFakes|/DisableAutoFakes command line switch.
/// </summary>
internal class DisableAutoFakesArgumentProcessor : ArgumentProcessor<bool>
{
    public DisableAutoFakesArgumentProcessor() :
        base("/DisableAutoFakes", typeof(DisableAutoFakesArgumentExecutor))
    {
        HelpPriority = HelpContentPriority.DisableAutoFakesArgumentProcessorHelpPriority;
    }
}

internal class DisableAutoFakesArgumentExecutor : IArgumentExecutor
{
    private readonly CommandLineOptions _commandLineOptions;

    public DisableAutoFakesArgumentExecutor(CommandLineOptions commandLineOptions)
    {
        _commandLineOptions = commandLineOptions;
    }

    public void Initialize(string? argument)
    {
        if (argument.IsNullOrWhiteSpace() || !bool.TryParse(argument, out bool value))
        {
            throw new CommandLineException(CommandLineResources.DisableAutoFakesUsage);
        }

        _commandLineOptions.DisableAutoFakes = value;
    }

    public ArgumentProcessorResult Execute()
    {
        // Nothing to do since we updated the parameter during initialize parameter
        return ArgumentProcessorResult.Success;
    }
}
