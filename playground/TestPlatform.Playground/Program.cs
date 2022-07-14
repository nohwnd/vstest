﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;

using Microsoft.TestPlatform.VsTestConsole.TranslationLayer;
using Microsoft.TestPlatform.VsTestConsole.TranslationLayer.Interfaces;
using Microsoft.VisualStudio.TestPlatform.CommandLine;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client.Interfaces;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

#nullable disable

namespace TestPlatform.Playground;

internal class Program
{
    static void Main(string[] args)
    {
        // This project references TranslationLayer, vstest.console, TestHostProvider, testhost and MSTest1 projects, to make sure
        // we build all the dependencies of that are used to run tests via VSTestConsoleWrapper. It then copies the components from
        // their original build locations, to $(TargetDir)\vstest.console directory, and its subfolders to create an executable
        // copy of TestPlatform that is similar to what we ship.
        //
        // The copying might trigger only on re-build, if you see outdated dependencies, Rebuild this project instead of just Build.
        //
        // Use this as playground for your debugging of end-to-end scenarios, it will automatically attach vstest.console and teshost
        // sub-processes. It won't stop at entry-point automatically, don't forget to set your breakpoints, or remove VSTEST_DEBUG_NOBP
        // from the environment variables of this project.

        EnvironmentVariables.Variables.ToList().ForEach(p => Environment.SetEnvironmentVariable(p.Key, p.Value));
        var thisAssemblyPath = Assembly.GetEntryAssembly().Location;
        var here = Path.GetDirectoryName(thisAssemblyPath);
        var playground = Path.GetFullPath(Path.Combine(here, "..", "..", "..", ".."));

        var discoverySettings = $@"
                <RunSettings>
                    <RunConfiguration>
                        <InIsolation>true</InIsolation>
                        <MaxCpuCount>10</MaxCpuCount>
<DisableAppDomain>False</DisableAppDomain>
<BatchSize>10</BatchSize>
                    </RunConfiguration>
                </RunSettings>
            ";

        var sourceSettings = @"
                <RunSettings>
                    <RunConfiguration>
                        <InIsolation>true</InIsolation>
                        <MaxCpuCount>10</MaxCpuCount>
<BatchSize>10</BatchSize>
                    </RunConfiguration>
                </RunSettings>
            ";

        var sources = new[] {
            Path.Combine(playground, "MSTest1", "bin", "Debug", "net472", "MSTest1.dll"),
            Path.Combine(playground, "MSTest1", "bin", "Debug", "net5.0", "MSTest1.dll"),
        };

        //// console mode
        //var settingsFile = Path.GetTempFileName();
        //try
        //{
        //    File.WriteAllText(settingsFile, sourceSettings);
        //    var process = Process.Start(console, string.Join(" ", sources) + " --settings:" + settingsFile + " --listtests");
        //    var cmd = console + "\n\n" + string.Join(" ", sources) + " --settings:" + settingsFile + " --listtests";
        //    var swc = Stopwatch.StartNew();
        //    process.WaitForExit();
        //    if (process.ExitCode != 0)
        //    {
        //        throw new Exception($"Process failed with {process.ExitCode}");
        //    }
        //    Console.WriteLine($"Done in {swc.ElapsedMilliseconds} ms");
        //}
        //finally
        //{
        //    try { File.Delete(settingsFile); } catch { }
        //}

        // design mode
        var dotnetExe = @"C:\Program Files\dotnet\dotnet.exe";
        var consoleDll = Path.Combine(here, "vstest.console", "vstest.console.dll");
        var consoleOptions = new ConsoleParameters
        {
            //LogFilePath = Path.Combine(here, "logs", "log.txt"),
            //TraceLevel = TraceLevel.Off,
        };
        var options = new TestPlatformOptions { CollectMetrics = true };
        // var r = new VsTestConsoleWrapper(consoleDll, dotnetExe, consoleOptions);
        // r.StartSession();
        IVsTestConsoleWrapper r = new InProcessVsTestConsoleWrapper(consoleOptions);

        var sessionHandler = new TestSessionHandler();
#pragma warning disable CS0618 // Type or member is obsolete
        //   r.StartTestSession(sources, sourceSettings, sessionHandler);
#pragma warning restore CS0618 // Type or member is obsolete
        var discoveryHandler = new PlaygroundTestDiscoveryHandler();
        var sw = Stopwatch.StartNew();
        r.DiscoverTests(sources, discoverySettings, options, sessionHandler.TestSessionInfo, discoveryHandler);
        var dd = sw.ElapsedMilliseconds;
        Console.WriteLine($"Discovery done in {sw.ElapsedMilliseconds} ms");
        sw.Restart();
        var discoveryDuration = sw.ElapsedMilliseconds;
        Console.WriteLine($"Discovery done in {discoveryDuration} ms");
        sw.Restart();
        // Run with test cases and custom testhost launcher
        r.RunTestsWithCustomTestHost(discoveryHandler.TestCases, sourceSettings, options, sessionHandler.TestSessionInfo, new TestRunHandler(), new DebuggerTestHostLauncher());
        var rd = sw.ElapsedMilliseconds;
        Console.WriteLine($"Discovery: {dd} ms, Run: {rd} ms, Total: {dd + rd} ms");
    }

    public class PlaygroundTestDiscoveryHandler : ITestDiscoveryEventsHandler, ITestDiscoveryEventsHandler2
    {
        private int _testCasesCount;

        public List<TestCase> TestCases { get; internal set; } = new List<TestCase>();

