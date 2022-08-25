// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

namespace Microsoft.VisualStudio.TestPlatform.CommandLine.Processors;

/// <summary>
//  An argument processor to provide path to the file for listing fully qualified tests.
/// To be used only with ListFullyQualifiedTests
/// </summary>
internal class ListTestsTargetPathArgumentProcessor : ArgumentProcessor<FileInfo>
{
    public ListTestsTargetPathArgumentProcessor()
        : base("--ListTestsTargetPath", typeof(ListTestsTargetPathArgumentExecutor))
    {
        IsHiddenInHelp = true;
    }
}

internal class ListTestsTargetPathArgumentExecutor : IArgumentExecutor
{
    /// <summary>
    /// Used for getting sources.
    /// </summary>
    private readonly CommandLineOptions _commandLineOptions;

    public ListTestsTargetPathArgumentExecutor(CommandLineOptions options)
    {
        ValidateArg.NotNull(options, nameof(options));
        _commandLineOptions = options;
    }

    /// <summary>
    /// Initializes with the argument that was provided with the command.
    /// </summary>
    /// <param name="argument">Argument that was provided with the command.</param>
    public void Initialize(string? argument)
    {
        if (argument.IsNullOrWhiteSpace())
        {
            // Not adding this string to resources because this processor is only used internally.
            throw new CommandLineException("ListTestsTargetPath is required with ListFullyQualifiedTests!");
        }

        _commandLineOptions.ListTestsTargetPath = argument;
    }

    /// <summary>
    /// The ListTestsTargetPath is already set, return success.
    /// </summary>
    /// <returns> The <see cref="ArgumentProcessorResult"/> Success </returns>
    public ArgumentProcessorResult Execute()
    {
        return ArgumentProcessorResult.Success;
    }
}
