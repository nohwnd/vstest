// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;

using Microsoft.VisualStudio.TestPlatform.CommandLine.Processors;

namespace Microsoft.VisualStudio.TestPlatform.CommandLine;

internal class ParseResult
{
    private readonly Dictionary<string, string> Strings = new(StringComparer.OrdinalIgnoreCase);

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

    internal bool TryGetValueFor(ArgumentProcessor argumentProcessor, out object? value)
    {
        if (!Strings.TryGetValue(argumentProcessor.Name, out string? unparsedValue))
        {
            value = default;
            return false;
        }

        value = argumentProcessor.ValueFactory(unparsedValue);
        return true;
    }

    internal bool TryGetValueFor<T>(ArgumentProcessor<T> argumentProcessor, out T? value)
    {
        if (!Strings.TryGetValue(argumentProcessor.Name, out string? unparsedValue))
        {
            value = default;
            return false;
        }

        value = (T?)argumentProcessor.ValueFactory(unparsedValue);
        return true;
    }
}
