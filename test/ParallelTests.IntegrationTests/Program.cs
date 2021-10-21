using Microsoft.TestPlatform.VsTestConsole.TranslationLayer;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ParallelTests.IntegrationTests
{
    internal class Program
    {
        public static async Task Main()
        {
            var settings = @"<RunSettings><RunConfiguration><InIsolation>true</InIsolation></RunConfiguration></RunSettings>";
            var sources = new[] {
                @"C:\p\vstest2\TestProject1\bin\Debug\net48\TestProject1.dll",
                @"C:\p\vstest2\TestProject1\bin\Debug\net472\TestProject1.dll"
            };

            var console = @"C:\p\vstest2\src\vstest.console\bin\Debug\net451\win7-x64\vstest.console.exe";
            var consoleOptions = new ConsoleParameters
            {
                LogFilePath = @"c:\temp\logs\log.txt",
                TraceLevel = System.Diagnostics.TraceLevel.Verbose
            };
            var r = new VsTestConsoleWrapper(console, consoleOptions);
            ITestRunEventsHandler handler = new TestRunHandler();

            var tasks = new List<Task>();

            foreach (var source in sources)
            {
                tasks.Add(r.RunTestsAsync(new[] { source }, settings, handler));
                Console.WriteLine($"Run tests in:{source}.");
            }

            Task.WaitAll(tasks.ToArray());
        }
    }

    public class TestRunHandler : ITestRunEventsHandler
    {
        public void HandleLogMessage(TestMessageLevel level, string message)
        {
            Console.WriteLine($"[{level.ToString().ToUpper()}]: {message}");
        }

        public void HandleRawMessage(string rawMessage)
        {
            Console.WriteLine($"[MESSAGE]: { rawMessage}");
        }

        public void HandleTestRunComplete(TestRunCompleteEventArgs testRunCompleteArgs, TestRunChangedEventArgs lastChunkArgs, ICollection<AttachmentSet> runContextAttachments, ICollection<string> executorUris)
        {
            Console.WriteLine($"[COMPLETE]: err: { testRunCompleteArgs.Error }, lastChunk: {WriteTests(lastChunkArgs.NewTestResults)}");
        }

        public void HandleTestRunStatsChange(TestRunChangedEventArgs testRunChangedArgs)
        {
            Console.WriteLine($"[PROGRESS - RUNNING    ]: {WriteTests(testRunChangedArgs.ActiveTests)}");
            Console.WriteLine($"[PROGRESS - NEW RESULTS]: {WriteTests(testRunChangedArgs.NewTestResults)}");
        }

        public int LaunchProcessWithDebuggerAttached(TestProcessStartInfo testProcessStartInfo)
        {
            throw new NotImplementedException();
        }

        private string WriteTests(IEnumerable<TestResult> testResults)
        {
            return WriteTests(testResults.Select(t => t.TestCase));
        }

       private string WriteTests(IEnumerable<TestCase> testCases) {
                
            return "\t" + string.Join("\n\t", testCases.Select(r =>  r.DisplayName ));
        }
    }

}
