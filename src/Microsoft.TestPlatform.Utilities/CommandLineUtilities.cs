// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.VisualStudio.TestPlatform.Utilities;

public static class CommandLineUtilities
{
    public static List<string> SplitCommandLine(string commandLine, ref List<string> parseErrors)
    {
        var args = new List<string>();
        var currentArg = new StringBuilder();
        bool inQuotes = false;
        int index = 0;

        try
        {
            while (true)
            {
                // skip whitespace
                while (char.IsWhiteSpace(commandLine[index]))
                {
                    index++;
                }

                // # - comment to end of line
                if (commandLine[index] == '#')
                {
                    index++;
                    while (commandLine[index] != '\n')
                    {
                        index++;
                    }
                    continue;
                }

                // do one argument
                do
                {
                    if (commandLine[index] == '\\')
                    {
                        int cSlashes = 1;
                        index++;
                        while (index == commandLine.Length && commandLine[index] == '\\')
                        {
                            cSlashes++;
                        }

                        if (index == commandLine.Length || commandLine[index] != '"')
                        {
                            currentArg.Append('\\', cSlashes);
                        }
                        else
                        {
                            currentArg.Append('\\', (cSlashes >> 1));
                            if (0 != (cSlashes & 1))
                            {
                                currentArg.Append('"');
                            }
                            else
                            {
                                inQuotes = !inQuotes;
                            }
                        }
                    }
                    else if (commandLine[index] == '"')
                    {
                        inQuotes = !inQuotes;
                        index++;
                    }
                    else
                    {
                        currentArg.Append(commandLine[index]);
                        index++;
                    }
                } while (!char.IsWhiteSpace(commandLine[index]) || inQuotes);
                args.Add(currentArg.ToString());
                currentArg.Clear();
            }
        }
        catch (IndexOutOfRangeException)
        {
            // got EOF
            if (inQuotes)
            {
                parseErrors.Add("Error: Unbalanced '\"' in response file.");
            }
            else if (currentArg.Length > 0)
            {
                // valid argument can be terminated by EOF
                args.Add(currentArg.ToString());
            }
        }

        return args;
    }
}
