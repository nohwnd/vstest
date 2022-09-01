// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestPlatform.Client;
using Microsoft.VisualStudio.TestPlatform.Client.RequestHelper;
using Microsoft.VisualStudio.TestPlatform.CommandLine.Internal;
using Microsoft.VisualStudio.TestPlatform.CommandLine.Processors.Utilities;
using Microsoft.VisualStudio.TestPlatform.CommandLine.Publisher;
using Microsoft.VisualStudio.TestPlatform.CommandLine.TestPlatformHelpers;
using Microsoft.VisualStudio.TestPlatform.CommandLine2;
using Microsoft.VisualStudio.TestPlatform.CommandLineUtilities;
using Microsoft.VisualStudio.TestPlatform.Common;
using Microsoft.VisualStudio.TestPlatform.Common.Hosting;
using Microsoft.VisualStudio.TestPlatform.Common.Interfaces;
using Microsoft.VisualStudio.TestPlatform.Common.Logging;
using Microsoft.VisualStudio.TestPlatform.Common.Utilities;
using Microsoft.VisualStudio.TestPlatform.CoreUtilities.Tracing;
using Microsoft.VisualStudio.TestPlatform.CrossPlatEngine;
using Microsoft.VisualStudio.TestPlatform.CrossPlatEngine.ArtifactProcessing;
using Microsoft.VisualStudio.TestPlatform.CrossPlatEngine.TestRunAttachmentsProcessing;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Engine;
using Microsoft.VisualStudio.TestPlatform.PlatformAbstractions.Interfaces;
using Microsoft.VisualStudio.TestPlatform.Utilities;
using Microsoft.VisualStudio.TestPlatform.Utilities.Helpers.Interfaces;

using CommandLineResources = Microsoft.VisualStudio.TestPlatform.CommandLine.Resources.Resources;
using ObjectModelConstants = Microsoft.VisualStudio.TestPlatform.ObjectModel.Constants;

namespace Microsoft.VisualStudio.TestPlatform.CommandLine.Processors;

internal class RunTestsArgumentProcessor : ArgumentProcessor<bool>, IExecutorCreator
{
    public RunTestsArgumentProcessor()
        : base("--RunTests", typeof(RunTestsArgumentExecutor))
    {

        IsCommand = true;
        IsHidden = true;
        // This is the last command that everything falls into
        // when there is no other command before it.
        AlwaysExecute = true;

        HelpContentResourceName = CommandLineResources.RunTestsArgumentHelp;
        HelpPriority = HelpContentPriority.RunTestsArgumentProcessorHelpPriority;

        CreateExecutor = c =>
        {
            var serviceProvider = c.ServiceProvider;
            var testSessionMessageLogger = TestSessionMessageLogger.Instance;
            var testhostProviderManager = new TestRuntimeProviderManager(testSessionMessageLogger);
            var testEngine = new TestEngine(testhostProviderManager, serviceProvider.GetService<IProcessHelper>(), serviceProvider.GetService<IEnvironment>());
            var testPlatform = new Client.TestPlatform(testEngine, serviceProvider.GetService<IFileHelper>(),
                testhostProviderManager, serviceProvider.GetService<IRunSettingsProvider>());
            var testPlatformEventSource = TestPlatformEventSource.Instance;
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
            new TestRunAttachmentsProcessingManager(testPlatformEventSource, new DataCollectorAttachmentsProcessorsFactory()),
            serviceProvider.GetService<IEnvironment>()
            );
            var artifactProcessingManager = new ArtifactProcessingManager(CommandLineOptions.Instance.TestSessionCorrelationId);
            // TODO: Replace those resolves by shipping the instances on the invocation context directly,
            // so we don't get strayed into trying to grab unavailable services from the provider,
            // or register more services than what is the "evironment" surrounding the run (e.g. consoleOptions
            // or environmentHelper, or runsettings, but we should not register TestPlatform or TestRequestManager).
            return new RunTestsArgumentExecutor(
                c.ServiceProvider.GetService<CommandLineOptions>(),
                    c.ServiceProvider.GetService<IRunSettingsProvider>()!,
                    testRequestManager,
                    artifactProcessingManager,
                    c.ServiceProvider.GetService<IOutput>()!);
        };
    }

    public Func<InvocationContext, IArgumentExecutor> CreateExecutor { get; }
}

