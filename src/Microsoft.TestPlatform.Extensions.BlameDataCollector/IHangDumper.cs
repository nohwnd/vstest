﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.TestPlatform.Extensions.BlameDataCollector
{
    [System.Obsolete]
    public interface IHangDumper
    {
        void Dump(int processId, string outputDirectory, DumpTypeOption dumpType);
    }
}
