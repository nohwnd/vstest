// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;

using Microsoft.VisualStudio.TestPlatform.Client;
using Microsoft.VisualStudio.TestPlatform.Common.ExtensionFramework;
using Microsoft.VisualStudio.TestPlatform.Utilities;

using CommandLineResources = Microsoft.VisualStudio.TestPlatform.CommandLine.Resources.Resources;

namespace Microsoft.VisualStudio.TestPlatform.CommandLine.Processors;

internal class ListExecutorsArgumentProcessor : ArgumentProcessor<bool>
{
    public ListExecutorsArgumentProcessor()
        : base("--ListExecutors", typeof(ListExecutorsArgumentExecutor))
    {
        IsCommand = true;
        IsHiddenInHelp = true;
    }
}


internal class ListExecutorsArgumentExecutor : IArgumentExecutor
{
    private readonly IOutput _output;
    private readonly TestExecutorExtensionManager _extensionManager;

    public ListExecutorsArgumentExecutor(IOutput output)
    {
        _output = output;
        // TODO: static "hidden" dependencies, with temporal dependencies
        _ = TestPlatformFactory.GetTestPlatform();
        _extensionManager = TestExecutorExtensionManager.Create();
    }

    public void Initialize(string? argument)
    {
    }

    public ArgumentProcessorResult Execute()
    {
        _output.WriteLine(CommandLineResources.AvailableExecutorsHeaderMessage, OutputLevel.Information);
        _ = TestPlatformFactory.GetTestPlatform();
        foreach (var extension in _extensionManager.TestExtensions)
        {
            _output.WriteLine(extension.Value.GetType().FullName, OutputLevel.Information);
            _output.WriteLine(string.Format(CultureInfo.CurrentCulture, CommandLineResources.AvailableExtensionsMetadataFormat, "Uri", extension.Metadata.ExtensionUri), OutputLevel.Information);
        }

        return ArgumentProcessorResult.Success;
    }
}
