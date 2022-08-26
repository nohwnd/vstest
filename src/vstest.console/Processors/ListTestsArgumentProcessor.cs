// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Linq;

using Microsoft.VisualStudio.TestPlatform.Client.RequestHelper;
using Microsoft.VisualStudio.TestPlatform.CommandLine.Internal;
using Microsoft.VisualStudio.TestPlatform.Common.Interfaces;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;
using Microsoft.VisualStudio.TestPlatform.Utilities;

using CommandLineResources = Microsoft.VisualStudio.TestPlatform.CommandLine.Resources.Resources;

namespace Microsoft.VisualStudio.TestPlatform.CommandLine.Processors;

internal class ListTestsArgumentProcessor : ArgumentProcessor<bool>
{
    public ListTestsArgumentProcessor()
        : base(new string[] { "-t", "-lt", "--listtests", "--list-tests" }, typeof(ListTestsArgumentExecutor))
    {
        IsCommand = true;
        HelpContentResourceName = CommandLineResources.ListTestsHelp;
        HelpPriority = HelpContentPriority.ListTestsArgumentProcessorHelpPriority;
    }
}

internal class ListTestsArgumentExecutor : IArgumentExecutor
{
    /// <summary>
    /// Used for getting sources.
    /// </summary>
    private readonly CommandLineOptions _commandLineOptions;

    /// <summary>
    /// Used for getting tests.
    /// </summary>
    private readonly ITestRequestManager _testRequestManager;

    /// <summary>
    /// Used for sending output.
    /// </summary>
    internal IOutput Output;
    private bool _shouldExecute;

    /// <summary>
    /// RunSettingsManager to get currently active run settings.
    /// </summary>
    private readonly IRunSettingsProvider _runSettingsManager;

    /// <summary>
    /// Registers for discovery events during discovery
    /// </summary>
    private readonly ITestDiscoveryEventsRegistrar _discoveryEventsRegistrar;

    public ListTestsArgumentExecutor(
        CommandLineOptions options,
        IRunSettingsProvider runSettingsProvider,
        ITestRequestManager testRequestManager,
        IOutput output)
    {
        ValidateArg.NotNull(options, nameof(options));

        _commandLineOptions = options;
        Output = output;
        _testRequestManager = testRequestManager;

        _runSettingsManager = runSettingsProvider;
        _discoveryEventsRegistrar = new DiscoveryEventsRegistrar(output);
    }

    public void Initialize(ParseResult parseResult)
    {
        _shouldExecute = parseResult.GetValueFor(new ListTestsArgumentProcessor());
    }

    /// <summary>
    /// Lists out the available discoverers.
    /// </summary>
    public ArgumentProcessorResult Execute()
    {
        if (!_shouldExecute)
        {
            return ArgumentProcessorResult.Success;
        }

        TPDebug.Assert(Output != null);
        TPDebug.Assert(_commandLineOptions != null);
        TPDebug.Assert(!StringUtils.IsNullOrWhiteSpace(_runSettingsManager?.ActiveRunSettings?.SettingsXml));

        if (!_commandLineOptions.Sources.Any())
        {
            throw new CommandLineException(CommandLineResources.MissingTestSourceFile);
        }

        Output.WriteLine(CommandLineResources.ListTestsHeaderMessage, OutputLevel.Information);
        if (!StringUtils.IsNullOrEmpty(EqtTrace.LogFile))
        {
            Output.Information(false, CommandLineResources.VstestDiagLogOutputPath, EqtTrace.LogFile);
        }

        var runSettings = _runSettingsManager.ActiveRunSettings.SettingsXml;

        _testRequestManager.DiscoverTests(
            new DiscoveryRequestPayload() { Sources = _commandLineOptions.Sources, RunSettings = runSettings },
            _discoveryEventsRegistrar, Constants.DefaultProtocolConfig);

        return ArgumentProcessorResult.Success;
    }

    private class DiscoveryEventsRegistrar : ITestDiscoveryEventsRegistrar
    {
        private readonly IOutput _output;

        public DiscoveryEventsRegistrar(IOutput output)
        {
            _output = output;
        }

        public void LogWarning(string message)
        {
            ConsoleLogger.RaiseTestRunWarning(message);
        }

        public void RegisterDiscoveryEvents(IDiscoveryRequest discoveryRequest)
        {
            discoveryRequest.OnDiscoveredTests += DiscoveryRequest_OnDiscoveredTests;
        }

        public void UnregisterDiscoveryEvents(IDiscoveryRequest discoveryRequest)
        {
            discoveryRequest.OnDiscoveredTests -= DiscoveryRequest_OnDiscoveredTests;
        }

        private void DiscoveryRequest_OnDiscoveredTests(object? sender, DiscoveredTestsEventArgs args)
        {
            // List out each of the tests.
            foreach (var test in args.DiscoveredTestCases!)
            {
                _output.WriteLine(
                    string.Format(CultureInfo.CurrentCulture, CommandLineResources.AvailableTestsFormat, test.DisplayName),
                    OutputLevel.Information);
            }
        }
    }
}
