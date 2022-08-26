// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;

using Microsoft.VisualStudio.TestPlatform.Client;
using Microsoft.VisualStudio.TestPlatform.Common.ExtensionFramework;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using Microsoft.VisualStudio.TestPlatform.Utilities;

using CommandLineResources = Microsoft.VisualStudio.TestPlatform.CommandLine.Resources.Resources;

namespace Microsoft.VisualStudio.TestPlatform.CommandLine.Processors;

internal class ListLoggersArgumentProcessor : ArgumentProcessor<bool>
{
    public ListLoggersArgumentProcessor()
        : base("--ListLoggers", typeof(ListLoggersArgumentExecutor))
    {
        IsCommand = true;
        IsHiddenInHelp = true;
    }
}

internal class ListLoggersArgumentExecutor : IArgumentExecutor
{
    private readonly IOutput _output;
    private readonly TestLoggerExtensionManager _extensionManager;
    private bool _shouldExecute;

    public ListLoggersArgumentExecutor(IOutput output)
    {
        _output = output;
        // TODO: static "hidden" dependencies, with temporal dependencies
        _ = TestPlatformFactory.GetTestPlatform();
        _extensionManager = TestLoggerExtensionManager.Create(new NullMessageLogger());
    }
    public void Initialize(ParseResult parseResult)
    {
        _shouldExecute = parseResult.GetValueFor(new ListLoggersArgumentProcessor());
    }

    public ArgumentProcessorResult Execute()
    {
        if (!_shouldExecute)
        {
            return ArgumentProcessorResult.Success;
        }

        _output.WriteLine(CommandLineResources.AvailableLoggersHeaderMessage, OutputLevel.Information);
        foreach (var extension in _extensionManager.TestExtensions)
        {
            _output.WriteLine(extension.Value.GetType().FullName, OutputLevel.Information);
            _output.WriteLine(string.Format(CultureInfo.CurrentCulture, CommandLineResources.AvailableExtensionsMetadataFormat, "Uri", extension.Metadata.ExtensionUri), OutputLevel.Information);
            _output.WriteLine(string.Format(CultureInfo.CurrentCulture, CommandLineResources.AvailableExtensionsMetadataFormat, "FriendlyName", string.Join(", ", extension.Metadata.FriendlyName)), OutputLevel.Information);
        }

        return ArgumentProcessorResult.Success;
    }

    private class NullMessageLogger : IMessageLogger
    {
        public void SendMessage(TestMessageLevel testMessageLevel, string message)
        {
        }
    }
}
