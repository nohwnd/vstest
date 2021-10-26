// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.ObjectModel
{
    using System.Runtime.Serialization;

    /// <summary>
    /// The test process info payload.
    /// </summary>
    [DataContract]
    public class TestProcessAttachDebuggerPayload
    {

#pragma warning disable RS0016 // Add public types and members to the declared API
        public TestProcessAttachDebuggerPayload (int pid, string debuggerHint)
#pragma warning restore RS0016 // Add public types and members to the declared API
        {
            this.ProcessID = pid;
            this.DebuggerHint = debuggerHint;
        }
#pragma warning disable RS0016 // Add public types and members to the declared API
        /// <summary>
        /// The process id the debugger should attach to.
        /// </summary>
        [DataMember]
        public int ProcessID { get; set; }

        /// <summary>
        /// A hint to the debugger, describing which engine to use, or other additional info it needs to setup the correct debugger.
        /// </summary>
        [DataMember]
        public string DebuggerHint { get; set; }
#pragma warning restore RS0016 // Add public types and members to the declared API
    }
}
