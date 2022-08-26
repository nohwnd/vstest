// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using Microsoft.VisualStudio.TestPlatform.Common.Interfaces;
using Microsoft.VisualStudio.TestPlatform.Common.Utilities;
using Microsoft.VisualStudio.TestPlatform.Utilities;
using Microsoft.VisualStudio.TestPlatform.Utilities.Helpers.Interfaces;

using CommandLineResources = Microsoft.VisualStudio.TestPlatform.CommandLine.Resources.Resources;

namespace Microsoft.VisualStudio.TestPlatform.CommandLine.Processors;

/// <summary>
/// Allows the user to specify a path to load custom adapters from.
/// </summary>
internal class TestAdapterPathArgumentProcessor : ArgumentProcessor<string[]>
{
    // Cannot make it DirectoryInfo[] or FileSystemInfo[] because AzDO adds
    // additional quotes into the path that would throw on object construction.

    public TestAdapterPathArgumentProcessor()
        : base(new string[] { "--TestAdapterPath", "--test-adapter-path" }, typeof(TestAdapterPathArgumentExecutor))
    {
        Priority = ArgumentProcessorPriority.TestAdapterPath;
        HelpContentResourceName = CommandLineResources.TestAdapterPathHelp;
        HelpPriority = HelpContentPriority.TestAdapterPathArgumentProcessorHelpPriority;
    }
}

internal class TestAdapterPathArgumentExecutor : IArgumentExecutor
{
    /// <summary>
    /// Used for getting sources.
    /// </summary>
    private readonly CommandLineOptions _commandLineOptions;

    /// <summary>
    /// Run settings provider.
    /// </summary>
    private readonly IRunSettingsProvider _runSettingsManager;

    /// <summary>
    /// Used for sending output.
    /// </summary>
    private readonly IOutput _output;

    /// <summary>
    /// For file related operation
    /// </summary>
    private readonly IFileHelper _fileHelper;

    /// <summary>
    /// Separators for multiple paths in argument.
    /// </summary>
    internal readonly static char[] ArgumentSeparators = new[] { ';' };

    public const string RunSettingsPath = "RunConfiguration.TestAdaptersPaths";

    /// <summary>
    /// Default constructor.
    /// </summary>
    /// <param name="options"> The options. </param>
    /// <param name="testPlatform">The test platform</param>
    public TestAdapterPathArgumentExecutor(CommandLineOptions options, IRunSettingsProvider runSettingsManager, IOutput output, IFileHelper fileHelper)
    {
        _commandLineOptions = options ?? throw new ArgumentNullException(nameof(options));
        _runSettingsManager = runSettingsManager ?? throw new ArgumentNullException(nameof(runSettingsManager));
        _output = output ?? throw new ArgumentNullException(nameof(output));
        _fileHelper = fileHelper ?? throw new ArgumentNullException(nameof(fileHelper));
    }

    /// <summary>
    /// Initializes with the argument that was provided with the command.
    /// </summary>
    /// <param name="argument">Argument that was provided with the command.</param>
    public void Initialize(ParseResult parseResult)
    {
        var arguments = parseResult.GetValueFor(new TestAdapterPathArgumentProcessor());

        if (arguments == null)
        {
            return;
        }

        string[] customAdaptersPath;

        var testAdapterPaths = new List<string>();

        foreach (var argument in arguments)
        {
            if (argument == null)
            {
                throw new CommandLineException(
                    string.Format(CultureInfo.CurrentCulture, CommandLineResources.TestAdapterPathValueRequired));
            }

            // VSTS task add double quotes around TestAdapterpath. For example if user has given TestAdapter path C:\temp,
            // Then VSTS task will add TestAdapterPath as "/TestAdapterPath:\"C:\Temp\"".
            // Remove leading and trailing ' " ' chars...
            var path = argument.Trim().Trim(new char[] { '\"' });

            // Get test adapter paths from RunSettings.
            var testAdapterPathsInRunSettings = _runSettingsManager.QueryRunSettingsNode(RunSettingsPath);

            if (!testAdapterPathsInRunSettings.IsNullOrWhiteSpace())
            {
                testAdapterPaths.AddRange(SplitPaths(testAdapterPathsInRunSettings));
            }

            testAdapterPaths.AddRange(SplitPaths(path));
        }
        customAdaptersPath = testAdapterPaths.Distinct().ToArray();

        _runSettingsManager.UpdateRunSettingsNode(RunSettingsPath, string.Join(";", customAdaptersPath));
        _commandLineOptions.TestAdapterPath = customAdaptersPath;
    }

    /// <summary>
    /// Executes the argument processor.
    /// </summary>
    /// <returns> The <see cref="ArgumentProcessorResult"/>. </returns>
    public ArgumentProcessorResult Execute()
    {
        // Nothing to do since we updated the parameter during initialize parameter
        return ArgumentProcessorResult.Success;
    }

    /// <summary>
    /// Splits provided paths into array.
    /// </summary>
    /// <param name="paths">Source paths joined by semicolons.</param>
    /// <returns>Paths.</returns>
    internal static string[] SplitPaths(string? paths)
    {
        return paths.IsNullOrWhiteSpace() ? new string[0] : paths.Split(ArgumentSeparators, StringSplitOptions.RemoveEmptyEntries);
    }
}
