// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Globalization;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestPlatform.Client.DesignMode;
using Microsoft.VisualStudio.TestPlatform.Client.RequestHelper;
using Microsoft.VisualStudio.TestPlatform.CommandLine.Processors.Utilities;
using Microsoft.VisualStudio.TestPlatform.CommandLine.Publisher;
using Microsoft.VisualStudio.TestPlatform.CommandLine.TestPlatformHelpers;
using Microsoft.VisualStudio.TestPlatform.CommandLine2;
using Microsoft.VisualStudio.TestPlatform.CommandLineUtilities;
using Microsoft.VisualStudio.TestPlatform.Common;
using Microsoft.VisualStudio.TestPlatform.Common.ExtensionFramework;
using Microsoft.VisualStudio.TestPlatform.Common.Hosting;
using Microsoft.VisualStudio.TestPlatform.Common.Interfaces;
using Microsoft.VisualStudio.TestPlatform.Common.Logging;
using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities;
using Microsoft.VisualStudio.TestPlatform.CoreUtilities.Tracing;
using Microsoft.VisualStudio.TestPlatform.CrossPlatEngine;
using Microsoft.VisualStudio.TestPlatform.CrossPlatEngine.ArtifactProcessing;
using Microsoft.VisualStudio.TestPlatform.CrossPlatEngine.TestRunAttachmentsProcessing;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.PlatformAbstractions.Interfaces;
using Microsoft.VisualStudio.TestPlatform.Utilities;
using Microsoft.VisualStudio.TestPlatform.Utilities.Helpers;
using Microsoft.VisualStudio.TestPlatform.Utilities.Helpers.Interfaces;

using CommandLineResources = Microsoft.VisualStudio.TestPlatform.CommandLine.Resources.Resources;

namespace Microsoft.VisualStudio.TestPlatform.CommandLine.Processors;

/// <summary>
/// Argument Processor for the "--Port|/Port" command line argument.
/// </summary>
internal class PortArgumentProcessor : ArgumentProcessor<int>, IExecutorCreator
{
    public PortArgumentProcessor()
        : base("--port", typeof(PortArgumentExecutor))
    {
        // REVIEW: This was not a command (action) before, but I guess we are just checking for /Port somewhere and create the processor by name.
        IsCommand = true;
        IsHiddenInHelp = true;

        Priority = ArgumentProcessorPriority.DesignMode;
        HelpContentResourceName = CommandLineResources.PortArgumentHelp;
        HelpPriority = HelpContentPriority.PortArgumentProcessorHelpPriority;

        CreateExecutor = c =>
        {
            var serviceProvider = c.ServiceProvider;
            var testPlatformEventSource = TestPlatformEventSource.Instance;
            var dataSerializer = JsonDataSerializer.Instance;
            var testSessionMessageLogger = new TestSessionMessageLogger();
            var testPluginCache = new TestPluginCache(testSessionMessageLogger);
            var runsettingsProvider = new RunSettingsManager(testSessionMessageLogger, testPluginCache);

            Client.TestPlatform.AddExtensionAssembliesFromExtensionDirectory(testPluginCache, runsettingsProvider);
            var testhostProviderManager = new TestRuntimeProviderManager(testSessionMessageLogger, testPluginCache);
            var testEngine = new TestEngine(testhostProviderManager, serviceProvider.GetService<IProcessHelper>(), serviceProvider.GetService<IEnvironment>(),
                testSessionMessageLogger, testPlatformEventSource, testPluginCache, dataSerializer, serviceProvider.GetService<IFileHelper>());
            var testPlatform = new Client.TestPlatform(testEngine, serviceProvider.GetService<IFileHelper>(),
                testhostProviderManager, runsettingsProvider, testPluginCache, dataSerializer);

            var metricsPublisher = serviceProvider.GetService<IMetricsPublisher>();
            var metricsPublisherTask = Task.FromResult(metricsPublisher);
            var testRequestManager = new TestRequestManager(
                c.ServiceProvider.GetService<CommandLineOptions>(),
                testPlatform,
                new TestRunResultAggregator(),
                testPlatformEventSource,
                new InferHelper(AssemblyMetadataProvider.Instance),
                metricsPublisherTask,
                serviceProvider.GetService<IProcessHelper>(),
                new TestRunAttachmentsProcessingManager(testPlatformEventSource, new DataCollectorAttachmentsProcessorsFactory(), testPluginCache),
                serviceProvider.GetService<IEnvironment>()
            );
            var artifactProcessingManager = new ArtifactProcessingManager(CommandLineOptions.Instance.TestSessionCorrelationId, testPlatformEventSource, testPluginCache, serviceProvider.GetService<IOutput>());

            var designModeClient = new DesignModeClient(new SocketCommunicationManager(), dataSerializer, serviceProvider.GetService<IEnvironment>(), testSessionMessageLogger);
            // TODO: Replace those resolves by shipping the instances on the invocation context directly.

            var sharedDependencies = serviceProvider.GetService<SharedDependencyDictionary>();
            // TODO: Maybe rather add directly this, because that is what we need in in-process console wrapper?
            // sharedDependencies[typeof(TestRequestManager)] = new WeakReference(testRequestManager);
            sharedDependencies[typeof(DesignModeClient)] = new WeakReference(designModeClient);
            return new PortArgumentExecutor(
                c.ServiceProvider.GetService<CommandLineOptions>(),
                    testRequestManager,
                   c.ServiceProvider.GetService<IProcessHelper>(),
                   designModeClient,
                   c.ServiceProvider.GetService<IRunSettingsHelper>());
        };
    }

