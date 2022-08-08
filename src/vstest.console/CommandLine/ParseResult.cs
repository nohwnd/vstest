// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

using Microsoft.VisualStudio.TestPlatform.CommandLine.Processors;

namespace Microsoft.VisualStudio.TestPlatform.CommandLine;

internal class ParseResult
{
    public string? ParseError { get; internal set; }
    public int ExitCode { get; internal set; }
    public List<string> Errors { get; internal set; }
    public List<string> Args { get; internal set; }
    public List<string> Options { get; internal set; }
    public List<Parameter> Bound { get; internal set; }
    public List<Parameter> Unbound { get; internal set; }
    public Parser Parser { get; internal set; }

    internal T GetValueFor<T>(ArgumentProcessor<T> argumentProcessor, T? defaultValue = default)
    {
        throw new NotImplementedException();
    }


    internal bool TryGetValueFor<T>(ArgumentProcessor<T> argumentProcessor, out T? value)
    {

    }

    internal bool TryGetValueFor(IReadOnlyCollection<string> aliases, out object value)
    {

    }
}
