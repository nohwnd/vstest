// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.Utilities
{
    using System;
    using System.IO;

    /// <summary>
    /// Sends output to the console.
    /// </summary>
    public class ConsoleOutput : IOutput
    {
        private static ConsoleOutput instance = null;

        private TextWriter standardOutput = null;
        private TextWriter standardError = null;
        private bool oweNewLine = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConsoleOutput"/> class.
        /// </summary>
        internal ConsoleOutput()
        {
            this.standardOutput = Console.Out;
            this.standardError = Console.Error;
        }

        /// <summary>
        /// Gets the instance of <see cref="ConsoleOutput"/>.
        /// </summary>
        public static ConsoleOutput Instance
        {
            get
            {
                if (instance != null)
                {
                    return instance;
                }

                lock (Console.Out)
                {
                    if (instance == null)
                    {
                        instance = new ConsoleOutput();
                    }

                    return instance;
                }
            }
        }

        /// <summary>
        /// Writes the message with a new line.
        /// </summary>
        /// <param name="message">Message to be output.</param>
        /// <param name="level">Level of the message.</param>
        public void WriteLine(string message, OutputLevel level)
        {
            WriteLocking(message, level, newLine: true);
        }

        /// <summary>
        /// Writes the message with no new line.
        /// </summary>
        /// <param name="message">Message to be output.</param>
        /// <param name="level">Level of the message.</param>
        public void Write(string message, OutputLevel level)
        {
            WriteLocking(message, level, newLine: false);
        }

        private void WriteLocking(string message, OutputLevel level, bool newLine)
        {
            switch (level)
            {
                case OutputLevel.Information:
                case OutputLevel.Warning:
                    lock (Console.Out)
                    {
                        if (message == ".")
                        {
                            // when we write progress we always owe new line so that the next real message will write it
                            // but we never write it ourselves to keep writing dots on the same line
                            oweNewLine = true;
                            this.standardOutput.Write(message);
                        }
                        else
                        {
                            this.standardOutput.Write(!oweNewLine ? message : $"{Environment.NewLine}{message}");
                            this.oweNewLine = newLine;
                        }
                    }
                    break;

                case OutputLevel.Error:
                    lock (Console.Error)
                    {
                        this.standardError.Write(!oweNewLine ? message : $"{Environment.NewLine}{message}");
                        oweNewLine = newLine;
                    }
                    break;

                default:
                    lock (Console.Out)
                    {
                        var err = "ConsoleOutput.WriteLine: The output level is unrecognized: {0}";
                        this.standardOutput.Write(!oweNewLine ? err : $"{Environment.NewLine}{message}", level);
                        oweNewLine = newLine;
                    }
                    break;
            }
        }
    }
}
