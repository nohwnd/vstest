using Microsoft.TestPlatform.VsTestConsole.TranslationLayer;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace ConsoleApp1
{
    class Program
    {
        static void Main(string[] args)
        {
            var sw = new Stopwatch();
            var handler = new DiscoveryEventsHandler(sw);
            var o = @"<?xml version=""1.0"" encoding=""utf-8""?>
                                    <RunSettings>
                                        <RunConfiguration>
                                        </RunConfiguration>
                                    </RunSettings>";
            // o = null; 
            var wrapper = new VsTestConsoleWrapper(@"C:\Projects\vstest\packages\microsoft.testplatform\16.5.0\tools\net451\Common7\IDE\Extensions\TestPlatform\vstest.console.exe", new ConsoleParameters { LogFilePath = @"c:\temp\log.txt" });
            sw.Start();
            wrapper.StartSession();
            // wrapper.InitializeExtensions( new string[0]);
            wrapper.DiscoverTests(new[] { @"C:\Projects\vstest\UnitTestProject1\bin\Debug\UnitTestProject1.dll" }, o, handler);

            Console.WriteLine($"Discovered in {sw.ElapsedMilliseconds} ms");
        }
    }

    public class DiscoveryEventsHandler : ITestDiscoveryEventsHandler
    {
        public DiscoveryEventsHandler(Stopwatch sw)
        {
            _sw = sw;
        }

        private Stopwatch _sw;
        public void HandleDiscoveredTests(IEnumerable<TestCase> discoveredTestCases)
        {
           
        }

        public void HandleDiscoveryComplete(long totalTests, IEnumerable<TestCase> lastChunk, bool isAborted)
        {
            Console.WriteLine($"Found {totalTests}");
            _sw.Stop();
            Console.WriteLine($"Discovered in {_sw.ElapsedMilliseconds} ms");
        }

        public void HandleLogMessage(TestMessageLevel level, string message)
        {
        } 

        public void HandleRawMessage(string rawMessage)
        {
        }

        public void HandleDiscoveryComplete(DiscoveryCompleteEventArgs discoveryCompleteEventArgs, IEnumerable<TestCase> lastChunk)
        {
            Console.WriteLine($"Found {discoveryCompleteEventArgs.TotalCount}");
            _sw.Stop();
            Console.WriteLine($"Discovered in {_sw.ElapsedMilliseconds} ms");
        }
    }
}
