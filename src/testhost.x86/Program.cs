// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.TestHost
{
#if NETFRAMEWORK && DEBUG
    using EnvDTE;
    using EnvDTE80;
#endif
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Runtime.InteropServices;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestPlatform.CoreUtilities.Helpers;
    using Microsoft.VisualStudio.TestPlatform.CoreUtilities.Tracing;
    using Microsoft.VisualStudio.TestPlatform.Execution;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel;
    using Microsoft.VisualStudio.TestPlatform.PlatformAbstractions;
    using System.Linq;

    /// <summary>
    /// The program.
    /// </summary>
    public class Program
    {
        private const string TestSourceArgumentString = "--testsourcepath";

        /// <summary>
        /// The main.
        /// </summary>
        /// <param name="args">
        /// The args.
        /// </param>
        public static void Main(string[] args)
        {
            try
            {
                TestPlatformEventSource.Instance.TestHostStart();
                Run(args);
            }
            catch (Exception ex)
            {
                EqtTrace.Error("TestHost: Error occurred during initialization of TestHost : {0}", ex);

                // Throw exception so that vstest.console get the exception message.
                throw;
            }
            finally
            {
                TestPlatformEventSource.Instance.TestHostStop();
                EqtTrace.Info("Testhost process exiting.");
            }
        }

        // In UWP(App models) Run will act as entry point from Application end, so making this method public
        public static void Run(string[] args)
        {
#if NETFRAMEWORK && DEBUG
            // This will get the first instance of VS with this version that is running
            // and attach to it, if you have multiple instances of VS started, make sure this is the first 
            // one. It will still attach correctly if VS is ready, but there is a lot of delay if debugger 
            // is not running already, so some bps might be skipped.
            if (!System.Diagnostics.Debugger.IsAttached)
            {
                var vsVersion = Environment.GetEnvironmentVariable("VisualStudioVersion");
                var inVs = !string.IsNullOrWhiteSpace(vsVersion);
                if (inVs)
                {
                    var process = System.Diagnostics.Process.GetCurrentProcess();
                    try
                    {
                        // grab the automation object and attach to debugger
                        DTE2 dte = (DTE2)Marshal.GetActiveObject($"VisualStudio.DTE.{vsVersion}");

                        // attach to the current process 
                        var id = process.Id;
                        var processes = dte.Debugger.LocalProcesses;
                        var proc = processes.Cast<Process2>().SingleOrDefault(p => p.ProcessID == id);
                        proc.Attach();
                    }
                    catch (Exception ex)
                    {
                        // no logging is setup yet
                        Console.WriteLine($"Process {process.ProcessName} ({process.Id}) failed to attach to VS debugger, because of an error: {ex}");
                    }
                }
            }
#endif
            DebuggerBreakpoint.WaitForNativeDebugger("VSTEST_HOST_NATIVE_DEBUG");
            DebuggerBreakpoint.WaitForDebugger("VSTEST_HOST_DEBUG");
            UILanguageOverride.SetCultureSpecifiedByUser();
            var argsDictionary = CommandLineArgumentsHelper.GetArgumentsDictionary(args);

            // Invoke the engine with arguments
            GetEngineInvoker(argsDictionary).Invoke(argsDictionary);
        }

        private static IEngineInvoker GetEngineInvoker(IDictionary<string, string> argsDictionary)
        {
            IEngineInvoker invoker = null;
#if NETFRAMEWORK
            // If Args contains test source argument, invoker Engine in new appdomain
            if (argsDictionary.TryGetValue(TestSourceArgumentString, out var testSourcePath) && !string.IsNullOrWhiteSpace(testSourcePath))
            {
                // remove the test source arg from dictionary
                argsDictionary.Remove(TestSourceArgumentString);

                // Only DLLs and EXEs can have app.configs or ".exe.config" or ".dll.config"
                if (System.IO.File.Exists(testSourcePath) &&
                        (testSourcePath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                        || testSourcePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)))
                {
                    invoker = new AppDomainEngineInvoker<DefaultEngineInvoker>(testSourcePath);
                }
            }
#endif
            return invoker ?? new DefaultEngineInvoker();
        }
    }
}
