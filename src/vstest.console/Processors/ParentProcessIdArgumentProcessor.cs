// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

using Microsoft.VisualStudio.TestPlatform.ObjectModel;

using CommandLineResources = Microsoft.VisualStudio.TestPlatform.CommandLine.Resources.Resources;

namespace Microsoft.VisualStudio.TestPlatform.CommandLine.Processors;

/// <summary>
/// Argument Processor for the "--ParentProcessId|/ParentProcessId" command line argument.
/// </summary>
internal class ParentProcessIdArgumentProcessor : ArgumentProcessor<int>
{
    public ParentProcessIdArgumentProcessor()
        : base("--ParentProcessId", typeof(ParentProcessIdArgumentExecutor))
    {
        // Hide this because we use it just for design mode, and there VSTestConsoleWrapper
        // is supposed to know the details of what to provide.
        IsHiddenInHelp = true;

        Priority = ArgumentProcessorPriority.DesignMode;
        HelpContentResourceName = CommandLineResources.ParentProcessIdArgumentHelp;
        HelpPriority = HelpContentPriority.ParentProcessIdArgumentProcessorHelpPriority;
    }
}

/// <summary>
/// Argument Executor for the "/ParentProcessId" command line argument.
/// </summary>
internal class ParentProcessIdArgumentExecutor : IArgumentExecutor
{
    /// <summary>
    /// Used for getting sources.
    /// </summary>
    private readonly CommandLineOptions _commandLineOptions;

    public ParentProcessIdArgumentExecutor(CommandLineOptions options)
    {
        ValidateArg.NotNull(options, nameof(options));
        _commandLineOptions = options;
    }

    /// <summary>
    /// Initializes with the argument that was provided with the command.
    /// </summary>
    /// <param name="argument">Argument that was provided with the command.</param>
    public void Initialize(string? argument)
    {
        if (argument.IsNullOrWhiteSpace() || !int.TryParse(argument, out int parentProcessId))
        {
            throw new CommandLineException(CommandLineResources.InvalidParentProcessIdArgument);
        }

        _commandLineOptions.ParentProcessId = parentProcessId;
    }

    /// <summary>
    /// ParentProcessId is already set, return success.
    /// </summary>
    /// <returns> The <see cref="ArgumentProcessorResult"/> Success </returns>
    public ArgumentProcessorResult Execute()
    {
        // Nothing to do here, the work was done in initialization.
        return ArgumentProcessorResult.Success;
    }
}
