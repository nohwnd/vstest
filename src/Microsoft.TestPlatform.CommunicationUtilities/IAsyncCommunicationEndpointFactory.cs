// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

using Microsoft.VisualStudio.TestPlatform.ObjectModel;

namespace Microsoft.VisualStudio.TestPlatform.CommunicationUtilities;
internal interface IAsyncCommunicationEndpointFactory
{
    IAsyncCommunicationEndPoint Create(string name, string address, ConnectionRole role, Transport transport);
}
