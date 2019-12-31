// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.CommandLine
{
    using System;
    using System.Globalization;
    using System.IO;
    using Microsoft.VisualStudio.TestPlatform.Utilities;

    /// <summary>
    /// Main entry point for the command line runner.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// Main entry point. Hands off execution to the executor class.
        /// </summary>
        /// <param name="args">Arguments provided on the command line.</param>
        /// <returns>0 if everything was successful and 1 otherwise.</returns>
        public static int Main(string[] args)
        {
            File.AppendAllText(@"C:\temp\t.txt", $"----> starting {DateTime.Now.ToString("HH:mm:ss.fff")}\n");
            var debugEnabled = Environment.GetEnvironmentVariable("VSTEST_RUNNER_DEBUG");
            if (!string.IsNullOrEmpty(debugEnabled) && debugEnabled.Equals("1", StringComparison.Ordinal))
            {
                ConsoleOutput.Instance.WriteLine("Waiting for debugger attach...", OutputLevel.Information);

                var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
                ConsoleOutput.Instance.WriteLine(
                    string.Format("Process Id: {0}, Name: {1}", currentProcess.Id, currentProcess.ProcessName),
                    OutputLevel.Information);

                while (!System.Diagnostics.Debugger.IsAttached)
                {
                    System.Threading.Thread.Sleep(1000);
                }

                System.Diagnostics.Debugger.Break();
            }

            SetCultureSpecifiedByUser();
            System.IO.File.AppendAllText(@"C:\temp\t.txt", $"executor create {System.DateTime.Now.ToString("HH:mm:ss.fff")}\n");
            var e = new Executor(ConsoleOutput.Instance);
            System.IO.File.AppendAllText(@"C:\temp\t.txt", $"executor crate done {System.DateTime.Now.ToString("HH:mm:ss.fff")}\n");
            var r = e.Execute(args);
            System.IO.File.AppendAllText(@"C:\temp\t.txt", $"executor execution done {System.DateTime.Now.ToString("HH:mm:ss.fff")}\n");
            return r;
        }

        private static void SetCultureSpecifiedByUser()
        {
            var userCultureSpecified = Environment.GetEnvironmentVariable(CoreUtilities.Constants.DotNetUserSpecifiedCulture);
            if(!string.IsNullOrWhiteSpace(userCultureSpecified))
            {
                try
                {
                    CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.CreateSpecificCulture(userCultureSpecified);
                }
                catch(Exception)
                {
                    ConsoleOutput.Instance.WriteLine(string.Format("Invalid Culture Info: {0}", userCultureSpecified), OutputLevel.Information);
                }
            }
        }
    }
}
