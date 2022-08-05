// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;

using Microsoft.VisualStudio.TestPlatform.CrossPlatEngine.ArtifactProcessing;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Engine;
using Microsoft.VisualStudio.TestPlatform.Utilities;

namespace Microsoft.VisualStudio.TestPlatform.CommandLine.Processors;

internal class ArtifactProcessingPostProcessModeProcessor : ArgumentProcessor<bool>
{
    public ArtifactProcessingPostProcessModeProcessor()
        : base("/ArtifactsProcessingMode-PostProcess", typeof(ArtifactProcessingPostProcessModeProcessorExecutor))
    {
        // Was created like this:
        // new ArtifactProcessingPostProcessModeProcessorExecutor(CommandLineOptions.Instance,
        //        new ArtifactProcessingManager(CommandLineOptions.Instance.TestSessionCorrelationId)));

        IsCommand = true;
        IsHiddenInHelp = true;
    }


    // TODO: what? why?
    public static bool ContainsPostProcessCommand(string[]? args, IFeatureFlag? featureFlag = null)
        => !(featureFlag ?? FeatureFlag.Instance).IsSet(FeatureFlag.DISABLE_ARTIFACTS_POSTPROCESSING) &&
            (args?.Contains("--artifactsProcessingMode-postprocess", StringComparer.OrdinalIgnoreCase) == true ||
            args?.Contains("/ArtifactsProcessingMode-PostProcess", StringComparer.OrdinalIgnoreCase) == true);
}

internal class ArtifactProcessingPostProcessModeProcessorExecutor : IArgumentExecutor
{
    private readonly CommandLineOptions _commandLineOptions;
    private readonly IArtifactProcessingManager _artifactProcessingManage;

    public ArtifactProcessingPostProcessModeProcessorExecutor(CommandLineOptions options, IArtifactProcessingManager artifactProcessingManager)
    {
        _commandLineOptions = options ?? throw new ArgumentNullException(nameof(options));
        _artifactProcessingManage = artifactProcessingManager ?? throw new ArgumentNullException(nameof(artifactProcessingManager));
    }

    public void Initialize(string? _)
    {
        _commandLineOptions.ArtifactProcessingMode = ArtifactProcessingMode.PostProcess;
        EqtTrace.Verbose($"ArtifactProcessingPostProcessModeProcessorExecutor.Initialize: ArtifactProcessingMode.PostProcess");
    }

    public ArgumentProcessorResult Execute()
    {
        try
        {
            // We don't have async execution at the moment for the argument processors.
            // Anyway post processing could involve a lot of I/O and so we make some space
            // for some possible parallelization async/await and fair I/O for the callee.
            _artifactProcessingManage.PostProcessArtifactsAsync().Wait();
            return ArgumentProcessorResult.Success;
        }
        catch (Exception e)
        {
            EqtTrace.Error("ArtifactProcessingPostProcessModeProcessorExecutor: Exception during artifact post processing: " + e);
            return ArgumentProcessorResult.Fail;
        }
    }
}
