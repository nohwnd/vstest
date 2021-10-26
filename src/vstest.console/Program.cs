// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.CommandLine
{
    using EnvDTE;
    using EnvDTE80;
    using Microsoft.VisualStudio.TestPlatform.Client.RequestHelper;
    using Microsoft.VisualStudio.TestPlatform.CommandLine.TestPlatformHelpers;
    using Microsoft.VisualStudio.TestPlatform.Common;
    using Microsoft.VisualStudio.TestPlatform.Common.Interfaces;
    using Microsoft.VisualStudio.TestPlatform.CoreUtilities.Tracing;
    using Microsoft.VisualStudio.TestPlatform.Execution;
    using Microsoft.VisualStudio.TestPlatform.Utilities;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.InteropServices;

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
            // using DEBUG && !DEBUG to disable this
#if NETFRAMEWORK && DEBUG && !DEBUG
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
            DebuggerBreakpoint.WaitForDebugger("VSTEST_RUNNER_DEBUG");
            UILanguageOverride.SetCultureSpecifiedByUser();
            var serviceLocator = new ServiceLocator();
            InstanceServiceLocator.Instance = serviceLocator;
            //#pragma warning disable CS0612 // Type or member is obsolete
            //            return new Executor(ConsoleOutput.Instance).Execute(args);
            //#pragma warning restore CS0612 // Type or member is obsolete
            return new Executor(ConsoleOutput.Instance, TestPlatformEventSource.Instance, serviceLocator).Execute(args);
        }
    }

    public class ServiceLocator : IServiceLocator
    {
        private readonly ConcurrentDictionary<Type, object> _instances = new ConcurrentDictionary<Type, object>();

        public T GetShared<T>()
        {
            if (typeof(T) == typeof(IRunSettingsProvider))
                return (T)_instances.GetOrAdd(typeof(T), _ => new RunSettingsManager());

            if (typeof(T) == typeof(ITestRequestManager))
                return (T)_instances.GetOrAdd(typeof(T), _ => new TestRequestManager());

            throw new InvalidOperationException($"Type {typeof(T)} does not have any instance available");
        }
    }

}
