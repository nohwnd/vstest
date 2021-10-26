// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter
{
    /// <summary>
    /// Handle to the framework which is passed to the test executors.
    /// </summary>
    public interface IFrameworkHandle2 : IFrameworkHandle
    {
        /// <summary>
        /// Attach debugger to an already running process.
        /// </summary>
        /// <param name="pid">Process ID of the process to which the debugger should be attached.</param>
        /// <returns><see cref="true"/> if the debugger was successfully attached to the requested process, <see cref="false"/> otherwise.</returns>
        bool AttachDebuggerToProcess(int pid);
    }

    /// <summary>
    /// Handle to the framework which is passed to the test executors.
    /// </summary>
#pragma warning disable RS0016 // Add public types and members to the declared API
    public interface IFrameworkHandle3 : IFrameworkHandle2
    {
        /// <summary>
        /// Attach debugger to an already running process.
        /// </summary>
        /// <param name="pid">Process ID of the process to which the debugger should be attached.</param>
        /// <param name="debuggerHint">A hint to the debugger that helps it select the correct debugging settings.</param>
        /// <returns><see cref="true"/> if the debugger was successfully attached to the requested process, <see cref="false"/> otherwise.</returns>
        bool AttachDebuggerToProcess(int pid, string debuggerHint);
    }
#pragma warning restore RS0016 // Add public types and members to the declared API
}
