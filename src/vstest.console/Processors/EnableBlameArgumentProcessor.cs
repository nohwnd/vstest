// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml;

using Microsoft.VisualStudio.TestPlatform.CommandLine.Processors.Utilities;
using Microsoft.VisualStudio.TestPlatform.Common.Interfaces;
using Microsoft.VisualStudio.TestPlatform.Common.Utilities;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Utilities;
using Microsoft.VisualStudio.TestPlatform.Utilities;
using Microsoft.VisualStudio.TestPlatform.Utilities.Helpers.Interfaces;

using CommandLineResources = Microsoft.VisualStudio.TestPlatform.CommandLine.Resources.Resources;

namespace Microsoft.VisualStudio.TestPlatform.CommandLine.Processors;

internal class EnableBlameArgumentProcessor : ArgumentProcessor<string>
{
    public EnableBlameArgumentProcessor()
        : base(new[] {
            "--blame",
            // TODO: move these to their own processors so they can capture parameters
            // and then make this one execute always so it can pick up the values from them.
            //"--blame-crash",
            //"--blame-crash-dump-type",
            //"--blame-hang",
            //"--blame-hang-timeout",
            //"--blame-hang-dump-type",
            //"--blame-crash-collect-always",
        }, typeof(EnableBlameArgumentExecutor))
    {
        Priority = ArgumentProcessorPriority.Logging;
        HelpContentResourceName = CommandLineResources.EnableBlameUsage;
        HelpPriority = HelpContentPriority.EnableDiagArgumentProcessorHelpPriority;
    }
}

internal class EnableBlameArgumentExecutor : IArgumentExecutor
{
    private readonly IRunSettingsProvider _runSettingsManager;
    private readonly IOutput _output;
    private readonly IFileHelper _fileHelper;

    internal EnableBlameArgumentExecutor(IRunSettingsProvider runSettingsManager, IOutput output, IFileHelper fileHelper)
    {
        _runSettingsManager = runSettingsManager;
        _output = output;
        _fileHelper = fileHelper;
    }

    #region IArgumentExecutor

    /// <summary>
    /// Initializes with the argument that was provided with the command.
    /// </summary>
    /// <param name="argument">Argument that was provided with the command.</param>
    public void Initialize(ParseResult parseResult)
    {
        var argument = parseResult.GetValueFor(new EnableBlameArgumentProcessor());

        var enableDump = false;
        var enableHangDump = false;
        var exceptionMessage = string.Format(CultureInfo.CurrentCulture, CommandLineResources.InvalidBlameArgument, argument);
        Dictionary<string, string>? collectDumpParameters = null;

        if (!argument.IsNullOrWhiteSpace())
        {
            // Get blame argument list.
            var blameArgumentList = ArgumentProcessorUtilities.GetArgumentList(argument, ArgumentProcessorUtilities.SemiColonArgumentSeparator, exceptionMessage);
            Func<string, bool> isDumpCollect = a => Constants.BlameCollectDumpKey.Equals(a, StringComparison.OrdinalIgnoreCase);
            Func<string, bool> isHangDumpCollect = a => Constants.BlameCollectHangDumpKey.Equals(a, StringComparison.OrdinalIgnoreCase);

            // Get collect dump key.
            var hasCollectDumpKey = blameArgumentList.Any(isDumpCollect);
            var hasCollectHangDumpKey = blameArgumentList.Any(isHangDumpCollect);

            // Check if dump should be enabled or not.
            enableDump = hasCollectDumpKey;

            // Check if dump should be enabled or not.
            enableHangDump = hasCollectHangDumpKey;

            if (!enableDump && !enableHangDump)
            {
                _output.Warning(false, string.Format(CultureInfo.CurrentCulture, CommandLineResources.BlameIncorrectOption, argument));
            }
            else
            {
                // Get collect dump parameters.
                var collectDumpParameterArgs = blameArgumentList.Where(a => !isDumpCollect(a) && !isHangDumpCollect(a));
                collectDumpParameters = ArgumentProcessorUtilities.GetArgumentParameters(collectDumpParameterArgs, ArgumentProcessorUtilities.EqualNameValueSeparator, exceptionMessage);
            }
        }

        // Initialize blame.
        InitializeBlame(enableDump, enableHangDump, collectDumpParameters);
    }

    /// <summary>
    /// Executes the argument processor.
    /// </summary>
    /// <returns>The <see cref="ArgumentProcessorResult"/>.</returns>
    public ArgumentProcessorResult Execute()
    {
        // Nothing to do since we updated the logger and data collector list in initialize
        return ArgumentProcessorResult.Success;
    }

