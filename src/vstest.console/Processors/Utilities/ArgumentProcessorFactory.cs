// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Reflection;

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

    ///// <summary>
    ///// Available argument processors.
    ///// </summary>
    //private Dictionary<string, ArgumentProcessor>? _commandToProcessorMap;
    //private Dictionary<string, ArgumentProcessor>? _specialCommandToProcessorMap;

    ///// Initializes the argument processor factory.
    ///// </summary>
    ///// <param name="argumentProcessors">
    ///// The argument Processors.
    ///// </param>
    ///// <param name="featureFlag">
    ///// The feature flag support.
    ///// </param>
    ///// <remarks>
    ///// This is not public because the static Create method should be used to access the instance.
    ///// </remarks>
    //protected ArgumentProcessorFactory(IEnumerable<ArgumentProcessor> argumentProcessors)
    //{
    //    ValidateArg.NotNull(argumentProcessors, nameof(argumentProcessors));
    //    AllArgumentProcessors = argumentProcessors;
    //}

    /// <summary>
    /// Creates ArgumentProcessorFactory.
    /// </summary>
    /// <param name="featureFlag">
    /// The feature flag support.
    /// </param>
    /// <returns>ArgumentProcessorFactory.</returns>
    internal static List<ArgumentProcessor> GetProcessorList(IFeatureFlag? featureFlag = null)
    {
        List<ArgumentProcessor> processors = new(DefaultArgumentProcessors);

        if (!(featureFlag ?? FeatureFlag.Instance).IsSet(FeatureFlag.DISABLE_ARTIFACTS_POSTPROCESSING))
        {
            processors.Add(new ArtifactProcessingCollectModeProcessor());
            processors.Add(new ArtifactProcessingPostProcessModeProcessor());
            processors.Add(new TestSessionCorrelationIdProcessor());
        }

        // Get the ArgumentProcessorFactory
        return processors;
    }

    ///// <summary>
    ///// Creates the argument processor associated with the provided command line argument.
    ///// The Lazy that is returned will initialize the underlying argument processor when it is first accessed.
    ///// </summary>
    ///// <param name="argument">Command line argument to create the argument processor for.</param>
    ///// <returns>The argument processor or null if one was not found.</returns>
    //public ArgumentProcessor? CreateArgumentProcessor(string argument)
    //{
    //    ValidateArg.NotNullOrWhiteSpace(argument, nameof(argument));

    //    // Parse the input into its command and argument parts.
    //    var pair = new CommandArgumentPair(argument);

    //    // Find the associated argument processor.
    //    CommandToProcessorMap.TryGetValue(pair.Command, out ArgumentProcessor? argumentProcessor);

    //    // If an argument processor was not found for the command, then consider it as a test source argument.
    //    if (argumentProcessor == null)
    //    {
    //        // Update the command pair since the command is actually the argument in the case of
    //        // a test source.
    //        pair = new CommandArgumentPair(TestSourceArgumentProcessor.CommandName, argument);

    //        argumentProcessor = SpecialCommandToProcessorMap[TestSourceArgumentProcessor.CommandName];
    //    }

    //    if (argumentProcessor != null)
    //    {
    //        argumentProcessor = WrapLazyProcessorToInitializeOnInstantiation(argumentProcessor, pair.Argument);
    //    }

    //    return argumentProcessor;
    //}

    private static IReadOnlyList<ArgumentProcessor> DefaultArgumentProcessors => new List<ArgumentProcessor> {
        new SplashScreenArgumentProcessor(), // --no-logo
        new HelpArgumentProcessor(), // --help, when the help parameter is present, we should only print the help
        new EnableDiagArgumentProcessor(), // --diag, we want this to happen as soon as possible to the start so we can initialize diag logger
        new TestSourceArgumentProcessor(),
        new ListTestsArgumentProcessor(),
        new RunTestsArgumentProcessor(),
        new RunSpecificTestsArgumentProcessor(),
        new TestAdapterPathArgumentProcessor(),
        new TestAdapterLoadingStrategyArgumentProcessor(),
        new TestCaseFilterArgumentProcessor(),
        new ParentProcessIdArgumentProcessor(),
        new PortArgumentProcessor(),
        new RunSettingsArgumentProcessor(),
        new PlatformArgumentProcessor(),
        new FrameworkArgumentProcessor(),
        new EnableLoggerArgumentProcessor(),
        new ParallelArgumentProcessor(),
        new CliRunSettingsArgumentProcessor(),
        new ResultsDirectoryArgumentProcessor(),
        new InIsolationArgumentProcessor(),
        new CollectArgumentProcessor(),
        new EnableCodeCoverageArgumentProcessor(),
        new DisableAutoFakesArgumentProcessor(),
        new ResponseFileArgumentProcessor(),
        new EnableBlameArgumentProcessor(),
        new UseVsixExtensionsArgumentProcessor(),
        new ListDiscoverersArgumentProcessor(),
        new ListExecutorsArgumentProcessor(),
        new ListLoggersArgumentProcessor(),
        new ListSettingsProvidersArgumentProcessor(),
        new ListFullyQualifiedTestsArgumentProcessor(),
        new ListTestsTargetPathArgumentProcessor(),
        new EnvironmentArgumentProcessor()
    };

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

    internal static IArgumentExecutor CreateExecutor<TExecutor>(IServiceProvider serviceProvider)
    {
        return CreateExecutor(serviceProvider, typeof(TExecutor));
    }


    internal static IArgumentExecutor CreateExecutor(IServiceProvider serviceProvider, Type executorType)
    {
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
