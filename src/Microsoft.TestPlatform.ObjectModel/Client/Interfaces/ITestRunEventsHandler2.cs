// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.ObjectModel.Client
{
    /// <summary>
    /// Interface contract for handling test run events during run operation.
    /// </summary>
    public interface ITestRunEventsHandler2 : ITestRunEventsHandler
    {
        /// <summary>
        /// Attach debugger to an already running process.
        /// </summary>
        /// <param name="pid">Process ID of the process to which the debugger should be attached.</param>
        /// <returns><see cref="true"/> if the debugger was successfully attached to the requested process, <see cref="false"/> otherwise.</returns>
        bool AttachDebuggerToProcess(int pid);
    }

    /// <summary>
    /// Interface contract for handling test run events during run operation.
    /// </summary>
#pragma warning disable RS0016 // Add public types and members to the declared API
    public interface ITestRunEventsHandler3 : ITestRunEventsHandler2
    {
        /// <summary>
        /// Attach debugger to an already running process.
        /// </summary>
        /// <param name="pid">Process ID of the process to which the debugger should be attached.</param>
        /// <returns><see cref="true"/> if the debugger was successfully attached to the requested process, <see cref="false"/> otherwise.</returns>
        bool AttachDebuggerToProcess(int pid, string debuggerHint);
    }
#pragma warning restore RS0016 // Add public types and members to the declared API
}