        public void HandleDiscoveredTests(IEnumerable<TestCase>? discoveredTestCases)
        {
            //Console.WriteLine($"[DISCOVERY.PROGRESS]");
            //Console.WriteLine(WriteTests(discoveredTestCases));
            _testCasesCount += discoveredTestCases.Count();
            if (discoveredTestCases != null) { TestCases.AddRange(discoveredTestCases); }
        }

        public void HandleDiscoveryComplete(long totalTests, IEnumerable<TestCase>? lastChunk, bool isAborted)
        {
            Console.WriteLine($"[DISCOVERY.COMPLETE] aborted? {isAborted}, tests count: {totalTests}");
            //Console.WriteLine("Last chunk:");
            //Console.WriteLine(WriteTests(lastChunk));
            if (lastChunk != null) { TestCases.AddRange(lastChunk); }
        }

        public void HandleDiscoveryComplete(DiscoveryCompleteEventArgs discoveryCompleteEventArgs, IEnumerable<TestCase>? lastChunk)
        {
            Console.WriteLine($"[DISCOVERY.COMPLETE] aborted? {discoveryCompleteEventArgs.IsAborted}, tests count: {discoveryCompleteEventArgs.TotalCount}, discovered count: {_testCasesCount}");
            //Console.WriteLine("Last chunk:");
            //Console.WriteLine(WriteTests(lastChunk));
            Console.WriteLine("Fully discovered:");
            Console.WriteLine(WriteSources(discoveryCompleteEventArgs.FullyDiscoveredSources));
            Console.WriteLine("Partially discovered:");
            Console.WriteLine(WriteSources(discoveryCompleteEventArgs.PartiallyDiscoveredSources));
            Console.WriteLine("Skipped discovery:");
            Console.WriteLine(WriteSources(discoveryCompleteEventArgs.SkippedDiscoveredSources));
            Console.WriteLine("Not discovered:");
            Console.WriteLine(WriteSources(discoveryCompleteEventArgs.NotDiscoveredSources));
            if (lastChunk != null) { TestCases.AddRange(lastChunk); }
        }

        public void HandleLogMessage(TestMessageLevel level, string? message)
        {
            Console.WriteLine($"[DISCOVERY.{level.ToString().ToUpper(CultureInfo.InvariantCulture)}] {message}");
        }

        public void HandleRawMessage(string rawMessage)
        {
            //Console.WriteLine($"[DISCOVERY.MESSAGE] {rawMessage}");
        }

        private static string WriteTests(IEnumerable<TestCase>? testCases)
            => testCases?.Any() == true
                ? "\t" + string.Join("\n\t", testCases?.Select(r => r.Source + " " + r.DisplayName))
                : "\t<empty>";

        private static string WriteSources(IEnumerable<string>? sources)
            => sources?.Any() == true
                ? "\t" + string.Join("\n\t", sources)
                : "\t<empty>";
    }

    public class TestRunHandler : ITestRunEventsHandler
    {

        public TestRunHandler()
        {
        }

        public void HandleLogMessage(TestMessageLevel level, string? message)
        {
            Console.WriteLine($"[{level.ToString().ToUpper(CultureInfo.InvariantCulture)}]: {message}");
        }

        public void HandleRawMessage(string rawMessage)
        {
            //Console.WriteLine($"[RUN.MESSAGE]: {rawMessage}");
        }

        public void HandleTestRunComplete(TestRunCompleteEventArgs testRunCompleteArgs, TestRunChangedEventArgs? lastChunkArgs, ICollection<AttachmentSet>? runContextAttachments, ICollection<string>? executorUris)
        {
            Console.WriteLine($"[RUN.COMPLETE]: err: {testRunCompleteArgs.Error}, lastChunk:");
            //Console.WriteLine(WriteTests(lastChunkArgs?.NewTestResults));
        }

        public void HandleTestRunStatsChange(TestRunChangedEventArgs? testRunChangedArgs)
        {
            //Console.WriteLine($"[RUN.PROGRESS]");
            //Console.WriteLine(WriteTests(testRunChangedArgs.NewTestResults));
        }

        public int LaunchProcessWithDebuggerAttached(TestProcessStartInfo testProcessStartInfo)
        {
            throw new NotImplementedException();
        }

        private static string WriteTests(IEnumerable<TestResult>? testResults)
            => WriteTests(testResults?.Select(t => t.TestCase));

        private static string WriteTests(IEnumerable<TestCase>? testCases)
            => testCases?.Any() == true
                ? "\t" + string.Join("\n\t", testCases.Select(r => r.DisplayName))
                : "\t<empty>";
    }

    internal class DebuggerTestHostLauncher : ITestHostLauncher2
    {
        public bool IsDebug => true;

        public bool AttachDebuggerToProcess(int pid)
        {
            return true;
        }

        public bool AttachDebuggerToProcess(int pid, CancellationToken cancellationToken)
        {
            return true;
        }

        public int LaunchTestHost(TestProcessStartInfo defaultTestHostStartInfo)
        {
            return 1;
        }

        public int LaunchTestHost(TestProcessStartInfo defaultTestHostStartInfo, CancellationToken cancellationToken)
        {
            return 1;
        }
    }
}

internal class TestSessionHandler : ITestSessionEventsHandler
{
    public TestSessionInfo? TestSessionInfo { get; private set; }

    public void HandleLogMessage(TestMessageLevel level, string? message)
    {

    }

    public void HandleRawMessage(string rawMessage)
    {

    }

    public void HandleStartTestSessionComplete(StartTestSessionCompleteEventArgs? eventArgs)
    {
        TestSessionInfo = eventArgs?.TestSessionInfo;
    }

    public void HandleStopTestSessionComplete(StopTestSessionCompleteEventArgs? eventArgs)
    {

    }
}
