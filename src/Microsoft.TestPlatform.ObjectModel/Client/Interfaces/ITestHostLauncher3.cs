// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading;

namespace Microsoft.VisualStudio.TestPlatform.ObjectModel.Client.Interfaces
{
    /// <summary>
    /// Interface defining contract for custom test host implementations
    /// </summary>
#pragma warning disable RS0016 // Add public types and members to the declared API
    public interface ITestHostLauncher3 : ITestHostLauncher2
    {
        /// <summary>
        /// Attach debugger to already running custom test host process.
        /// </summary>
        /// <param name="pid">Process ID of the process to which the debugger should be attached.</param>
        /// <param name="framework">Framework version.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns><see cref="true"/> if the debugger was successfully attached to the requested process, <see cref="false"/> otherwise.</returns>
        bool AttachDebuggerToProcess(AttachDebuggerPayload data, CancellationToken cancellationToken);
    }
#pragma warning restore RS0016 // Add public types and members to the declared API
}