internal interface IExecutorCreator
{
    Func<InvocationContext, IArgumentExecutor> CreateExecutor { get; }
}

internal class RunTestsArgumentExecutor : IArgumentExecutor
{
    /// <summary>
    /// Used for getting tests to run.
    /// </summary>
    private readonly CommandLineOptions _commandLineOptions;

    /// <summary>
    /// The instance of testPlatforms
    /// </summary>
    private readonly ITestRequestManager _testRequestManager;

    /// <summary>
    /// Used for sending discovery messages.
    /// </summary>
    internal IOutput Output;

    /// <summary>
    /// Settings manager to get currently active settings.
    /// </summary>
    private readonly IRunSettingsProvider _runSettingsManager;

    /// <summary>
    /// Registers and Unregisters for test run events before and after test run
    /// </summary>
    private readonly ITestRunEventsRegistrar _testRunEventsRegistrar;
    private bool _shouldExecute;

    /// <summary>
    /// Shows the number of tests which were executed
    /// </summary>
    private static long s_numberOfExecutedTests;

    /// <summary>
    /// Default constructor.
    /// </summary>
    public RunTestsArgumentExecutor(
        CommandLineOptions commandLineOptions,
        IRunSettingsProvider runSettingsProvider,
        ITestRequestManager testRequestManager,
        IArtifactProcessingManager artifactProcessingManager,
        IOutput output)
    {
        ValidateArg.NotNull(commandLineOptions, nameof(commandLineOptions));

        _commandLineOptions = commandLineOptions;
        _runSettingsManager = runSettingsProvider;
        _testRequestManager = testRequestManager;
        Output = output;
        _testRunEventsRegistrar = new TestRunRequestEventsRegistrar(Output, _commandLineOptions, artifactProcessingManager, runSettingsProvider);
    }

    public void Initialize(ParseResult parseResult)
    {
        if (parseResult.TryGetValueFor(new RunTestsArgumentProcessor(), out var runTests) && runTests == false)
        {
            // User explicitly specified false, we should not run.
            _shouldExecute = false;
        }
        else
        {
            // User specified nothing or specified true, we should run.
            _shouldExecute = true;
        }

    }

    /// <summary>
    /// Execute all of the tests.
    /// </summary>
    public ArgumentProcessorResult Execute()
    {
        if (!_shouldExecute)
        {
            return ArgumentProcessorResult.Success;
        }

        TPDebug.Assert(_commandLineOptions != null);
        TPDebug.Assert(!StringUtils.IsNullOrWhiteSpace(_runSettingsManager?.ActiveRunSettings?.SettingsXml));

        if (_commandLineOptions.IsDesignMode)
        {
            // Do not attempt execution in case of design mode. Expect execution to happen via the design mode client.
            return ArgumentProcessorResult.Success;
        }

        // Ensure a test source file was provided
        var anySource = _commandLineOptions.Sources.FirstOrDefault();
        if (anySource == null)
        {
            throw new CommandLineException(CommandLineResources.MissingTestSourceFile);
        }

        Output.WriteLine(CommandLineResources.StartingExecution, OutputLevel.Information);
        if (!StringUtils.IsNullOrEmpty(EqtTrace.LogFile))
        {
            Output.Information(false, CommandLineResources.VstestDiagLogOutputPath, EqtTrace.LogFile);
        }

        var runSettings = _runSettingsManager.ActiveRunSettings.SettingsXml;

        if (_commandLineOptions.Sources.Any())
        {
            RunTests(runSettings);
        }

        bool treatNoTestsAsError = RunSettingsUtilities.GetTreatNoTestsAsError(runSettings);

        return treatNoTestsAsError && s_numberOfExecutedTests == 0 ? ArgumentProcessorResult.Fail : ArgumentProcessorResult.Success;
    }

