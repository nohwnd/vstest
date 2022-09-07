// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using FluentAssertions;

using Microsoft.VisualStudio.TestPlatform.CommandLine.Processors;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace vstest.console.UnitTests.Processors;

[TestClass]
public class ProcessorTests
{
    [TestMethod]
    public void ProcessorsHaveCorrectPriority()
    {
        var expected = new Type[]
        {
            typeof(SplashScreenArgumentProcessor), // should be first to print splash screen first
            typeof(HelpArgumentProcessor), // we have special case for this, no matter where it is we write help and return if we see --help
            typeof(EnableDiagArgumentProcessor), // enable diagnostics as soon as possible
            typeof(ResponseFileArgumentProcessor),
            typeof(RunSettingsArgumentProcessor), // populate settings early, so any subsequest change can change the user provided settings
            typeof(TestAdapterLoadingStrategyArgumentProcessor), // needs runsettings, but should be before port processor, because that initializes TestPlatform, it also sets /InIsolation
            typeof(ParentProcessIdArgumentProcessor),
            typeof(PortArgumentProcessor), // loggers depend on designmode value, should be before loggers
            typeof(ArtifactProcessingCollectModeProcessor),
            typeof(TestAdapterPathArgumentProcessor),
            typeof(PlatformArgumentProcessor),
            typeof(FrameworkArgumentProcessor),
            typeof(ParallelArgumentProcessor),
            typeof(ResultsDirectoryArgumentProcessor),
            typeof(InIsolationArgumentProcessor),
            typeof(CollectArgumentProcessor),
            typeof(EnableCodeCoverageArgumentProcessor),
            typeof(UseVsixExtensionsArgumentProcessor),
            typeof(CliRunSettingsArgumentProcessor),
            typeof(TestSessionCorrelationIdProcessor),
            typeof(EnvironmentArgumentProcessor), // warns about overridden runsettings must be after RunSettings, also it sets /InIsolation
            typeof(EnableLoggerArgumentProcessor),
            typeof(EnableBlameArgumentProcessor),
            typeof(TestSourceArgumentProcessor),
            typeof(TestCaseFilterArgumentProcessor),
            typeof(DisableAutoFakesArgumentProcessor),
            typeof(ListDiscoverersArgumentProcessor),
            typeof(ListExecutorsArgumentProcessor),
            typeof(ListLoggersArgumentProcessor),
            typeof(ListSettingsProvidersArgumentProcessor),
            typeof(ListTestsTargetPathArgumentProcessor),
            typeof(ListFullyQualifiedTestsArgumentProcessor),
            typeof(ListTestsArgumentProcessor),
            typeof(ArtifactProcessingPostProcessModeProcessor),
            typeof(RunSpecificTestsArgumentProcessor),
            typeof(RunTestsArgumentProcessor),
        };

        var processors = ArgumentProcessorFactory.GetProcessorList().Select(p => p.GetType());

        processors.Select(p => p.Name).Should().ContainInOrder(expected.Select(p => p.Name));
        processors.Should().HaveCount(expected.Length);
    }
}
