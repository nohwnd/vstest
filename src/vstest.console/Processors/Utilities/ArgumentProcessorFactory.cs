// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Reflection;

using Microsoft.VisualStudio.TestPlatform.CommandLine2;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.Utilities;

namespace Microsoft.VisualStudio.TestPlatform.CommandLine.Processors;

/// <summary>
/// Used to create the appropriate instance of an argument processor.
/// </summary>
internal class ArgumentProcessorFactory
{
    /// <summary>
    /// The command starter.
    /// </summary>
    internal const string CommandStarter = "/";

    /// <summary>
    /// The xplat command starter.
    /// </summary>
    internal const string XplatCommandStarter = "-";

    /// <summary>
    /// Creates ArgumentProcessorFactory.
    /// </summary>
    /// <param name="featureFlag">
    /// The feature flag support.
    /// </param>
    /// <returns>ArgumentProcessorFactory.</returns>
    internal static List<ArgumentProcessor> GetProcessorList(IFeatureFlag? featureFlag = null)
    {
        var enablePostProcessing = !(featureFlag ?? FeatureFlag.Instance).IsSet(FeatureFlag.DISABLE_ARTIFACTS_POSTPROCESSING);
        List<ArgumentProcessor> processors = new() {
            new SplashScreenArgumentProcessor(), // --no-logo
            new HelpArgumentProcessor(), // --help, when the help parameter is present, we should only print the help
            new EnableDiagArgumentProcessor(), // --diag, we want this to happen as soon as possible to the start so we can initialize diag logger
            new ResponseFileArgumentProcessor(),
            new RunSettingsArgumentProcessor(),
            new TestAdapterLoadingStrategyArgumentProcessor(),
            new ParentProcessIdArgumentProcessor(),
            new PortArgumentProcessor(),
        };

        if (enablePostProcessing)
        {
            processors.Add(new ArtifactProcessingCollectModeProcessor());
        }

        processors.AddRange(new ArgumentProcessor[] {
            new TestAdapterPathArgumentProcessor(),
            new PlatformArgumentProcessor(),
            new FrameworkArgumentProcessor(),
            new ParallelArgumentProcessor(),
            new ResultsDirectoryArgumentProcessor(),
            new InIsolationArgumentProcessor(),
            new CollectArgumentProcessor(),
            new EnableCodeCoverageArgumentProcessor(),
            new UseVsixExtensionsArgumentProcessor(),
            new CliRunSettingsArgumentProcessor(),
        });

        if (enablePostProcessing)
        {
            processors.Add(new TestSessionCorrelationIdProcessor());
        }

        processors.AddRange(new ArgumentProcessor[] {
            new EnvironmentArgumentProcessor(),
            new EnableLoggerArgumentProcessor(),
            new EnableBlameArgumentProcessor(),
            new TestSourceArgumentProcessor(),
            new TestCaseFilterArgumentProcessor(),
            new DisableAutoFakesArgumentProcessor(),
            new ListDiscoverersArgumentProcessor(),
            new ListExecutorsArgumentProcessor(),
            new ListLoggersArgumentProcessor(),
            new ListSettingsProvidersArgumentProcessor(),
            new ListTestsTargetPathArgumentProcessor(),
            new ListFullyQualifiedTestsArgumentProcessor(),
            new ListTestsArgumentProcessor(),
        });

        if (enablePostProcessing)
        {
            processors.Add(new ArtifactProcessingPostProcessModeProcessor());
        }

        processors.AddRange(new ArgumentProcessor[] {
            new RunSpecificTestsArgumentProcessor(),
            new RunTestsArgumentProcessor(),
        });

        // Get the ArgumentProcessorFactory
        return processors;
    }

