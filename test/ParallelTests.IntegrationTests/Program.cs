using Microsoft.TestPlatform.VsTestConsole.TranslationLayer;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client.Interfaces;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ParallelTests.IntegrationTests
{
    internal class Program
    {
        public static async Task Main()
        {
            var settings = @"<RunSettings><RunConfiguration><Source>{0}</Source></RunConfiguration></RunSettings>";
            var sources = new[] {
                new {
                    Sources = new [] {
                        @"C:\p\vstest\TestProject1\bin\Debug\net48\TestProject1.dll",
                        @"C:\p\vstest\TestProject2\bin\Debug\net48\TestProject2.dll",
                    },
                    Handler = new TestRunHandler("net48-handler")
                },
                new
                {
                    Sources = new [] {
                        @"C:\p\vstest\TestProject1\bin\Debug\net472\TestProject1.dll",
                        @"C:\p\vstest\TestProject2\bin\Debug\net472\TestProject2.dll",
                    },
                    Handler = new TestRunHandler("net472-handler")
                },
                // new {
                //    Sources = new [] {
                //        @"C:\p\vstest\TestProject2\bin\Debug\net5.0\TestProject2.dll",
                //    },
                //    Handler = new TestRunHandler("net5.0")
                //},
                // new
                // {
                //     Sources = new []
                //     {
                //         @"C:\p\vstest\TestProject2\bin\Debug\netcoreapp3.1\TestProject2.dll",
                //     },
                //     Handler = new TestRunHandler("net3.1")
                // },
            };

            var console = @"C:\p\vstest\src\vstest.console\bin\Debug\net451\win7-x64\vstest.console.exe";
            var consoleOptions = new ConsoleParameters
            {
                LogFilePath = @"c:\temp\logs\log.txt",
                TraceLevel = TraceLevel.Verbose
            };
            var r = new VsTestConsoleWrapper(console, consoleOptions);

            //var sw = Stopwatch.StartNew();
            //foreach (var source in sources)
            //{
            //    // the first run is increased by 2 seconds because the session is not prestarted,
            //    // good for a basic comparison still
            //    var currentSettings = String.Format(settings, string.Join(";", source.Sources));
            //    r.RunTests(source.Sources, currentSettings, source.Handler);
            //    Console.WriteLine($"Run tests in:{source}.");
            //}
            //Console.WriteLine($"serial run took: {(int)sw.ElapsedMilliseconds} ms");

            var sw3 = Stopwatch.StartNew();
            var syncTasks = new List<Task>();
            // var handler = new TestRunHandler("common");
            foreach (var source in sources)
            {
                var currentSettings = String.Format(settings, string.Join(";", source.Sources));
                syncTasks.Add(Task.Run(() => r.RunTestsWithCustomTestHost(source.Sources, currentSettings, source.Handler, new DebuggerTestHostLauncher ()))); ;
                Console.WriteLine($"Run tests in:{source}.");
            }

            Task.WaitAll(syncTasks.ToArray());
            Console.WriteLine($"parallel run took: {(int)sw3.ElapsedMilliseconds} ms");


            //var tasks = new List<Task>();
            //var sw2 = Stopwatch.StartNew();
            //foreach (var source in sources)
            //{
            //    // this includes <Source> node in the runsettings, so we can distinguish the settings
            //    // along their way through vstest.console, runsettings should just keep it, because users 
            //    // are allowed to add their own config values.
            //    var currentSettings = String.Format(settings, string.Join(";", source.Sources));
            //    tasks.Add(r.RunTestsAsync(source.Sources, currentSettings, source.Handler));
            //    Console.WriteLine($"Run tests in:'{string.Join("','", source.Sources)}.");
            //}

            //Task.WaitAll(tasks.ToArray());
            //Console.WriteLine($"parallel run took: {(int)sw2.ElapsedMilliseconds} ms");

        }
    }

    internal class DebuggerTestHostLauncher : ITestHostLauncher3
    {
        public bool IsDebug => true;

        public bool AttachDebuggerToProcess(AttachDebuggerPayload data, CancellationToken cancellationToken)
        {
            return true;
        }

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

    public class TestRunHandler : ITestRunEventsHandler
    {
        private string _name;

        public TestRunHandler(string name)
        {
            _name = name;
        }

        public void HandleLogMessage(TestMessageLevel level, string message)
        {
            Console.WriteLine($"{_name} [{level.ToString().ToUpper()}]: {message}");
        }

        public void HandleRawMessage(string rawMessage)
        {
            Console.WriteLine($"{_name} [MESSAGE]: { rawMessage}");
        }

        public void HandleTestRunComplete(TestRunCompleteEventArgs testRunCompleteArgs, TestRunChangedEventArgs lastChunkArgs, ICollection<AttachmentSet> runContextAttachments, ICollection<string> executorUris)
        {
            Console.WriteLine($"{_name} - id: {testRunCompleteArgs.TestRunId} chunkid: {lastChunkArgs?.TestRunId} [COMPLETE]: err: { testRunCompleteArgs.Error }, lastChunk: {WriteTests(lastChunkArgs?.NewTestResults)}");
        }

        public void HandleTestRunStatsChange(TestRunChangedEventArgs testRunChangedArgs)
        {
            //Console.WriteLine($"{_name} [PROGRESS - RUNNING    ]: {WriteTests(testRunChangedArgs.ActiveTests)}");
            Console.WriteLine($"{_name} id: {testRunChangedArgs.TestRunId} [PROGRESS - NEW RESULTS]: {WriteTests(testRunChangedArgs.NewTestResults)}");
        }

        public int LaunchProcessWithDebuggerAttached(TestProcessStartInfo testProcessStartInfo)
        {
            throw new NotImplementedException();
        }

        private string WriteTests(IEnumerable<TestResult> testResults)
        {
            return WriteTests(testResults?.Select(t => t.TestCase));
        }

        private string WriteTests(IEnumerable<TestCase> testCases)
        {
            if (testCases == null)
            {
                return null;
            }

            return "\t" + string.Join("\n\t", testCases.Select(r => r.DisplayName));

        }
    }

}
