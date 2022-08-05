// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

using Microsoft.VisualStudio.TestPlatform.ObjectModel;

namespace Microsoft.VisualStudio.TestPlatform.CommandLine.Processors;

internal class ArtifactProcessingCollectModeProcessor : ArgumentProcessor<bool>
{
    public ArtifactProcessingCollectModeProcessor()
        : base("/ArtifactsProcessingMode-Collect", typeof(ArtifactProcessingCollectModeProcessorExecutor))
    {
        // We put priority at the same level of the argument processor for runsettings passed as argument through cli.
        // We'll be sure to run before test run arg processor.
        Priority = ArgumentProcessorPriority.RunSettings;
        IsHiddenInHelp = true;
    }
}

internal enum ArtifactProcessingMode
{
    None,
    Collect,
    PostProcess
}

internal class ArtifactProcessingCollectModeProcessorExecutor : IArgumentExecutor
{
    private readonly CommandLineOptions _commandLineOptions;

    public ArtifactProcessingCollectModeProcessorExecutor(CommandLineOptions options)
    {
        _commandLineOptions = options ?? throw new ArgumentNullException(nameof(options));
    }

    public void Initialize(string? _)
    {
        _commandLineOptions.ArtifactProcessingMode = ArtifactProcessingMode.Collect;
        EqtTrace.Verbose($"ArtifactProcessingPostProcessModeProcessorExecutor.Initialize: ArtifactProcessingMode.Collect");
    }

    public ArgumentProcessorResult Execute() => ArgumentProcessorResult.Success;
}