    ///// <summary>
    ///// Decorates a lazy argument processor so that the real processor is initialized when the lazy value is obtained.
    ///// </summary>
    ///// <param name="processor">The lazy processor.</param>
    ///// <param name="initArg">The argument with which the real processor should be initialized.</param>
    ///// <returns>The decorated lazy processor.</returns>
    //public static ArgumentProcessor WrapLazyProcessorToInitializeOnInstantiation(ArgumentProcessor processor, string? initArg = null)
    //{
    //    var processorExecutor = processor.Executor;
    //    var lazyArgumentProcessor = new Lazy<IArgumentExecutor>(() =>
    //    {
    //        IArgumentExecutor? instance = null;
    //        try
    //        {
    //            instance = processorExecutor!.Value;
    //        }
    //        catch (Exception e)
    //        {
    //            EqtTrace.Error("ArgumentProcessorFactory.WrapLazyProcessorToInitializeOnInstantiation: Exception creating argument processor: {0}", e);
    //            throw;
    //        }

    //        try
    //        {
    //            instance.Initialize(initArg);
    //        }
    //        catch (Exception e)
    //        {
    //            EqtTrace.Error("ArgumentProcessorFactory.WrapLazyProcessorToInitializeOnInstantiation: Exception initializing argument processor: {0}", e);
    //            throw;
    //        }

    //        return instance;
    //    }, System.Threading.LazyThreadSafetyMode.PublicationOnly);
    //    processor.Executor = lazyArgumentProcessor;

    //    return processor;
    //}

    ///// <summary>
    ///// Decorates a lazy argument processor so that the real processor is initialized when the lazy value is obtained.
    ///// </summary>
    ///// <param name="processor">The lazy processor.</param>
    ///// <param name="initArg">The argument with which the real processor should be initialized.</param>
    ///// <returns>The decorated lazy processor.</returns>
    //private static ArgumentProcessor WrapLazyProcessorToInitializeOnInstantiation(
    //    ArgumentProcessor processor,
    //    string[] initArgs)
    //{
    //    var processorExecutor = processor.Executor;
    //    var lazyArgumentProcessor = new Lazy<IArgumentExecutor>(() =>
    //    {
    //        IArgumentsExecutor? instance = null;
    //        try
    //        {
    //            instance = (IArgumentsExecutor)processorExecutor!.Value;
    //        }
    //        catch (Exception e)
    //        {
    //            EqtTrace.Error("ArgumentProcessorFactory.WrapLazyProcessorToInitializeOnInstantiation: Exception creating argument processor: {0}", e);
    //            throw;
    //        }

    //        try
    //        {
    //            instance.Initialize(initArgs);
    //        }
    //        catch (Exception e)
    //        {
    //            EqtTrace.Error("ArgumentProcessorFactory.WrapLazyProcessorToInitializeOnInstantiation: Exception initializing argument processor: {0}", e);
    //            throw;
    //        }

    //        return instance;
    //    }, System.Threading.LazyThreadSafetyMode.PublicationOnly);
    //    processor.Executor = lazyArgumentProcessor;

    //    return processor;
    //}


    internal static IArgumentExecutor CreateExecutor(ArgumentProcessor processor, InvocationContext context, Type executorType)
    {
        if (processor is IExecutorCreator creator)
        {
            return creator.CreateExecutor(context);
        }

        var serviceProvider = context.ServiceProvider;

        // TODO: this is temporary, each processor should be responsible for creating the type from the context, or
        // be a composition root when it is a command. In tests we then should just try to create all of them and see if any fails,
        // no need to have separate test for each item.
        var ctors = executorType.GetConstructors().Concat(executorType.GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance));
        var longestCtor = ctors.OrderByDescending(c => c.GetParameters().Length).FirstOrDefault();
        if (longestCtor == null)
            throw new InvalidOperationException($"Type {executorType.FullName} has no accessible constructor.");

        var parameters = longestCtor.GetParameters();
        var instances = new object[parameters.Length];
        for (var i = 0; i < parameters.Length; i++)
        {
            var service = serviceProvider.GetService(parameters[i].ParameterType);
            if (service == null)
            {
                throw new InvalidOperationException($"Could not resolve service of type: '{parameters[i].ParameterType}' for executor {executorType}");
            }
            instances[i] = service;
        }

        return (IArgumentExecutor)longestCtor.Invoke(instances);
    }
}
