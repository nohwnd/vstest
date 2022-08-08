// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

using Microsoft.VisualStudio.TestPlatform.CommandLine.Processors;

using CommandLineResources = Microsoft.VisualStudio.TestPlatform.CommandLine.Resources.Resources;

namespace Microsoft.VisualStudio.TestPlatform.CommandLine;

internal class Parser
{
    public Parser()
    {
    }

    internal ParseResult Parse(string[]? args, IReadOnlyList<ArgumentProcessor> argumentProcessors)
    {
        List<string> parseErrors = new();

        // Ensure there are some arguments
        if (args == null || args.Length == 0)
        {
            parseErrors.Add(CommandLineResources.NoArgumentsProvided);
            return new ParseResult
            {
                Errors = parseErrors,
            };
        }

        List<string> argList = args?.ToList() ?? new();

        // Split to arguments and literal text that is after --
        var (remainingArgs, doubleDashOptions) = GetDoubleDashOptions(argList);

        // Expand all arguments we get from @ files
        var expandedArgs = ExpandResponseFileArguments(remainingArgs, ref parseErrors);
        if (parseErrors.Any())
        {
            return new ParseResult
            {
                Args = remainingArgs,
                Options = doubleDashOptions,
                Errors = parseErrors,
            };
        }

        // Find all parameters we can link to an argument processor, and parse out the desired value
        var (bound, unbound) = Bind(expandedArgs, argumentProcessors, ref parseErrors);
        if (parseErrors.Any())
        {
            return new ParseResult
            {
                Args = expandedArgs,
                Options = doubleDashOptions,
                Errors = parseErrors,
            };
        }

        // Validate all parameters
        var validate = Validate(bound, unbound, argumentProcessors, ref parseErrors);

        if (!validate || parseErrors.Any())
        {
            return new ParseResult
            {
                Args = expandedArgs,
                Bound = bound,
                Unbound = unbound,
                Options = doubleDashOptions,
                Errors = parseErrors,
            };
        }

        return new ParseResult
        {
            Args = expandedArgs,
            Bound = bound,
            Unbound = unbound,
            Options = doubleDashOptions,
            Errors = parseErrors,
            Parser = this,
        };
    }

    private (List<Parameter> bound, List<Parameter> unbound) Bind(List<string> args, IReadOnlyList<ArgumentProcessor> argumentProcessors, ref List<string> parseErrors)
    {
        throw new NotImplementedException();
    }

    private bool Validate(List<Parameter> bound, List<Parameter> unbound, IReadOnlyList<ArgumentProcessor> argumentProcessors, ref List<string> parseErrors)
    {
        if (unbound.Count > 0)
        {
            foreach (var arg in unbound)
            {
                parseErrors.Add(string.Format(CultureInfo.CurrentCulture, CommandLineResources.InvalidArgument, arg.Name));
            }

            return false;
        }

        foreach (var arg in bound)
        {
            foreach (var validator in arg.Processor.Validators)
            {
                var error = validator(arg.Value);
                if (!StringUtils.IsNullOrWhiteSpace(error))
                {
                    parseErrors.Add(error);
                }
            }
        }


        return true;
    }

    private static List<string> ExpandResponseFileArguments(List<string> args, ref List<string> parseErrors)
    {
        var outputArguments = new List<string>(args.Count);
        foreach (var arg in args)
        {
            if (!arg.StartsWith("@", StringComparison.Ordinal))
            {
                outputArguments.Add(arg);
                continue;
            }

            string path = arg.Substring(1).TrimEnd();
            string? content = null;
            try
            {
                // TODO: do we have a special class for this? FileHelper?
                content = File.ReadAllText(path);
                // REVIEW: this is possibly very long, let's not print that to the screen again? 
                // _output.WriteLine($"vstest.console.exe {content}", OutputLevel.Information);
            }
            catch (Exception)
            {
                parseErrors.Add(string.Format(CultureInfo.CurrentCulture, CommandLineResources.OpenResponseFileError, path));
            }

            if (content != null)
            {
                if (!Utilities.CommandLineUtilities.SplitCommandLineIntoArguments(content, out var expandedArguments))
                {
                    // TODO: localize
                    parseErrors.Add($"Error splitting arguments from response file: '{path}'.");
                }
                else
                {
                    outputArguments.AddRange(expandedArguments);
                }
            }
        }

        return outputArguments;
    }

    private static (List<string> args, List<string> options) GetDoubleDashOptions(List<string> args)
    {
        var doubleDashIndex = args.ToList().IndexOf("--");

        if (doubleDashIndex == -1)
        {
            // Double dash "--" in not found, we have just args.
            return (args, new List<string>());
        }

        // Double dash is found, return the parts before and after it.
        var options = new List<string>(args.Count);
        var argsRemainder = new List<string>(args.Count);
        for (int i = 0; i < args.Count; i++)
        {
            if (i < doubleDashIndex)
            {
                argsRemainder.Add(args[i]);
            }
            else if (i == doubleDashIndex)
            {
                // skip --
            }
            else
            {
                options.Add(args[i]);
            }
        }

        return (argsRemainder, options);
    }

    ///// <summary>
    ///// Verify that the arguments are valid.
    ///// </summary>
    ///// <param name="argumentProcessors">Processors to verify against.</param>
    ///// <returns>0 if successful and 1 otherwise.</returns>
    //private int IdentifyDuplicateArguments(IEnumerable<ArgumentProcessor> argumentProcessors)
    //{
    //    int result = 0;

    //    // Used to keep track of commands that are only allowed to show up once.  The first time it is seen
    //    // an entry for the command will be added to the dictionary and the value will be set to 1.  If we
    //    // see the command again and the value is 1 (meaning this is the second time we have seen the command),
    //    // we will output an error and increment the count.  This ensures that the error message will only be
    //    // displayed once even if the user does something like /ListDiscoverers /ListDiscoverers /ListDiscoverers.
    //    var commandSeenCount = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    //    // Check each processor.
    //    foreach (var processor in argumentProcessors)
    //    {
    //        if (processor.Metadata.Value.AllowMultiple)
    //        {
    //            continue;
    //        }

    //        if (!commandSeenCount.TryGetValue(processor.Metadata.Value.CommandName, out int count))
    //        {
    //            commandSeenCount.Add(processor.Metadata.Value.CommandName, 1);
    //        }
    //        else if (count == 1)
    //        {
    //            result = 1;

    //            // Update the count so we do not print the error out for this argument multiple times.
    //            commandSeenCount[processor.Metadata.Value.CommandName] = ++count;
    //            Output.Error(false, string.Format(CultureInfo.CurrentCulture, CommandLineResources.DuplicateArgumentError, processor.Metadata.Value.CommandName));
    //        }
    //    }
    //    return result;
    //}
}

internal class Parameter
{
    public object Value { get; }

    public ArgumentProcessor Processor { get; }
}
