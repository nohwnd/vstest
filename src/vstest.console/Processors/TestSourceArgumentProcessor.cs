// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

using Microsoft.VisualStudio.TestPlatform.ObjectModel;

namespace Microsoft.VisualStudio.TestPlatform.CommandLine.Processors;

/// <summary>
/// Argument Executor which handles adding the source provided to the TestManager.
/// </summary>
internal class TestSourceArgumentProcessor : ArgumentProcessor<string>
{
    public TestSourceArgumentProcessor()
        : base("/TestSource", typeof(TestSourceArgumentExecutor))
    {
        IsHidden = true;

    }
}

internal class TestSourceArgumentExecutor : IArgumentExecutor
{
    /// <summary>
    /// Used for adding sources to the test manager.
    /// </summary>
    private readonly CommandLineOptions _testSources;

    public TestSourceArgumentExecutor(CommandLineOptions testSources)
    {
        ValidateArg.NotNull(testSources, nameof(testSources));
        _testSources = testSources;
    }

    public void Initialize(string? argument)
    {
        if (!argument.IsNullOrEmpty())
        {
            _testSources.AddSource(argument);
        }
    }

    public ArgumentProcessorResult Execute()
    {
        // Nothing to do. Our work was done during initialize.
        return ArgumentProcessorResult.Success;
    }
}
