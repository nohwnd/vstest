// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.CommandLine.Processors
{
    using System;
    using System.Diagnostics.Contracts;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Security;
    using Microsoft.VisualStudio.TestPlatform.CommandLine.Processors.Utilities;
    using Microsoft.VisualStudio.TestPlatform.Common;
    using Microsoft.VisualStudio.TestPlatform.Common.Interfaces;
    using Microsoft.VisualStudio.TestPlatform.Common.Utilities;
    using CommandLineResources = Microsoft.VisualStudio.TestPlatform.CommandLine.Resources.Resources;

    /// <summary>
    /// Allows the user to specify a path to save test results.
    /// </summary>
    internal class ResultsDirectoryArgumentProcessor : IArgumentProcessor
    {
        #region Constants

        /// <summary>
        /// The name of the command line argument that the ListTestsArgumentExecutor handles.
        /// </summary>
        public const string CommandName = "/ResultsDirectory";

        private const string RunSettingsPath = "RunConfiguration.ResultsDirectory";
        #endregion

        private Lazy<IArgumentProcessorCapabilities> metadata;

        private Lazy<IArgumentExecutor> executor;

        /// <summary>
        /// Gets the metadata.
        /// </summary>
        public Lazy<IArgumentProcessorCapabilities> Metadata
        {
            get
            {
                if (this.metadata == null)
                {
                    this.metadata = new Lazy<IArgumentProcessorCapabilities>(() => new ResultsDirectoryArgumentProcessorCapabilities());
                }

                return this.metadata;
            }
        }

        /// <summary>
        /// Gets or sets the executor.
        /// </summary>
        public Lazy<IArgumentExecutor> Executor
        {
            get
            {
                if (this.executor == null)
                {
                    this.executor = new Lazy<IArgumentExecutor>(() => new ResultsDirectoryArgumentExecutor(CommandLineOptions.Instance, RunSettingsManager.Instance));
                }

                return this.executor;
            }

            set
            {
                this.executor = value;
            }
        }
    }

    /// <summary>
    /// The argument capabilities.
    /// </summary>
    internal class ResultsDirectoryArgumentProcessorCapabilities : BaseArgumentProcessorCapabilities
    {
        public override string CommandName => ResultsDirectoryArgumentProcessor.CommandName;

        public override bool AllowMultiple => false;

        public override bool IsAction => false;

        public override ArgumentProcessorPriority Priority => ArgumentProcessorPriority.AutoUpdateRunSettings;

        public override string HelpContentResourceName => CommandLineResources.ResultsDirectoryArgumentHelp;

        public override HelpContentPriority HelpPriority => HelpContentPriority.ResultsDirectoryArgumentProcessorHelpPriority;
    }

    /// <summary>
    /// The argument executor.
    /// </summary>
    internal class ResultsDirectoryArgumentExecutor : IArgumentExecutor
    {
        #region Fields

        /// <summary>
        /// Used for getting sources.
        /// </summary>
        private CommandLineOptions commandLineOptions;

        private IRunSettingsProvider runSettingsManager;

        public const string ResultsDirectoryPath = "RunConfiguration.ResultsDirectory";
        public const string CleanResultsDirectory = "RunConfiguration.CleanResultsDirectory";

        #endregion

        #region Constructor

        /// <summary>
        /// Default constructor.
        /// </summary>
        /// <param name="options"> The options. </param>
        /// <param name="testPlatform">The test platform</param>
        public ResultsDirectoryArgumentExecutor(CommandLineOptions options, IRunSettingsProvider runSettingsManager)
        {
            Contract.Requires(options != null);
            Contract.Requires(runSettingsManager != null);

            this.commandLineOptions = options;
            this.runSettingsManager = runSettingsManager;
        }

        #endregion

        #region IArgumentExecutor

        /// <summary>
        /// Initializes with the argument that was provided with the command.
        /// </summary>
        /// <param name="argument">Argument that was provided with the command.</param>
        public void Initialize(string argument)
        {
            string exceptionMessage = string.Format(CultureInfo.CurrentUICulture, CommandLineResources.ResultsDirectoryValueRequired, argument);

            if (string.IsNullOrWhiteSpace(argument))
            {
                throw new CommandLineException(exceptionMessage);
            }

            // Throw error in case argument is null or empty.
            if (string.IsNullOrWhiteSpace(argument))
            {
                throw new CommandLineException(exceptionMessage);
            }

            var arguments = ArgumentProcessorUtilities.GetArgumentList(argument, ArgumentProcessorUtilities.SemiColonArgumentSeparator, exceptionMessage);
            
            var path = arguments[0];
            if (path.Contains("="))
            {
                throw new CommandLineException(exceptionMessage);
            }

            // Throw error in case path is null or empty, even when we have other parameters
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new CommandLineException(exceptionMessage);
            }

            // Get other parameters
            var parameters = arguments.Skip(1);
            var parsedParameters = ArgumentProcessorUtilities.GetArgumentParameters(parameters, ArgumentProcessorUtilities.EqualNameValueSeparator, exceptionMessage);

            var clean = false;
            if (parsedParameters.Any())
            {
                if (parsedParameters.TryGetValue("Clean", out var value))
                {
                    bool.TryParse(value, out clean);
                }
            }

            try
            {
                if (!Path.IsPathRooted(path))
                {
                    path = Path.GetFullPath(path);
                }

                if (clean)
                {
                    Directory.Delete(path, true);
                }

                Directory.CreateDirectory(path);
            }
            catch (Exception ex) when( ex is NotSupportedException || ex is SecurityException || ex is ArgumentException || ex is PathTooLongException || ex is IOException)
            {
                throw new CommandLineException(string.Format(CommandLineResources.InvalidResultsDirectoryPathCommand, path, ex.Message));
            }

            this.commandLineOptions.ResultsDirectory = path;
            this.commandLineOptions.CleanResultsDirectory = clean;
            this.runSettingsManager.UpdateRunSettingsNode(ResultsDirectoryArgumentExecutor.ResultsDirectoryPath, path);
            this.runSettingsManager.UpdateRunSettingsNode(ResultsDirectoryArgumentExecutor.CleanResultsDirectory, clean.ToString());
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

        #endregion
    }
}
