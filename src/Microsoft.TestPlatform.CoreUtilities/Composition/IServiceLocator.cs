// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.VisualStudio.TestPlatform.Utilities
{
    internal interface IServiceLocator
    {
        T GetShared<T>();
    }

    internal static class InstanceServiceLocator
    {
        public static IServiceLocator Instance { get; set; }
    }
}
