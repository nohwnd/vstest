// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using Microsoft.VisualStudio.TestPlatform.CommandLine.Processors;

namespace Microsoft.VisualStudio.TestPlatform.CommandLine;

internal class ParseResult
{
    public int ExitCode { get; internal set; }
    public List<string> Errors { get; internal set; }
    public List<string> Args { get; internal set; }
    public List<string> Options { get; internal set; }
    public List<BoundParameter> Bound { get; internal set; } = new();
    public List<TypedParameter> Typed { get; internal set; } = new();
    public List<string> Unbound { get; internal set; }
    public Parser Parser { get; internal set; }
    public IReadOnlyList<ArgumentProcessor> Processors { get; internal set; }

    internal T? GetValueFor<T>(ArgumentProcessor<T> argumentProcessor, T? defaultValue = default)
    {
        return TryGetValueFor((ArgumentProcessor)argumentProcessor, out var value) ? (T?)value : defaultValue;
    }

    internal bool TryGetValueFor(ArgumentProcessor argumentProcessor, out object? value)
    {
# if DEBUG
        // This fails for example when we exclude argument processor for
        // artifact post processing and try to grab it in logo processor.
        if (!Processors.Any(p => p.GetType() == argumentProcessor.GetType()))
        {
            throw new ArgumentException($"Processor {argumentProcessor.Name} is not registered.", nameof(argumentProcessor));
        }
# endif

        var typed = Typed.SingleOrDefault(p => p.Processor.GetType() == argumentProcessor.GetType());
        if (typed == null)
        {
            value = default;
            return false;
        }

        value = typed.Value;
        return true;
    }

    internal bool TryGetValueFor<T>(ArgumentProcessor<T> argumentProcessor, out T? value)
    {
        var result = TryGetValueFor(argumentProcessor, out object? objectValue);
        value = (T?)objectValue;
        return result;
    }
}