    private void RunTests(string runSettings)
    {
        // create/start test run
        EqtTrace.Info("RunTestsArgumentProcessor:Execute: Test run is starting.");
        EqtTrace.Verbose("RunTestsArgumentProcessor:Execute: Queuing Test run.");

        // for command line keep alive is always false.
        // for Windows Store apps it should be false, as Windows Store apps executor should terminate after finishing the test execution.
        var keepAlive = false;

        var runRequestPayload = new TestRunRequestPayload() { Sources = _commandLineOptions.Sources.ToList(), RunSettings = runSettings, KeepAlive = keepAlive, TestPlatformOptions = new TestPlatformOptions() { TestCaseFilter = _commandLineOptions.TestCaseFilterValue } };
        _testRequestManager.RunTests(runRequestPayload, null, _testRunEventsRegistrar, ObjectModelConstants.DefaultProtocolConfig);

        EqtTrace.Info("RunTestsArgumentProcessor:Execute: Test run is completed.");
    }

    private class TestRunRequestEventsRegistrar : ITestRunEventsRegistrar
    {
        private readonly IOutput _output;
        private readonly CommandLineOptions _commandLineOptions;
        private readonly IArtifactProcessingManager _artifactProcessingManager;
        private readonly IRunSettingsProvider _runsettingsProvider;

        public TestRunRequestEventsRegistrar(IOutput output, CommandLineOptions commandLineOptions, IArtifactProcessingManager artifactProcessingManager, IRunSettingsProvider runsettingsProvider)
        {
            _output = output;
            _commandLineOptions = commandLineOptions;
            _artifactProcessingManager = artifactProcessingManager;
            _runsettingsProvider = runsettingsProvider;
        }

        public void LogWarning(string message)
        {
            ConsoleLogger.RaiseTestRunWarning(message);
        }

        public void RegisterTestRunEvents(ITestRunRequest testRunRequest)
        {
            testRunRequest.OnRunCompletion += TestRunRequest_OnRunCompletion;
        }

        public void UnregisterTestRunEvents(ITestRunRequest testRunRequest)
        {
            testRunRequest.OnRunCompletion -= TestRunRequest_OnRunCompletion;
        }

        /// <summary>
        /// Handles the TestRunRequest complete event
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e">RunCompletion args</param>
        private void TestRunRequest_OnRunCompletion(object? sender, TestRunCompleteEventArgs e)
        {
            // If run is not aborted/canceled then check the count of executed tests.
            // we need to check if there are any tests executed - to try show some help info to user to check for installed vsix extensions
            if (!e.IsAborted && !e.IsCanceled)
            {
                var testsFoundInAnySource = e.TestRunStatistics != null && e.TestRunStatistics.ExecutedTests > 0;
                s_numberOfExecutedTests = e.TestRunStatistics!.ExecutedTests;

                // Indicate the user to use test adapter path command if there are no tests found
                if (!testsFoundInAnySource && !_commandLineOptions.TestAdapterPathsSet && _commandLineOptions.TestCaseFilterValue == null)
                {
                    _output.Warning(false, CommandLineResources.SuggestTestAdapterPathIfNoTestsIsFound);
                }
            }

            // Collect tests session artifacts for post processing
            if (_commandLineOptions.ArtifactProcessingMode == ArtifactProcessingMode.Collect)
            {
                TPDebug.Assert(_runsettingsProvider.ActiveRunSettings is not null, "_runsettingsProvider.ActiveRunSettings is null");
                TPDebug.Assert(_runsettingsProvider.ActiveRunSettings.SettingsXml is not null, "_runsettingsProvider.ActiveRunSettings.SettingsXml is null");
                _artifactProcessingManager.CollectArtifacts(e, _runsettingsProvider.ActiveRunSettings.SettingsXml);
            }
        }
    }
}
