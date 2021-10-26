﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.ObjectModel.Client
{
    using System.Runtime.Serialization;
#pragma warning disable RS0016 // Add public types and members to the declared API
    public class AttachDebuggerPayload
    {
        [DataMember]
        public int Pid { get; set; }

        [DataMember]
        public string DebuggerHint { get; set; }
#pragma warning restore RS0016 // Add public types and members to the declared API
    }
}
