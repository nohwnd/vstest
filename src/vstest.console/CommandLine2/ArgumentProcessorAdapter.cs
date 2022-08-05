// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

using Microsoft.VisualStudio.TestPlatform.CommandLine.Processors;

namespace Microsoft.VisualStudio.TestPlatform.CommandLine2;
internal class OptionWithMiddleware<T>
{
    public OptionWithMiddleware(ArgumentProcessor argumentProcessor, Func<IServiceProvider, IArgumentExecutor> executorFactory)
    {
        Option = null;// new Option<bool>();
        ExecutorFactory = executorFactory;


    }

    internal Option<T> Option { get; }
    internal Func<IServiceProvider, IArgumentExecutor> ExecutorFactory { get; }
}

internal class OptionAdapter : Option<object>
{
    private ArgumentProcessor _argumentProcessor;

    public OptionAdapter(ArgumentProcessor argumentProcessor) : base("bla", "blala")
    {
        _argumentProcessor = argumentProcessor;
    }
}
