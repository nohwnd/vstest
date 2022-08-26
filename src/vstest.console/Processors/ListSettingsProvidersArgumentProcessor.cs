// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;

using Microsoft.VisualStudio.TestPlatform.Client;
using Microsoft.VisualStudio.TestPlatform.Common.SettingsProvider;
using Microsoft.VisualStudio.TestPlatform.Utilities;

using CommandLineResources = Microsoft.VisualStudio.TestPlatform.CommandLine.Resources.Resources;

namespace Microsoft.VisualStudio.TestPlatform.CommandLine.Processors;


internal class ListSettingsProvidersArgumentProcessor : ArgumentProcessor<bool>
{
    public ListSettingsProvidersArgumentProcessor()
        : base("--ListSettingsProviders", typeof(ListSettingsProvidersArgumentExecutor))
    {
        IsCommand = true;
        IsHiddenInHelp = true;
    }
}

internal class ListSettingsProvidersArgumentExecutor : IArgumentExecutor
{
    private readonly IOutput _output;
    private readonly SettingsProviderExtensionManager _extensionManager;
    private bool _shouldExecute;

    public ListSettingsProvidersArgumentExecutor(IOutput output)
    {
        _output = output;
        // TODO: static "hidden" dependencies, with temporal dependencies
        _ = TestPlatformFactory.GetTestPlatform();
        _extensionManager = SettingsProviderExtensionManager.Create();
    }
    public void Initialize(ParseResult parseResult)
    {
        _shouldExecute = parseResult.GetValueFor(new ListSettingsProvidersArgumentProcessor());
    }

    public ArgumentProcessorResult Execute()
    {
        if (!_shouldExecute)
        {
            return ArgumentProcessorResult.Success;
        }

        _output.WriteLine(CommandLineResources.AvailableSettingsProvidersHeaderMessage, OutputLevel.Information);
        foreach (var extension in _extensionManager.SettingsProvidersMap.Values)
        {
            _output.WriteLine(extension.Value.GetType().FullName, OutputLevel.Information);
            _output.WriteLine(string.Format(CultureInfo.CurrentCulture, CommandLineResources.AvailableExtensionsMetadataFormat, "SettingName", extension.Metadata.SettingsName), OutputLevel.Information);
        }

        return ArgumentProcessorResult.Success;
    }
}
