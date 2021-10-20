// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.CommandLine
{
    using Microsoft.VisualStudio.TestPlatform.Common;
    using Microsoft.VisualStudio.TestPlatform.Common.Interfaces;
    using Microsoft.VisualStudio.TestPlatform.CoreUtilities.Tracing;
    using Microsoft.VisualStudio.TestPlatform.Execution;
    using Microsoft.VisualStudio.TestPlatform.Utilities;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;

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
                return (T) _instances.GetOrAdd(typeof(T), _ => new RunSettingsManager());

            throw new InvalidOperationException($"Type {typeof(T)} does not have any instance available");
        }
    }

}
