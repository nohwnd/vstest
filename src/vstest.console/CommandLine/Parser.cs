// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

using Microsoft.VisualStudio.TestPlatform.CommandLine.Processors;

namespace Microsoft.VisualStudio.TestPlatform.CommandLine;

internal class Parser
{
    public Parser()
    {
    }

    internal ParseResult Parse(string[]? args, IReadOnlyList<ArgumentProcessor> argumentProcessors)
    {
        var exitCode = FlattenArguments(args, out string[] flattenedArguments);

        return null;

    }

    private object FlattenArguments(string[]? args, out string[] flattenedArguments)
    {
        throw new NotImplementedException();
    }

    //private int ParseOutRunsettings()
    //{
    //    var arg = args[index];
    //    // If argument is '--', following arguments are key=value pairs for run settings.
    //    if (arg.Equals("--"))
    //    {
    //        var cliRunSettingsProcessor = processorFactory.CreateArgumentProcessor(arg, args.Skip(index + 1).ToArray());
    //        processors.Add(cliRunSettingsProcessor!);
    //        break;
    //    }
    //}

    ///// <summary>
    ///// Flattens command line arguments by processing response files.
    ///// </summary>
    ///// <param name="arguments">Arguments provided to perform execution with.</param>
    ///// <param name="flattenedArguments">Array of flattened arguments.</param>
    ///// <returns>0 if successful and 1 otherwise.</returns>
    //private int FlattenArguments(IEnumerable<string> arguments, out string[] flattenedArguments)
    //{
    //    List<string> outputArguments = new();
    //    int result = 0;

    //    foreach (var arg in arguments)
    //    {
    //        if (arg.StartsWith("@", StringComparison.Ordinal))
    //        {
    //            // response file:
    //            string path = arg.Substring(1).TrimEnd(null);
    //            var hadError = ReadArgumentsAndSanitize(path, out var responseFileArgs, out var nestedArgs);

    //            if (hadError)
    //            {
    //                result |= 1;
    //            }
    //            else
    //            {
    //                Output.WriteLine($"vstest.console.exe {responseFileArgs}", OutputLevel.Information);
    //                outputArguments.AddRange(nestedArgs!);
    //            }
    //        }
    //        else
    //        {
    //            outputArguments.Add(arg);
    //        }
    //    }

    //    flattenedArguments = outputArguments.ToArray();
    //    return result;
    //}

    ///// <summary>
    ///// Read and sanitize the arguments.
    ///// </summary>
    ///// <param name="fileName">File provided by user.</param>
    ///// <param name="args">argument in the file as string.</param>
    ///// <param name="arguments">Modified argument after sanitizing the contents of the file.</param>
    ///// <returns>0 if successful and 1 otherwise.</returns>
    //public bool ReadArgumentsAndSanitize(string fileName, out string? args, out string[]? arguments)
    //{
    //    arguments = null;
    //    return GetContentUsingFile(fileName, out args)
    //        || (!args.IsNullOrEmpty() && Utilities.CommandLineUtilities.SplitCommandLineIntoArguments(args, out arguments));
    //}


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


    //private bool GetContentUsingFile(string fileName, out string? contents)
    //{
    //    contents = null;
    //    try
    //    {
    //        contents = File.ReadAllText(fileName);
    //    }
    //    catch (Exception e)
    //    {
    //        EqtTrace.Verbose("Executor.Execute: Exiting with exit code of {0}", 1);
    //        EqtTrace.Error(string.Format(CultureInfo.InvariantCulture, "Error: Can't open command line argument file '{0}' : '{1}'", fileName, e.Message));
    //        Output.Error(false, string.Format(CultureInfo.CurrentCulture, CommandLineResources.OpenResponseFileError, fileName));
    //        return true;
    //    }

    //    return false;
    //}
}
