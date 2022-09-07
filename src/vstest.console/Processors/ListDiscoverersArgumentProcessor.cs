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

internal class ListDiscoverersArgumentProcessor : ArgumentProcessor<bool>, IExecutorCreator
{
    public ListDiscoverersArgumentProcessor()
        : base("--ListDiscoverers", typeof(ListDiscoverersArgumentExecutor))
    {
        IsCommand = true;
        IsHiddenInHelp = true;
    }
    public Func<InvocationContext, IArgumentExecutor> CreateExecutor { get; } =
        c => new ListDiscoverersArgumentExecutor(ConsoleOutput.Instance, new Client.TestPlatform());
}

internal class ListDiscoverersArgumentExecutor : IArgumentExecutor
{
    private readonly IOutput _output;
    private readonly ITestPlatform _testPlatform;
    private readonly TestDiscoveryExtensionManager _extensionManager;
    private bool _shouldExecute;

    public ListDiscoverersArgumentExecutor(IOutput output, ITestPlatform testPlatform)
    {
        _output = output;
        // Test platform populates extension manager in constructor.
        _testPlatform = testPlatform;
        _extensionManager = TestDiscoveryExtensionManager.Create();
    }

    public void Initialize(ParseResult parseResult)
    {
        _shouldExecute = parseResult.GetValueFor(new ListDiscoverersArgumentProcessor());
    }

    public ArgumentProcessorResult Execute()
    {
        if (!_shouldExecute)
        {
            return ArgumentProcessorResult.Success;
        }

        _output.WriteLine(CommandLineResources.AvailableDiscoverersHeaderMessage, OutputLevel.Information);
        foreach (var extension in _extensionManager.Discoverers)
        {
            _output.WriteLine(extension.Value.GetType().FullName, OutputLevel.Information);
            _output.WriteLine(string.Format(CultureInfo.CurrentCulture, CommandLineResources.UriOfDefaultExecutor, extension.Metadata.DefaultExecutorUri), OutputLevel.Information);
            _output.WriteLine(string.Format(CultureInfo.CurrentCulture, CommandLineResources.SupportedFileTypesIndicator + " " + string.Join(", ", extension.Metadata.FileExtension!)), OutputLevel.Information);
        }

        return ArgumentProcessorResult.Success;
    }
}
