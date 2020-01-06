// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.TestPlatform.Build.Trace
{
    using System;

    public static class Tracing
    {
        public static bool TraceEnabled { get; set; }

        public static void Trace(string message)
        {
            if (TraceEnabled)
            {
                Console.WriteLine(message);
            }
        }
    }
}
