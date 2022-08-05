// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Globalization;

using Microsoft.VisualStudio.TestPlatform.ObjectModel;

using CommandLineResources = Microsoft.VisualStudio.TestPlatform.CommandLine.Resources.Resources;

namespace Microsoft.VisualStudio.TestPlatform.CommandLine.Processors;

/// <summary>
/// Argument Executor for the "/TestCaseFilter" command line argument.
/// </summary>
internal class TestCaseFilterArgumentProcessor : ArgumentProcessor<string>
{
    public TestCaseFilterArgumentProcessor()
        : base(new string[] { "--filter", "/TestCaseFilter" }, typeof(TestCaseFilterArgumentExecutor))
    {
        HelpContentResourceName = CommandLineResources.TestCaseFilterArgumentHelp;
        HelpPriority = HelpContentPriority.TestCaseFilterArgumentProcessorHelpPriority;
    }
}

/// <summary>
/// Argument Executor for the "/TestCaseFilter" command line argument.
/// </summary>
internal class TestCaseFilterArgumentExecutor : IArgumentExecutor
{
    /// <summary>
    /// Used for getting sources.
    /// </summary>
    private readonly CommandLineOptions _commandLineOptions;

    /// <summary>
    /// Default constructor.
    /// </summary>
    /// <param name="options">
    /// The options.
    /// </param>
    public TestCaseFilterArgumentExecutor(CommandLineOptions options)
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
        var defaultFilter = _commandLineOptions.TestCaseFilterValue;
        var hasDefaultFilter = !defaultFilter.IsNullOrWhiteSpace();

        if (!hasDefaultFilter && argument.IsNullOrWhiteSpace())
        {
            throw new CommandLineException(string.Format(CultureInfo.CurrentCulture, CommandLineResources.TestCaseFilterValueRequired));
        }

        if (!hasDefaultFilter)
        {
            _commandLineOptions.TestCaseFilterValue = argument;
        }
        else
        {
            // Merge default filter an provided filter by AND operator to have both the default filter and custom filter applied.
            _commandLineOptions.TestCaseFilterValue = $"({defaultFilter})&({argument})";
        }
    }

    /// <summary>
    /// The TestCaseFilter is already set, return success.
    /// </summary>
    /// <returns> The <see cref="ArgumentProcessorResult"/> Success </returns>
    public ArgumentProcessorResult Execute()
    {
        return ArgumentProcessorResult.Success;
    }
}