    /// <summary>
    /// Initialize blame.
    /// </summary>
    /// <param name="enableCrashDump">Enable dump.</param>
    /// <param name="blameParameters">Blame parameters.</param>
    private void InitializeBlame(bool enableCrashDump, bool enableHangDump, Dictionary<string, string>? collectDumpParameters)
    {
        string blameFriendlyName = "blame";

        // Add Blame Logger
        LoggerUtilities.AddLoggerToRunSettings(blameFriendlyName, null, _runSettingsManager);

        // Add Blame Data Collector
        CollectArgumentExecutor.AddDataCollectorToRunSettings(blameFriendlyName, _runSettingsManager, _fileHelper);


        // Add default run settings if required.
        if (_runSettingsManager.ActiveRunSettings?.SettingsXml == null)
        {
            _runSettingsManager.AddDefaultRunSettings();
        }
        var settings = _runSettingsManager.ActiveRunSettings?.SettingsXml;

        // Get results directory from RunSettingsManager
        var resultsDirectory = GetResultsDirectory(settings)!;

        // Get data collection run settings. Create if not present.
        var dataCollectionRunSettings = XmlRunSettingsUtilities.GetDataCollectionRunSettings(settings);
        dataCollectionRunSettings ??= new DataCollectionRunSettings();

        // Create blame configuration element.
        var xmlDocument = new XmlDocument();
        var outernode = xmlDocument.CreateElement("Configuration");
        var node = xmlDocument.CreateElement("ResultsDirectory");
        outernode.AppendChild(node);
        node.InnerText = resultsDirectory;

        // Add collect dump node in configuration element.
        if (enableCrashDump)
        {
            var dumpParameters = collectDumpParameters
                ?.Where(p => new[] { "CollectAlways", "DumpType" }.Contains(p.Key, StringComparer.OrdinalIgnoreCase))
                .ToDictionary(p => p.Key, p => p.Value, StringComparer.OrdinalIgnoreCase)
                ?? new Dictionary<string, string>();

            if (!dumpParameters.ContainsKey("DumpType"))
            {
                dumpParameters.Add("DumpType", "Full");
            }

            AddCollectDumpNode(dumpParameters, xmlDocument, outernode);
        }

        // Add collect hang dump node in configuration element.
        if (enableHangDump)
        {
            var hangDumpParameters = collectDumpParameters
                ?.Where(p => new[] { "TestTimeout", "HangDumpType" }.Contains(p.Key, StringComparer.OrdinalIgnoreCase))
                .ToDictionary(p => p.Key, p => p.Value, StringComparer.OrdinalIgnoreCase)
                ?? new Dictionary<string, string>();

            if (!hangDumpParameters.ContainsKey("TestTimeout"))
            {
                hangDumpParameters.Add("TestTimeout", TimeSpan.FromHours(1).TotalMilliseconds.ToString(CultureInfo.CurrentCulture));
            }

            if (!hangDumpParameters.ContainsKey("HangDumpType"))
            {
                hangDumpParameters.Add("HangDumpType", "Full");
            }

            AddCollectHangDumpNode(hangDumpParameters, xmlDocument, outernode);
        }

        // Add blame configuration element to blame collector.
        foreach (var item in dataCollectionRunSettings.DataCollectorSettingsList)
        {
            if (string.Equals(item.FriendlyName, blameFriendlyName))
            {
                item.Configuration = outernode;
            }
        }

        // Update run settings.
        _runSettingsManager.UpdateRunSettingsNodeInnerXml(Constants.DataCollectionRunSettingsName, dataCollectionRunSettings.ToXml().InnerXml);
    }

    /// <summary>
    /// Get results directory.
    /// </summary>
    /// <param name="settings">Settings xml.</param>
    /// <returns>Results directory.</returns>
    private static string? GetResultsDirectory(string? settings)
    {
        string? resultsDirectory = null;
        if (settings == null)
        {
            return resultsDirectory;
        }

        try
        {
            RunConfiguration runConfiguration = XmlRunSettingsUtilities.GetRunConfigurationNode(settings);
            resultsDirectory = RunSettingsUtilities.GetTestResultsDirectory(runConfiguration);
        }
        catch (SettingsException se)
        {
            EqtTrace.Error("EnableBlameArgumentProcessor: Unable to get the test results directory: Error {0}", se);
        }

        return resultsDirectory;
    }

    /// <summary>
    /// Adds collect dump node in outer node.
    /// </summary>
    /// <param name="parameters">Parameters.</param>
    /// <param name="xmlDocument">Xml document.</param>
    /// <param name="outernode">Outer node.</param>
    private static void AddCollectDumpNode(Dictionary<string, string>? parameters, XmlDocument xmlDocument, XmlElement outernode)
    {
        var dumpNode = xmlDocument.CreateElement(Constants.BlameCollectDumpKey);
        if (parameters != null && parameters.Count > 0)
        {
            foreach (KeyValuePair<string, string> entry in parameters)
            {
                var attribute = xmlDocument.CreateAttribute(entry.Key);
                attribute.Value = entry.Value;
                dumpNode.Attributes.Append(attribute);
            }
        }
        outernode.AppendChild(dumpNode);
    }

    /// <summary>
    /// Adds collect dump node in outer node.
    /// </summary>
    /// <param name="parameters">Parameters.</param>
    /// <param name="xmlDocument">Xml document.</param>
    /// <param name="outernode">Outer node.</param>
    private static void AddCollectHangDumpNode(Dictionary<string, string> parameters, XmlDocument xmlDocument, XmlElement outernode)
    {
        var dumpNode = xmlDocument.CreateElement(Constants.CollectDumpOnTestSessionHang);
        if (parameters != null && parameters.Count > 0)
        {
            foreach (KeyValuePair<string, string> entry in parameters)
            {
                var attribute = xmlDocument.CreateAttribute(entry.Key);
                attribute.Value = entry.Value;
                dumpNode.Attributes.Append(attribute);
            }
        }
        outernode.AppendChild(dumpNode);
    }

    #endregion
}
