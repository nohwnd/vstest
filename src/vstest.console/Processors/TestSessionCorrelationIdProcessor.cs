// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.CodeDom;
using System.Net;

using Microsoft.VisualStudio.TestPlatform.ObjectModel;

using CommandLineResources = Microsoft.VisualStudio.TestPlatform.CommandLine.Resources.Resources;

namespace Microsoft.VisualStudio.TestPlatform.CommandLine.Processors;

/// <summary>
/// Argument Processor for the "--testSessionCorrelationId|/TestSessionCorrelationId" command line argument.
/// </summary>
internal class TestSessionCorrelationIdProcessor : ArgumentProcessor<string>
{
    public TestSessionCorrelationIdProcessor()
        : base("--TestSessionCorrelationId", typeof(TestSessionCorrelationIdProcessorModeProcessorExecutor))
    {
        // We put priority at the same level of the argument processor for runsettings passed as argument through cli.
        // We'll be sure to run before test run or artifact post processing.
        Priority = ArgumentProcessorPriority.CliRunSettings;
        IsHiddenInHelp = true;
    }
}

/// <summary>
/// Argument Executor for the "/TestSessionCorrelationId" command line argument.
/// </summary>
internal class TestSessionCorrelationIdProcessorModeProcessorExecutor : IArgumentExecutor
{
    private readonly CommandLineOptions _commandLineOptions;

    public TestSessionCorrelationIdProcessorModeProcessorExecutor(CommandLineOptions options)
    {
        _commandLineOptions = options ?? throw new ArgumentNullException(nameof(options));
    }

    public void Initialize(string? argument)
    {
        if (argument.IsNullOrEmpty())
        {
            throw new CommandLineException(CommandLineResources.InvalidTestSessionCorrelationId);
        }

        _commandLineOptions.TestSessionCorrelationId = argument;
        EqtTrace.Verbose($"TestSessionCorrelationIdProcessorModeProcessorExecutor.Initialize: TestSessionCorrelationId '{argument}'");
    }

    public ArgumentProcessorResult Execute() => ArgumentProcessorResult.Success;
}
