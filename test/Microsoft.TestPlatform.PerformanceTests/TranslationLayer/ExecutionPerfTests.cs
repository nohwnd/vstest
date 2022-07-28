﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Globalization;

using Microsoft.TestPlatform.PerformanceTests.PerfInstrumentation;
using Microsoft.VisualStudio.TestPlatform.Common.Telemetry;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.TestPlatform.PerformanceTests.TranslationLayer;

[TestClass]
public class ExecutionPerfTests : TelemetryPerfTestBase
{
    [TestMethod]
    [TestCategory("TelemetryPerf")]
    [DataRow("MSTest1Passing", 1)]
    [DataRow("MSTest100Passing", 100)]
    [DataRow("MSTest1000Passing", 1000)]
    [DataRow("MSTest10kPassing", 10_000)]
    [DataRow("NUnit1Passing", 1)]
    [DataRow("NUnit100Passing", 100)]
    [DataRow("NUnit1000Passing", 1000)]
    [DataRow("NUnit10kPassing", 10_000)]
    [DataRow("XUnit1Passing", 1)]
    [DataRow("XUnit100Passing", 100)]
    [DataRow("XUnit1000Passing", 1000)]
    [DataRow("XUnit10kPassing", 10_000)]
    [DataRow("Perfy.TestAdapter", 1)]
    [DataRow("Perfy.TestAdapter", 100)]
    [DataRow("Perfy.TestAdapter", 1000)]
    [DataRow("Perfy.TestAdapter", 10_000)]
    public void RunTests(string projectName, double expectedNumberOfTests)
    {
        var runEventHandler = new RunEventHandler();
        TestPlatformOptions options = new() { CollectMetrics = true };

        var perfAnalyzer = new PerfAnalyzer();
        using (perfAnalyzer.Start())
        {
            // This tells to PerfyTestAdapter how many tests it should return, this is our overhead baseline.
            var perfyTestAdapterEnv = new Dictionary<string, string?> { ["TEST_COUNT"] = expectedNumberOfTests.ToString(CultureInfo.InvariantCulture) };
            var vstestConsoleWrapper = GetVsTestConsoleWrapper(perfyTestAdapterEnv, traceLevel: System.Diagnostics.TraceLevel.Off);
            vstestConsoleWrapper.RunTests(GetPerfAssetFullPath(projectName), GetDefaultRunSettings(), options, runEventHandler);
            vstestConsoleWrapper.EndSession();
        }

        Assert.AreEqual(expectedNumberOfTests, runEventHandler.Metrics![TelemetryDataConstants.TotalTestsRun]);
        PostTelemetry(runEventHandler.Metrics, perfAnalyzer, projectName);
    }

    [TestMethod]
    [TestCategory("TelemetryPerf")]
    [DataRow("MSTest1Passing", 1)]
    [DataRow("MSTest100Passing", 100)]
    [DataRow("MSTest1000Passing", 1000)]
    [DataRow("MSTest10kPassing", 10_000)]
    [DataRow("NUnit1Passing", 1)]
    [DataRow("NUnit100Passing", 100)]
    [DataRow("NUnit1000Passing", 1000)]
    [DataRow("NUnit10kPassing", 10_000)]
    [DataRow("XUnit1Passing", 1)]
    [DataRow("XUnit100Passing", 100)]
    [DataRow("XUnit1000Passing", 1000)]
    [DataRow("XUnit10kPassing", 10_000)]
    [DataRow("Perfy.TestAdapter", 1)]
    [DataRow("Perfy.TestAdapter", 100)]
    [DataRow("Perfy.TestAdapter", 1000)]
    [DataRow("Perfy.TestAdapter", 10_000)]
    public void RunTestsWithDefaultAdaptersSkipped(string projectName, double expectedNumberOfTests)
    {
        var runEventHandler = new RunEventHandler();
        TestPlatformOptions options = new()
        {
            CollectMetrics = true,
            SkipDefaultAdapters = true, // <-- skipping adapters
        };

        var perfAnalyzer = new PerfAnalyzer();
        using (perfAnalyzer.Start())
        {
            // This tells to PerfyTestAdapter how many tests it should return, this is our overhead baseline.
            var perfyTestAdapterEnv = new Dictionary<string, string?> { ["TEST_COUNT"] = expectedNumberOfTests.ToString(CultureInfo.InvariantCulture) };
            var vstestConsoleWrapper = GetVsTestConsoleWrapper(perfyTestAdapterEnv, traceLevel: System.Diagnostics.TraceLevel.Off);
            vstestConsoleWrapper.RunTests(GetPerfAssetFullPath(projectName), GetDefaultRunSettings(), options, runEventHandler);
            vstestConsoleWrapper.EndSession();
        }

        Assert.AreEqual(expectedNumberOfTests, runEventHandler.Metrics![TelemetryDataConstants.TotalTestsRun]);
        PostTelemetry(runEventHandler.Metrics, perfAnalyzer, projectName);
    }
}
