// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.CommandLine.Internal
{
    using Microsoft.VisualStudio.TestPlatform.Utilities;
    using System.Timers;

    /// <summary>
    /// Indicates the test run progress
    /// </summary>
    internal class ProgressIndicator : IProgressIndicator
    {
        private Timer timer;

        /// <summary>
        /// Used to output to the console
        /// </summary>
        public IOutput ConsoleOutput { get; }
        public bool IsRunning => timer?.Enabled ?? false;

        public ProgressIndicator(IOutput output)
        {
            this.ConsoleOutput = output;
        }

        /// <inheritdoc />
        public void Start()
        {
            if (timer == null)
            {
                this.timer = new Timer(1000);
                this.timer.Elapsed += (_, __) =>
                {
                    this.ConsoleOutput.Write(".", OutputLevel.Information);
                };
            }

            this.timer.Start();
        }

        /// <inheritdoc />
        public void Pause()
        {
            this.Stop();
        }

        /// <inheritdoc />
        public void Stop()
        {            
            this.timer?.Stop();
        }
    }
}
