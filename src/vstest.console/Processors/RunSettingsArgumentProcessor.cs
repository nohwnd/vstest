// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;

using Microsoft.VisualStudio.TestPlatform.Common.Interfaces;
using Microsoft.VisualStudio.TestPlatform.Common.Utilities;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Utilities;
using Microsoft.VisualStudio.TestPlatform.Utilities;
using Microsoft.VisualStudio.TestPlatform.Utilities.Helpers.Interfaces;

using CommandLineResources = Microsoft.VisualStudio.TestPlatform.CommandLine.Resources.Resources;

namespace Microsoft.VisualStudio.TestPlatform.CommandLine.Processors;

internal class RunSettingsArgumentProcessor : ArgumentProcessor<FileInfo>
{
    // TODO: add existing file validator
    public RunSettingsArgumentProcessor()
        : base(new string[] { "-s", "--settings" }, typeof(RunSettingsArgumentExecutor))
    {
        Priority = ArgumentProcessorPriority.RunSettings;
        HelpContentResourceName = CommandLineResources.RunSettingsArgumentHelp;
        HelpPriority = HelpContentPriority.RunSettingsArgumentProcessorHelpPriority;
    }
}

internal class RunSettingsArgumentExecutor : IArgumentExecutor
{
    private readonly CommandLineOptions _commandLineOptions;
    private readonly IRunSettingsProvider _runSettingsManager;
    private readonly IRunSettingsHelper _runsettingsHelper;

    private readonly IFileHelper _fileHelper;

    internal RunSettingsArgumentExecutor(CommandLineOptions commandLineOptions, IRunSettingsProvider runSettingsManager, IRunSettingsHelper runSettingsHelper, IFileHelper fileHelper)
    {
        _commandLineOptions = commandLineOptions;
        _runSettingsManager = runSettingsManager;
        _runsettingsHelper = runSettingsHelper;
        _fileHelper = fileHelper;
    }

    public void Initialize(ParseResult parseResult)
    {
        var argument = parseResult.GetValueFor(new RunSettingsArgumentProcessor())?.FullName;
        if (argument.IsNullOrWhiteSpace())
        {
            throw new CommandLineException(CommandLineResources.RunSettingsRequired);
        }

        if (!_fileHelper.Exists(argument))
        {
            throw new CommandLineException(
                string.Format(
                    CultureInfo.CurrentCulture,
                    CommandLineResources.RunSettingsFileNotFound,
                    argument));
        }

        Contract.EndContractBlock();

        // Load up the run settings and set it as the active run settings.
        try
        {
            XmlDocument document = GetRunSettingsDocument(argument);

            _runSettingsManager.UpdateRunSettings(document.OuterXml);

            // To determine whether to infer framework and platform.
            ExtractFrameworkAndPlatform();

            //Add default runsettings values if not exists in given runsettings file.
            _runSettingsManager.AddDefaultRunSettings();

            _commandLineOptions.SettingsFile = argument;

            if (_runSettingsManager.QueryRunSettingsNode("RunConfiguration.EnvironmentVariables") != null)
            {
                _commandLineOptions.InIsolation = true;
                _runSettingsManager.UpdateRunSettingsNode(InIsolationArgumentExecutor.RunSettingsPath, "true");
            }

            var testCaseFilter = _runSettingsManager.QueryRunSettingsNode("RunConfiguration.TestCaseFilter");
            if (testCaseFilter != null)
            {
                _commandLineOptions.TestCaseFilterValue = testCaseFilter;
            }
        }
        catch (XmlException exception)
        {
            throw new SettingsException(
                string.Format(CultureInfo.CurrentCulture, "{0} {1}", ObjectModel.Resources.CommonResources.MalformedRunSettingsFile, exception.Message),
                exception);
        }
    }

    private void ExtractFrameworkAndPlatform()
    {
        var framworkStr = _runSettingsManager.QueryRunSettingsNode(FrameworkArgumentExecutor.RunSettingsPath);
        Framework? framework = Framework.FromString(framworkStr);
        if (framework != null)
        {
            _commandLineOptions.TargetFrameworkVersion = framework;
        }

        var platformStr = _runSettingsManager.QueryRunSettingsNode(PlatformArgumentExecutor.RunSettingsPath);
        if (Enum.TryParse<Architecture>(platformStr, true, out var architecture))
        {
            _runsettingsHelper.IsDefaultTargetArchitecture = false;
            _commandLineOptions.TargetArchitecture = architecture;
        }
    }

    protected virtual XmlReader GetReaderForFile(string runSettingsFile)
    {
        return XmlReader.Create(new StringReader(File.ReadAllText(runSettingsFile, Encoding.UTF8)), XmlRunSettingsUtilities.ReaderSettings);
    }

    private XmlDocument GetRunSettingsDocument(string runSettingsFile)
    {
        XmlDocument runSettingsDocument;

        if (!MSTestSettingsUtilities.IsLegacyTestSettingsFile(runSettingsFile))
        {
            using XmlReader reader = GetReaderForFile(runSettingsFile);
            var settingsDocument = new XmlDocument();
            settingsDocument.Load(reader);
            ClientUtilities.FixRelativePathsInRunSettings(settingsDocument, runSettingsFile);
            runSettingsDocument = settingsDocument;
        }
        else
        {
            runSettingsDocument = XmlRunSettingsUtilities.CreateDefaultRunSettings();
            runSettingsDocument = MSTestSettingsUtilities.Import(runSettingsFile, runSettingsDocument);
        }

        return runSettingsDocument;
    }

    public ArgumentProcessorResult Execute()
    {
        // Nothing to do here, the work was done in initialization.
        return ArgumentProcessorResult.Success;
    }
}
