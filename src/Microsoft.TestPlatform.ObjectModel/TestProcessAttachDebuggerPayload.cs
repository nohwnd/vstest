﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.Serialization;

#nullable disable

namespace Microsoft.VisualStudio.TestPlatform.ObjectModel;

/// <summary>
/// The test process info payload.
/// </summary>
[DataContract]
public class TestProcessAttachDebuggerPayload
{
    /// <summary>
    /// Creates a new instance of this class.
    /// </summary>
    /// <param name="processId">The process id the debugger should attach to.</param>
    public TestProcessAttachDebuggerPayload(int processId)
    {
        ProcessID = processId;
    }

    /// <summary>
    /// The process id the debugger should attach to.
    /// </summary>
    [DataMember]
    public int ProcessID { get; set; }
}
