// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Globalization;

using Microsoft.VisualStudio.TestPlatform.CommandLine2;
using Microsoft.VisualStudio.TestPlatform.Common.Hosting;
using Microsoft.VisualStudio.TestPlatform.Common.Interfaces;
using Microsoft.VisualStudio.TestPlatform.Common.Logging;
using Microsoft.VisualStudio.TestPlatform.Common.SettingsProvider;
using Microsoft.VisualStudio.TestPlatform.CrossPlatEngine;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;
using Microsoft.VisualStudio.TestPlatform.PlatformAbstractions.Interfaces;
using Microsoft.VisualStudio.TestPlatform.Utilities;
using Microsoft.VisualStudio.TestPlatform.Utilities.Helpers.Interfaces;

using CommandLineResources = Microsoft.VisualStudio.TestPlatform.CommandLine.Resources.Resources;

namespace Microsoft.VisualStudio.TestPlatform.CommandLine.Processors;


internal class ListSettingsProvidersArgumentProcessor : ArgumentProcessor<bool>, IExecutorCreator
{
    public ListSettingsProvidersArgumentProcessor()
        : base("--ListSettingsProviders", typeof(ListSettingsProvidersArgumentExecutor))
    {
        IsCommand = true;
        IsHiddenInHelp = true;

        CreateExecutor = c =>
        {
            var serviceProvider = c.ServiceProvider;
            var testSessionMessageLogger = TestSessionMessageLogger.Instance;
            var testhostProviderManager = new TestRuntimeProviderManager(testSessionMessageLogger);
            var testEngine = new TestEngine(testhostProviderManager, serviceProvider.GetService<IProcessHelper>(), serviceProvider.GetService<IEnvironment>());
            var testPlatform = new Client.TestPlatform(testEngine, serviceProvider.GetService<IFileHelper>(),
                testhostProviderManager, serviceProvider.GetService<IRunSettingsProvider>());
            return new ListDiscoverersArgumentExecutor(serviceProvider.GetService<IOutput>(), testPlatform);
        };
    }

    public Func<InvocationContext, IArgumentExecutor> CreateExecutor { get; }
}

internal class ListSettingsProvidersArgumentExecutor : IArgumentExecutor
{
    private readonly IOutput _output;
    private readonly ITestPlatform _testPlatform;
    private readonly SettingsProviderExtensionManager _extensionManager;
    private bool _shouldExecute;

    public ListSettingsProvidersArgumentExecutor(IOutput output, ITestPlatform testPlatform)
    {
        _output = output;
        // Test platform populates extension manager in constructor.
        _testPlatform = testPlatform;
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
