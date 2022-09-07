// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.IO;

using Microsoft.VisualStudio.TestPlatform.CommandLine.Internal;
using Microsoft.VisualStudio.TestPlatform.PlatformAbstractions;
using Microsoft.VisualStudio.TestPlatform.PlatformAbstractions.Interfaces;
using Microsoft.VisualStudio.TestPlatform.Utilities;

using CommandLineResources = Microsoft.VisualStudio.TestPlatform.CommandLine.Resources.Resources;

namespace Microsoft.VisualStudio.TestPlatform.CommandLine.Processors;

internal class SplashScreenArgumentProcessor : ArgumentProcessor<bool>
{
    public SplashScreenArgumentProcessor()
        : base(new string[] { "--nologo", "--no-logo" }, typeof(SplashScreenArgumentExecutor))
    {
        AlwaysExecute = true;
        Priority = ArgumentProcessorPriority.Maximum;
    }
}

internal class SplashScreenArgumentExecutor : IArgumentExecutor
{
    private readonly IOutput _output;
    private readonly IProcessHelper _processHelper;
    private readonly IEnvironment _environment;

    public SplashScreenArgumentExecutor(IOutput output, IProcessHelper processHelper, IEnvironment environment)
    {
        _output = output;
        _processHelper = processHelper;
        _environment = environment;
    }

    public void Initialize(ParseResult parseResult)
    {
        var noLogo = parseResult.GetValueFor(new SplashScreenArgumentProcessor());

        if (noLogo)
        {
            // If user specified --no-logo via dotnet, do not print splash screen.
        }
        else if (parseResult.GetValueFor(new ArtifactProcessingPostProcessModeProcessor()))
        {
            // If we're postprocessing we don't need to show the splash
        }
        else
        {
            var isDiag = parseResult.TryGetValueFor(new EnableDiagArgumentProcessor(), out var _);
            PrintSplashScreen(isDiag);
        }
    }

    public ArgumentProcessorResult Execute()
    {
        return ArgumentProcessorResult.Success;
    }


    /// <summary>
    /// Displays the Company and Copyright splash title info immediately after launch
    /// </summary>
    private void PrintSplashScreen(bool isDiag)
    {
        string? assemblyVersion = Product.Version;
        if (!isDiag)
        {
            var end = Product.Version?.IndexOf("-release");

            if (end >= 0)
            {
                assemblyVersion = Product.Version?.Substring(0, end.Value);
            }
        }

        string assemblyVersionAndArchitecture = $"{assemblyVersion} ({_processHelper.GetCurrentProcessArchitecture().ToString().ToLowerInvariant()})";
        string commandLineBanner = string.Format(CultureInfo.CurrentCulture, CommandLineResources.MicrosoftCommandLineTitle, assemblyVersionAndArchitecture);
        _output.WriteLine(commandLineBanner, OutputLevel.Information);
        _output.WriteLine(CommandLineResources.CopyrightCommandLineTitle, OutputLevel.Information);
        PrintWarningIfRunningEmulatedOnArm64();
        _output.WriteLine(string.Empty, OutputLevel.Information);
    }



    /// <summary>
    /// Display a warning if we're running the runner on ARM64 but with a different current process architecture.
    /// </summary>
    private void PrintWarningIfRunningEmulatedOnArm64()
    {
        string nonARM64RunnerName = "vstest.console.exe";

        var currentProcessArchitecture = _processHelper.GetCurrentProcessArchitecture();
        if (Path.GetFileName(_processHelper.GetCurrentProcessFileName()) == nonARM64RunnerName &&
            _environment.Architecture == PlatformArchitecture.ARM64 &&
            currentProcessArchitecture != PlatformArchitecture.ARM64)
        {
            _output.Warning(false, CommandLineResources.WarningEmulatedOnArm64, currentProcessArchitecture.ToString().ToLowerInvariant());
        }
    }
}
