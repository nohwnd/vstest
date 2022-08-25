// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;

using Microsoft.VisualStudio.TestPlatform.Client.RequestHelper;
using Microsoft.VisualStudio.TestPlatform.Common.Interfaces;
using Microsoft.VisualStudio.TestPlatform.Utilities;

using CommandLineResources = Microsoft.VisualStudio.TestPlatform.CommandLine.Resources.Resources;

namespace Microsoft.VisualStudio.TestPlatform.CommandLine.Processors;

/// <summary>
/// The argument processor for initializing the vsix based adapters.
/// </summary>
internal class UseVsixExtensionsArgumentProcessor : ArgumentProcessor<bool>
{
    public UseVsixExtensionsArgumentProcessor()
        : base("--UseVsixExtensions", typeof(UseVsixExtensionsArgumentExecutor))
    {
        Priority = ArgumentProcessorPriority.AutoUpdateRunSettings;
        IsHiddenInHelp = true;
    }
}

internal class UseVsixExtensionsArgumentExecutor : IArgumentExecutor
{
    private readonly CommandLineOptions _commandLineOptions;
    private readonly ITestRequestManager _testRequestManager;
    private readonly IVSExtensionManager _extensionManager;
    private readonly IOutput _output;

    internal UseVsixExtensionsArgumentExecutor(CommandLineOptions commandLineOptions, ITestRequestManager testRequestManager, IVSExtensionManager extensionManager, IOutput output)
    {
        _commandLineOptions = commandLineOptions;
        _testRequestManager = testRequestManager;
        _extensionManager = extensionManager;
        _output = output;
    }

    /// <inheritdoc />
    public void Initialize(string? argument)
    {
        if (argument.IsNullOrWhiteSpace())
        {
            throw new CommandLineException(string.Format(CultureInfo.CurrentCulture, CommandLineResources.UseVsixExtensionsValueRequired));
        }

        if (!bool.TryParse(argument, out bool value))
        {
            throw new CommandLineException(
                string.Format(CultureInfo.CurrentCulture, CommandLineResources.InvalidUseVsixExtensionsCommand, argument));
        }

        _output.Warning(false, string.Format(CultureInfo.CurrentCulture, CommandLineResources.UseVsixExtensionsDeprecation));
        _commandLineOptions.UseVsixExtensions = value;

        if (_commandLineOptions.UseVsixExtensions)
        {
            var vsixExtensions = _extensionManager.GetUnitTestExtensions();
            _testRequestManager.InitializeExtensions(vsixExtensions, skipExtensionFilters: true);
        }
    }

    /// <inheritdoc />
    public ArgumentProcessorResult Execute()
    {
        return ArgumentProcessorResult.Success;
    }
}
