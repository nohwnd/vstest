// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Globalization;

using Microsoft.VisualStudio.TestPlatform.CommandLine2;
using Microsoft.VisualStudio.TestPlatform.Common.ExtensionFramework;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;
using Microsoft.VisualStudio.TestPlatform.Utilities;

using CommandLineResources = Microsoft.VisualStudio.TestPlatform.CommandLine.Resources.Resources;

namespace Microsoft.VisualStudio.TestPlatform.CommandLine.Processors;

internal class ListExecutorsArgumentProcessor : ArgumentProcessor<bool>, IExecutorCreator
{
    public ListExecutorsArgumentProcessor()
        : base("--ListExecutors", typeof(ListExecutorsArgumentExecutor))
    {
        IsCommand = true;
        IsHiddenInHelp = true;
    }

    public Func<InvocationContext, IArgumentExecutor> CreateExecutor { get; } =
        c => new ListExecutorsArgumentExecutor(ConsoleOutput.Instance, new Client.TestPlatform());
}


internal class ListExecutorsArgumentExecutor : IArgumentExecutor
{
    private readonly IOutput _output;
    private readonly ITestPlatform _testPlatform;
    private readonly TestExecutorExtensionManager _extensionManager;
    private bool _shouldExecute;

    public ListExecutorsArgumentExecutor(IOutput output, ITestPlatform testPlatform)
    {
        _output = output;
        // Test platform populates extension manager in constructor.
        _testPlatform = testPlatform;
        _extensionManager = TestExecutorExtensionManager.Create();
    }

    public void Initialize(ParseResult parseResult)
    {
        _shouldExecute = parseResult.GetValueFor(new ListExecutorsArgumentProcessor());
    }

    public ArgumentProcessorResult Execute()
    {
        if (!_shouldExecute)
        {
            return ArgumentProcessorResult.Success;
        }

        _output.WriteLine(CommandLineResources.AvailableExecutorsHeaderMessage, OutputLevel.Information);
        foreach (var extension in _extensionManager.TestExtensions)
        {
            _output.WriteLine(extension.Value.GetType().FullName, OutputLevel.Information);
            _output.WriteLine(string.Format(CultureInfo.CurrentCulture, CommandLineResources.AvailableExtensionsMetadataFormat, "Uri", extension.Metadata.ExtensionUri), OutputLevel.Information);
        }

        return ArgumentProcessorResult.Success;
    }
}
