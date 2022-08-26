// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.ObjectModel;

namespace Microsoft.VisualStudio.TestPlatform.CommandLine.Processors;

/// <summary>
/// Argument Executor which handles adding the source provided to the TestManager.
/// </summary>
internal class TestSourceArgumentProcessor : ArgumentProcessor<string[]>
{
    public TestSourceArgumentProcessor()
        : base("--TestSource", typeof(TestSourceArgumentExecutor))
    {
        IsHidden = true;
        IsDefault = true;
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

    public void Initialize(ParseResult parseResult)
    {
        var sources = parseResult.GetValueFor(new TestSourceArgumentProcessor());
        if (sources == null)
        {
            return;
        }

        foreach (var source in sources)
        {
            if (!source.IsNullOrEmpty())
            {
                _testSources.AddSource(source);
            }
        }
    }

    public ArgumentProcessorResult Execute()
    {
        // Nothing to do. Our work was done during initialize.
        return ArgumentProcessorResult.Success;
    }
}
