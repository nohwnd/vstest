// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

using Microsoft.VisualStudio.TestPlatform.Common.ExtensionFramework;
using Microsoft.VisualStudio.TestPlatform.Common.Interfaces;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

namespace Microsoft.VisualStudio.TestPlatform.Common;

/// <summary>
/// Manages the active run settings.
/// </summary>
internal class RunSettingsManager : IRunSettingsProvider
{
    private readonly IMessageLogger _messageLogger;
    private readonly TestPluginCache _testPluginCache;

    /// <summary>
    /// Default constructor.
    /// </summary>
    internal RunSettingsManager(IMessageLogger messageLogger, TestPluginCache testPluginCache)
    {
        ActiveRunSettings = new RunSettings(messageLogger, testPluginCache);
        _messageLogger = messageLogger;
        _testPluginCache = testPluginCache;
    }

    #region IRunSettingsProvider

    /// <summary>
    /// Gets the active run settings.
    /// </summary>
    public RunSettings ActiveRunSettings { get; private set; }

    #endregion

    /// <summary>
    /// Set the active run settings.
    /// </summary>
    /// <param name="runSettings">RunSettings to make the active Run Settings.</param>
    public void SetActiveRunSettings(RunSettings runSettings)
    {
        ActiveRunSettings = runSettings ?? throw new ArgumentNullException(nameof(runSettings));
    }

    public RunSettings CreateRunSettings()
    {
        return new RunSettings(_messageLogger, _testPluginCache);
    }

}
