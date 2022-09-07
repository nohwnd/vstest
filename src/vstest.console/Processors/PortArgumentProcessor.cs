// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Globalization;

using Microsoft.VisualStudio.TestPlatform.Client.DesignMode;
using Microsoft.VisualStudio.TestPlatform.Client.RequestHelper;
using Microsoft.VisualStudio.TestPlatform.CommandLine.TestPlatformHelpers;
using Microsoft.VisualStudio.TestPlatform.CommandLine2;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.PlatformAbstractions;
using Microsoft.VisualStudio.TestPlatform.PlatformAbstractions.Interfaces;
using Microsoft.VisualStudio.TestPlatform.Utilities.Helpers;

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
            new PortArgumentExecutor(CommandLineOptions.Instance, TestRequestManager.Instance);
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
    private readonly Func<int, IProcessHelper, IDesignModeClient> _designModeInitializer;

    /// <summary>
    /// IDesignModeClient
    /// </summary>
    private IDesignModeClient? _designModeClient;

    /// <summary>
    /// Process helper for process management actions.
    /// </summary>
    private readonly IProcessHelper _processHelper;

    /// <summary>
    /// Default constructor.
    /// </summary>
    /// <param name="options">
    /// The options.
    /// </param>
    /// <param name="testRequestManager"> Test request manager</param>
    public PortArgumentExecutor(CommandLineOptions options, ITestRequestManager testRequestManager)
        : this(options, testRequestManager, InitializeDesignMode, new ProcessHelper())
    {
    }

    /// <summary>
    /// For Unit testing only
    /// </summary>
    internal PortArgumentExecutor(CommandLineOptions options, ITestRequestManager testRequestManager, IProcessHelper processHelper)
        : this(options, testRequestManager, InitializeDesignMode, processHelper)
    {
    }

    /// <summary>
    /// For Unit testing only
    /// </summary>
    internal PortArgumentExecutor(CommandLineOptions options, ITestRequestManager testRequestManager, Func<int, IProcessHelper, IDesignModeClient> designModeInitializer, IProcessHelper processHelper)
    {
        ValidateArg.NotNull(options, nameof(options));
        _commandLineOptions = options;
        _testRequestManager = testRequestManager;
        _designModeInitializer = designModeInitializer;
        _processHelper = processHelper;
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
        RunSettingsHelper.Instance.IsDesignMode = true;
        _designModeClient = _designModeInitializer?.Invoke(_commandLineOptions.ParentProcessId, _processHelper);
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

    private static IDesignModeClient InitializeDesignMode(int parentProcessId, IProcessHelper processHelper)
    {
        if (parentProcessId > 0)
        {
            processHelper.SetExitCallback(parentProcessId, (obj) =>
            {
                EqtTrace.Info($"PortArgumentProcessor: parent process:{parentProcessId} exited.");
                DesignModeClient.Instance?.HandleParentProcessExit();
            });
        }

        DesignModeClient.Initialize();
        return DesignModeClient.Instance;
    }
}
