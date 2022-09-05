// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

using Microsoft.VisualStudio.TestPlatform.Common;
using Microsoft.VisualStudio.TestPlatform.Common.ExtensionFramework;
using Microsoft.VisualStudio.TestPlatform.Common.Logging;
using Microsoft.VisualStudio.TestPlatform.Common.Telemetry;
using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities.Interfaces;
using Microsoft.VisualStudio.TestPlatform.CoreUtilities.Tracing.Interfaces;
using Microsoft.VisualStudio.TestPlatform.CrossPlatEngine.Discovery;
using Microsoft.VisualStudio.TestPlatform.CrossPlatEngine.Execution;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Engine.TesthostProtocol;

namespace Microsoft.VisualStudio.TestPlatform.CrossPlatEngine;

/// <summary>
/// The factory that provides discovery and execution managers to the test host.
/// </summary>
public class TestHostManagerFactory : ITestHostManagerFactory
{
    private IDiscoveryManager? _discoveryManager;
    private IExecutionManager? _executionManager;
    private readonly bool _telemetryOptedIn;
    private readonly ITestPlatformEventSource _testPlatformEventSource;
    private readonly TestSessionMessageLogger _sessionMessageLogger;
    private readonly TestPluginCache _testPluginCache;
    private readonly IDataSerializer _dataSerializer;

    /// <summary>
    /// Initializes a new instance of the <see cref="TestHostManagerFactory"/> class.
    /// </summary>
    ///
    /// <param name="telemetryOptedIn">
    /// A value indicating if the telemetry is opted in or not.
    /// </param>
    public TestHostManagerFactory(bool telemetryOptedIn, ITestPlatformEventSource testPlatformEventSource, TestSessionMessageLogger sessionMessageLogger, TestPluginCache testPluginCache, IDataSerializer dataSerializer)
    {
        _telemetryOptedIn = telemetryOptedIn;
        _testPlatformEventSource = testPlatformEventSource;
        _sessionMessageLogger = sessionMessageLogger;
        _testPluginCache = testPluginCache;
        _dataSerializer = dataSerializer;
    }

    /// <summary>
    /// The discovery manager instance for any discovery related operations inside of the test host.
    /// </summary>
    /// <returns>The discovery manager.</returns>
    [Obsolete("This is basically .Instance, but hidden.")]
    public IDiscoveryManager GetDiscoveryManager()
        => _discoveryManager ??= new DiscoveryManager(GetRequestData(_telemetryOptedIn), _testPlatformEventSource, _sessionMessageLogger, _testPluginCache);

    /// <summary>
    /// The execution manager instance for any discovery related operations inside of the test host.
    /// </summary>
    /// <returns>The execution manager.</returns>
    [Obsolete("This is basically .Instance, but hidden.")]
    public IExecutionManager GetExecutionManager()
        => _executionManager ??= new ExecutionManager(GetRequestData(_telemetryOptedIn), _testPlatformEventSource, _testPluginCache, _sessionMessageLogger, _dataSerializer);

    private static RequestData GetRequestData(bool telemetryOptedIn)
    {
        // REVIEW: this should probably move up, and be provided as dependency.
        return new RequestData
        {
            MetricsCollection =
                telemetryOptedIn
                    ? new MetricsCollection()
                    : new NoOpMetricsCollection(),
            IsTelemetryOptedIn = telemetryOptedIn
        };
    }
}
