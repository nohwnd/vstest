// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.VisualStudio.TestPlatform.CommandLine2;


// Naming the file just Command.cs makes it compile just for net462, huh?

internal class Command
{
    private List<Option> _options = new();

    internal void AddOption<T>(Option<T> option)
    {
        _options.Add(option);
    }
}

