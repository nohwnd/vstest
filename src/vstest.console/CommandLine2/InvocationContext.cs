// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

using Microsoft.VisualStudio.TestPlatform.CommandLine;

namespace Microsoft.VisualStudio.TestPlatform.CommandLine2;

internal class InvocationContext
{
    public InvocationContext(ServiceProvider serviceProvider, ParseResult parseResult)
    {
        ServiceProvider = serviceProvider;
        ParseResult = parseResult;
    }

    public ParseResult ParseResult { get; }
    public ServiceProvider ServiceProvider { get; internal set; }
    public object?[] ExitCode { get; internal set; }
}
