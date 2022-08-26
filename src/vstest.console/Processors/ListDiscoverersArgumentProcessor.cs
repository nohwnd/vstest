// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;

using Microsoft.VisualStudio.TestPlatform.Client;
using Microsoft.VisualStudio.TestPlatform.CommandLine2;
using Microsoft.VisualStudio.TestPlatform.Common.ExtensionFramework;
using Microsoft.VisualStudio.TestPlatform.Utilities;

using CommandLineResources = Microsoft.VisualStudio.TestPlatform.CommandLine.Resources.Resources;

namespace Microsoft.VisualStudio.TestPlatform.CommandLine.Processors;

internal class ListDiscoverersArgumentProcessor : ArgumentProcessor<bool>
{
    public ListDiscoverersArgumentProcessor()
        : base("--ListDiscoverers", typeof(ListDiscoverersArgumentExecutor))
    {
        IsCommand = true;
        IsHiddenInHelp = true;
    }
}

internal class ListDiscoverersArgumentExecutor : IArgumentExecutor
{
    private readonly IOutput _output;
    private readonly TestDiscoveryExtensionManager _extensionManager;
    private bool _shouldExecute;

    public ListDiscoverersArgumentExecutor(IOutput output)
    {
        _output = output;
        // TODO: static "hidden" dependencies, with temporal dependencies
        _ = TestPlatformFactory.GetTestPlatform();
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