    public Func<InvocationContext, IArgumentExecutor> CreateExecutor { get; }
}

/// <summary>
/// Argument Executor for the "/Port" command line argument.
/// </summary>
internal class PortArgumentExecutor : IArgumentExecutor
{
    /// <summary>
    /// Used for getting sources.
    /// </summary>
    private readonly CommandLineOptions _commandLineOptions;

    /// <summary>
    /// Test Request Manager
    /// </summary>
    private readonly ITestRequestManager _testRequestManager;

    /// <summary>
    /// Initializes Design mode when called
    /// </summary>
    private readonly Func<IDesignModeClient?, int, IProcessHelper, IDesignModeClient> _initializeDesignMode;

    /// <summary>
    /// IDesignModeClient
    /// </summary>
    private IDesignModeClient? _designModeClient;
    private readonly IRunSettingsHelper _runSettingsHelper;

    /// <summary>
    /// Process helper for process management actions.
    /// </summary>
    private readonly IProcessHelper _processHelper;

    // REVIEW: this has initialize design mode callback, I guess that is to prevent startup during unit tests
    internal PortArgumentExecutor(CommandLineOptions options, ITestRequestManager testRequestManager, IProcessHelper processHelper, IDesignModeClient designModeClient, IRunSettingsHelper runSettingsHelper)
        : this(options, testRequestManager, InitializeDesignMode, processHelper, designModeClient, runSettingsHelper)
    {
    }

    internal PortArgumentExecutor(CommandLineOptions options, ITestRequestManager testRequestManager, Func<IDesignModeClient?, int, IProcessHelper, IDesignModeClient> designModeInitializer, IProcessHelper processHelper, IDesignModeClient designModeClient, IRunSettingsHelper runSettingsHelper)
    {
        ValidateArg.NotNull(options, nameof(options));
        _commandLineOptions = options;
        _testRequestManager = testRequestManager;
        _initializeDesignMode = designModeInitializer;
        _processHelper = processHelper;
        _designModeClient = designModeClient;
        _runSettingsHelper = runSettingsHelper;
    }


    #region IArgumentExecutor

    /// <summary>
    /// Initializes with the argument that was provided with the command.
    /// </summary>
    /// <param name="argument">Argument that was provided with the command.</param>
    public void Initialize(ParseResult parseResult)
    {
        var portNumber = parseResult.GetValueFor(new PortArgumentProcessor());

        _commandLineOptions.Port = portNumber;
        _commandLineOptions.IsDesignMode = true;
        _runSettingsHelper.IsDesignMode = true;
        _designModeClient = _initializeDesignMode?.Invoke(_designModeClient, _commandLineOptions.ParentProcessId, _processHelper);
    }

    /// <summary>
    /// Initialize the design mode client.
    /// </summary>
    /// <returns> The <see cref="ArgumentProcessorResult.Success"/> if initialization is successful. </returns>
    public ArgumentProcessorResult Execute()
    {
        try
        {
            _designModeClient?.ConnectToClientAndProcessRequests(_commandLineOptions.Port, _testRequestManager);
        }
        catch (TimeoutException ex)
        {
            throw new CommandLineException(string.Format(CultureInfo.CurrentCulture, CommandLineResources.DesignModeClientTimeoutError, _commandLineOptions.Port), ex);
        }

        return ArgumentProcessorResult.Success;
    }

    #endregion

    private static IDesignModeClient InitializeDesignMode(IDesignModeClient? designModeClient, int parentProcessId, IProcessHelper processHelper)
    {
        if (designModeClient == null)
        {
            // Throw when we provide the default initialization but don't provide the designModeClient instance.
            // This should happen only in tests, because the init func is here for tests only.
            throw new ArgumentNullException(nameof(designModeClient));
        }

        if (parentProcessId > 0)
        {
            processHelper.SetExitCallback(parentProcessId, (obj) =>
            {
                EqtTrace.Info($"PortArgumentProcessor: parent process:{parentProcessId} exited.");
                designModeClient.HandleParentProcessExit();
            });
        }

        return designModeClient;
    }
}
