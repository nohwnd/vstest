// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.CommandLine.Processors;

internal class NullExecutor : IArgumentExecutor
{
    public ArgumentProcessorResult Execute()
    {
        return ArgumentProcessorResult.Success;
    }

    public void Initialize(ParseResult _)
    {
    }
}
