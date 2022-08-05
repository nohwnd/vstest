// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.VisualStudio.TestPlatform.Utilities;

using CommandLineResources = Microsoft.VisualStudio.TestPlatform.CommandLine.Resources.Resources;

namespace Microsoft.VisualStudio.TestPlatform.CommandLine.Processors;

internal class HelpArgumentProcessor : ArgumentProcessor<bool>
{

    public HelpArgumentProcessor()
        : base(new string[] { "-?", "-h", "--help" }, typeof(HelpArgumentExecutor))
    {
        Priority = ArgumentProcessorPriority.Help;

        HelpContentResourceName = CommandLineResources.HelpArgumentHelp;

        HelpPriority = HelpContentPriority.HelpArgumentProcessorHelpPriority;
    }
}

internal class HelpArgumentExecutor : IArgumentExecutor
{
    private readonly IOutput _output;
    private readonly List<ArgumentProcessor> _argumentProcessors;

    internal HelpArgumentExecutor(IOutput output, List<ArgumentProcessor> argumentProcessors)
    {
        _output = output;
        _argumentProcessors = argumentProcessors;
    }

    public void Initialize(string? argument)
    {
    }

    public ArgumentProcessorResult Execute()
    {
        // Output the stock output text
        OutputSection(CommandLineResources.HelpUsageText);
        OutputSection(CommandLineResources.HelpDescriptionText);
        OutputSection(CommandLineResources.HelpArgumentsText);

        var processors = _argumentProcessors.ToList();
        processors.Sort((p1, p2) => Comparer<HelpContentPriority>.Default.Compare(p1.HelpPriority, p2.HelpPriority));

        // Output the help description for RunTestsArgumentProcessor
        ArgumentProcessor? runTestsArgumentProcessor = processors.Find(p1 => p1.GetType() == typeof(RunTestsArgumentProcessor));
        TPDebug.Assert(runTestsArgumentProcessor is not null, "runTestsArgumentProcessor is null");
        processors.Remove(runTestsArgumentProcessor);
        var helpDescription = LookupHelpDescription(runTestsArgumentProcessor);
        if (helpDescription != null)
        {
            OutputSection(helpDescription);
        }

        // Output the help description for each available argument processor
        OutputSection(CommandLineResources.HelpOptionsText);
        foreach (var argumentProcessor in processors)
        {
            helpDescription = LookupHelpDescription(argumentProcessor);
            if (helpDescription != null)
            {
                OutputSection(helpDescription);
            }
        }
        OutputSection(CommandLineResources.Examples);

        // When Help has finished abort any subsequent argument processor operations
        return ArgumentProcessorResult.Abort;
    }

    /// <summary>
    /// Lookup the help description for the argument processor.
    /// </summary>
    /// <param name="argumentProcessor">The argument processor for which to discover any help content</param>
    /// <returns>The formatted string containing the help description if found null otherwise</returns>
    private string? LookupHelpDescription(ArgumentProcessor argumentProcessor)
    {
        string? result = null;

        if (argumentProcessor.HelpContentResourceName != null)
        {
            try
            {
                result = argumentProcessor.HelpContentResourceName;
                //ResourceHelper.GetString(argumentProcessor.Metadata.HelpContentResourceName, assembly, CultureInfo.CurrentUICulture);
            }
            catch (Exception e)
            {
                _output.Warning(false, e.Message);
            }
        }

        return result;
    }

    /// <summary>
    /// Output a section followed by an empty line.
    /// </summary>
    /// <param name="message">Message to output.</param>
    private void OutputSection(string message)
    {
        _output.WriteLine(message, OutputLevel.Information);
        _output.WriteLine(string.Empty, OutputLevel.Information);
    }
}
