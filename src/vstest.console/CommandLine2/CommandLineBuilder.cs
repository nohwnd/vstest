// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

using Microsoft.VisualStudio.TestPlatform.CommandLine.Processors;

namespace Microsoft.VisualStudio.TestPlatform.CommandLine2;

internal class CommandLineBuilder
{
    private List<InvocationMiddleware> _middleware;

    public CommandLineBuilder(Command? rootCommand = null)
    {
        Command = rootCommand != null ? rootCommand : new RootCommand();
    }

    public Command Command { get; }

    internal CommandLineBuilder AddOptionWithMiddleware(Option option)
    {
        // Command.AddOption(option);
        return null;
    }

    internal void AddOption(Option option)
    {
        throw new NotImplementedException();
    }

    internal void AddMiddleware(Func<IServiceProvider, IArgumentExecutor> middleware)
    {
        throw new NotImplementedException();
    }
}
