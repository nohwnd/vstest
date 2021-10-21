using Microsoft.TestPlatform.VsTestConsole.TranslationLayer;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
                        @"C:\p\vstest2\TestProject1\bin\Debug\net48\TestProject1.dll",
                        @"C:\p\vstest2\TestProject2\bin\Debug\net48\TestProject2.dll",
                    },
                    Handler = new TestRunHandler("net48 ")
                },
                new
                {
                    Sources = new [] {
                        @"C:\p\vstest2\TestProject1\bin\Debug\net472\TestProject1.dll",
                        @"C:\p\vstest2\TestProject2\bin\Debug\net472\TestProject2.dll",
                    },
                    Handler = new TestRunHandler("net472")
                },
                 new {
                    Sources = new [] {
                        @"C:\p\vstest2\TestProject2\bin\Debug\net5.0\TestProject2.dll",
                    },
                    Handler = new TestRunHandler("net5.0")
                },
                 new
                 {
                     Sources = new []
                     {
                         @"C:\p\vstest2\TestProject2\bin\Debug\netcoreapp3.1\TestProject2.dll",
                     },
                     Handler = new TestRunHandler("net3.1")
                 },
            };

            var console = @"C:\p\vstest2\src\vstest.console\bin\Debug\net451\win7-x64\vstest.console.exe";
            var consoleOptions = new ConsoleParameters
            {
                LogFilePath = @"c:\temp\logs\log.txt",
                TraceLevel = System.Diagnostics.TraceLevel.Verbose
            };
            var r = new VsTestConsoleWrapper(console, consoleOptions);
            // ITestRunEventsHandler handler = new TestRunHandler();

            //var sw = Stopwatch.StartNew();
            //foreach (var source in sources)
            //{
            //    // this in increased by 2 seconds because the session is not prestarted,
            //    // good for a basic comparison still
            //    r.RunTests(new[] { source }, settings, handler);
            //    Console.WriteLine($"Run tests in:{source}.");
            //}
            //Console.WriteLine($"serial run took: {(int)sw.ElapsedMilliseconds} ms");


            var tasks = new List<Task>();
            var sw2 = Stopwatch.StartNew();
            foreach (var source in sources)
            {
                // this includes <Source> node in the runsettings, so we can distinguish the settings
                // along their way through vstest.console, runsettings should just keep it, because users 
                // are allowed to add their own config values.
                var currentSettings = String.Format(settings, source);
                tasks.Add(r.RunTestsAsync(source.Sources, currentSettings, source.Handler));
                Console.WriteLine($"Run tests in:'{string.Join("','", source.Sources)}.");
            }

            Task.WaitAll(tasks.ToArray());
            Console.WriteLine($"parallel run took: {(int)sw2.ElapsedMilliseconds} ms");

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
            Console.WriteLine($"{_name} [COMPLETE]: err: { testRunCompleteArgs.Error }, lastChunk: {WriteTests(lastChunkArgs?.NewTestResults)}");
        }

        public void HandleTestRunStatsChange(TestRunChangedEventArgs testRunChangedArgs)
        {
            Console.WriteLine($"{_name} [PROGRESS - RUNNING    ]: {WriteTests(testRunChangedArgs.ActiveTests)}");
            Console.WriteLine($"{_name} [PROGRESS - NEW RESULTS]: {WriteTests(testRunChangedArgs.NewTestResults)}");
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
