// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

using FluentAssertions;

using Microsoft.VisualStudio.TestPlatform.Common;
using Microsoft.VisualStudio.TestPlatform.CoreUtilities.Tracing.Interfaces;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Utilities;
using Microsoft.VisualStudio.TestPlatform.PlatformAbstractions;
using Microsoft.VisualStudio.TestPlatform.PlatformAbstractions.Interfaces;
using Microsoft.VisualStudio.TestPlatform.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Moq;

using CommandLineResources = Microsoft.VisualStudio.TestPlatform.CommandLine.Resources.Resources;

namespace Microsoft.VisualStudio.TestPlatform.CommandLine.UnitTests;

[TestClass]
public class ExecutorUnitTests
{
    private readonly Mock<ITestPlatformEventSource> _mockTestPlatformEventSource;

    public ExecutorUnitTests()
    {
        _mockTestPlatformEventSource = new Mock<ITestPlatformEventSource>();
    }

    /// <summary>
    /// Executor should Print splash screen first
    /// </summary>
    [TestMethod]
    public void ExecutorPrintsSplashScreen()
    {
        var mockOutput = new MockOutput();
        var exitCode = new Executor(mockOutput, _mockTestPlatformEventSource.Object, new ProcessHelper(), new PlatformEnvironment()).Execute("/badArgument");
        var assemblyVersion = typeof(Executor).GetTypeInfo().Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;

        Assert.AreEqual(1, exitCode, "Exit code must be One for bad arguments");

        // Verify that messages exist
        Assert.IsTrue(mockOutput.Messages.Count > 0, "Executor must print at least copyright info");
        Assert.IsNotNull(mockOutput.Messages.First().Message, "First Printed Message cannot be null or empty");

        // Just check first 20 characters - don't need to check whole thing as assembly version is variable
        // "First Printed message must be Microsoft Copyright");
        StringAssert.Contains(mockOutput.Messages.First().Message,
            CommandLineResources.MicrosoftCommandLineTitle.Substring(0, 20));

        var suffixIndex = assemblyVersion.IndexOf("-");
        var version = suffixIndex == -1 ? assemblyVersion : assemblyVersion.Substring(0, suffixIndex);
        StringAssert.Contains(mockOutput.Messages.First().Message,
            version);
    }

    [TestMethod]
    public void ExecutorShouldNotPrintsSplashScreenIfNoLogoPassed()
    {
        var mockOutput = new MockOutput();
        // TODO: Bad argument here needs to be provided because otherwise we get stuck. This is because there are shared static instances
        // of CommandLineOptions.Instance that has a valid source cross-pollinated from a test that run before, and
        // also RunSettingsManager.Instance.ActiveRunSettings have DesignMode = true, which puts the execution "port" like mode
        // so we start a server and never return. This needs to be fixed by not sharing instances, which is a difficult goal.
        // In the old code there was this same problem, but this test with nologo bailed early because the custom code for handling
        // --nologo removed it from the collection of arguments, and the execution ended with "no arguments provided".
        var exitCode = new Executor(mockOutput, _mockTestPlatformEventSource.Object, new ProcessHelper(), new PlatformEnvironment()).Execute("--nologo", "/badArgument");

        Assert.AreEqual(1, exitCode, "Exit code must be One for bad arguments");

        mockOutput.Messages.Select(m => m.Message)
            .Should().NotContainMatch($"*{CommandLineResources.MicrosoftCommandLineTitle}*");
    }

    /// <summary>
    /// Executor should Print Error message and Help contents when no arguments are provided.
    /// </summary>
    [TestMethod]
    public void ExecutorEmptyArgsPrintsErrorAndHelpMessage()
    {
        var mockOutput = new MockOutput();
        var exitCode = new Executor(mockOutput, _mockTestPlatformEventSource.Object, new ProcessHelper(), new PlatformEnvironment()).Execute(null);

        Assert.AreEqual(1, exitCode, "Exit code must be One when no arguments are provided.");

        Assert.IsTrue(mockOutput.Messages.Any(message => message.Message!.Contains(CommandLineResources.NoArgumentsProvided)));
    }

    [TestMethod]
    public void ExecutorWithInvalidArgsShouldPrintErrorMessage()
    {
        var mockOutput = new MockOutput();
        string badArg = "/badArgument";
        var exitCode = new Executor(mockOutput, _mockTestPlatformEventSource.Object, new ProcessHelper(), new PlatformEnvironment()).Execute(badArg);

        Assert.AreEqual(1, exitCode, "Exit code must be One when no arguments are provided.");

        Assert.IsTrue(mockOutput.Messages.Any(message => message.Message!.Contains(string.Format(CultureInfo.CurrentCulture, CommandLineResources.InvalidArgument, badArg))));
    }

    [TestMethod]
    public void ExecutorWithInvalidArgsShouldPrintHowToUseHelpOption()
    {
        var mockOutput = new MockOutput();
        string badArg = "--invalidArg";
        var exitCode = new Executor(mockOutput, _mockTestPlatformEventSource.Object, new ProcessHelper(), new PlatformEnvironment()).Execute(badArg);

        Assert.AreEqual(1, exitCode, "Exit code must be One when no arguments are provided.");

        Assert.IsTrue(mockOutput.Messages.Any(message => message.Message!.Contains(string.Format(CultureInfo.CurrentCulture, CommandLineResources.InvalidArgument, badArg))));
    }

    [TestMethod]
    public void ExecutorWithInvalidArgsAndValueShouldPrintErrorMessage()
    {
        var mockOutput = new MockOutput();
        string badParameter = "--invalidArg";
        string badArg = $"{badParameter:xyz}";
        var exitCode = new Executor(mockOutput, _mockTestPlatformEventSource.Object, new ProcessHelper(), new PlatformEnvironment()).Execute(badArg);

        Assert.AreEqual(1, exitCode, "Exit code must be One when no arguments are provided.");

        mockOutput.Messages.Select(m => m.Message).Should().Contain(string.Format(CultureInfo.CurrentCulture, CommandLineResources.InvalidArgument, badParameter));
    }

    /// <summary>
    /// Executor should set default runsettings value even there is no processor
    /// </summary>
    [TestMethod]
    public void ExecuteShouldInitializeDefaultRunsettings()
    {
        var mockOutput = new MockOutput();
        _ = new Executor(mockOutput, _mockTestPlatformEventSource.Object, new ProcessHelper(), new PlatformEnvironment()).Execute(null);
        RunConfiguration runConfiguration = XmlRunSettingsUtilities.GetRunConfigurationNode(RunSettingsManager.Instance.ActiveRunSettings.SettingsXml);
        Assert.AreEqual(Constants.DefaultResultsDirectory, runConfiguration.ResultsDirectory);
        Assert.AreEqual(Framework.DefaultFramework.ToString(), runConfiguration.TargetFramework!.ToString());
        Assert.AreEqual(Constants.DefaultPlatform, runConfiguration.TargetPlatform);
    }

    [TestMethod]
    public void ExecuteShouldInstrumentVsTestConsoleStart()
    {
        var mockOutput = new MockOutput();
        var exitCode = new Executor(mockOutput, _mockTestPlatformEventSource.Object, new ProcessHelper(), new PlatformEnvironment()).Execute(It.IsAny<string[]>());

        _mockTestPlatformEventSource.Verify(x => x.VsTestConsoleStart(), Times.Once);
    }

    [TestMethod]
    public void ExecuteShouldInstrumentVsTestConsoleStop()
    {
        var mockOutput = new MockOutput();
        var exitCode = new Executor(mockOutput, _mockTestPlatformEventSource.Object, new ProcessHelper(), new PlatformEnvironment()).Execute(It.IsAny<string[]>());

        _mockTestPlatformEventSource.Verify(x => x.VsTestConsoleStop(), Times.Once);
    }

    [TestMethod]
    public void ExecuteShouldExitWithErrorOnResponseFileException()
    {
        string[] args = { "@FileDoesNotExist.rsp" };
        var mockOutput = new MockOutput();

        var exitCode = new Executor(mockOutput, _mockTestPlatformEventSource.Object, new ProcessHelper(), new PlatformEnvironment()).Execute(args);

        var errorMessageCount = mockOutput.Messages.Count(msg => msg.Level == OutputLevel.Error && msg.Message!.Contains(
            string.Format(CultureInfo.CurrentCulture, CommandLineResources.OpenResponseFileError, args[0].Substring(1))));
        Assert.AreEqual(1, errorMessageCount, "Response File Exception should display error.");
        Assert.AreEqual(1, exitCode, "Response File Exception execution should exit with error.");
    }

    [TestMethod]
    public void ExecuteShouldNotThrowSettingsExceptionButLogOutput()
    {
        var activeRunSetting = RunSettingsManager.Instance.ActiveRunSettings;
        var runSettingsFile = Path.Combine(Path.GetTempPath(), "ExecutorShouldShowRightErrorMessage.runsettings");

        try
        {
            if (File.Exists(runSettingsFile))
            {
                File.Delete(runSettingsFile);
            }

            var fileContents = @"<RunSettings>
                                    <LoggerRunSettings>
                                        <Loggers>
                                            <Logger invalidName=""trx"" />
                                        </Loggers>
                                    </LoggerRunSettings>
                                </RunSettings>";

            File.WriteAllText(runSettingsFile, fileContents);

            var testSourceDllPath = Path.GetTempFileName();
            string[] args = { testSourceDllPath, "/settings:" + runSettingsFile };
            var mockOutput = new MockOutput();

            var exitCode = new Executor(mockOutput, _mockTestPlatformEventSource.Object, new ProcessHelper(), new PlatformEnvironment()).Execute(args);

            var result = mockOutput.Messages.Any(o => o.Level == OutputLevel.Error && o.Message!.Contains("Invalid settings 'Logger'. Unexpected XmlAttribute: 'invalidName'."));
            Assert.IsTrue(result, "expecting error message : Invalid settings 'Logger'.Unexpected XmlAttribute: 'invalidName'.");
        }
        finally
        {
            File.Delete(runSettingsFile);
            RunSettingsManager.Instance.SetActiveRunSettings(activeRunSetting);
        }
    }

    [TestMethod]
    public void ExecuteShouldReturnNonZeroExitCodeIfSettingsException()
    {
        var activeRunSetting = RunSettingsManager.Instance.ActiveRunSettings;
        var runSettingsFile = Path.Combine(Path.GetTempPath(), "ExecutorShouldShowRightErrorMessage.runsettings");

        try
        {
            if (File.Exists(runSettingsFile))
            {
                File.Delete(runSettingsFile);
            }

            var fileContents = @"<RunSettings>
                                    <LoggerRunSettings>
                                        <Loggers>
                                            <Logger invalidName=""trx"" />
                                        </Loggers>
                                    </LoggerRunSettings>
                                </RunSettings>";

            File.WriteAllText(runSettingsFile, fileContents);

            string[] args = { "/settings:" + runSettingsFile };
            var mockOutput = new MockOutput();

            var exitCode = new Executor(mockOutput, _mockTestPlatformEventSource.Object, new ProcessHelper(), new PlatformEnvironment()).Execute(args);

            Assert.AreEqual(1, exitCode, "Exit code should be one because it throws exception");
        }
        finally
        {
            File.Delete(runSettingsFile);
            RunSettingsManager.Instance.SetActiveRunSettings(activeRunSetting);
        }
    }

    [TestMethod]
    public void ExecutorShouldShowErrorMessageWhenValueForTargetPlatformIsNotAValidPlatform()
    {
        var activeRunSetting = RunSettingsManager.Instance.ActiveRunSettings;
        var runSettingsFile = Path.Combine(Path.GetTempPath(), "ExecutorShouldShowRightErrorMessage.runsettings");

        try
        {
            if (File.Exists(runSettingsFile))
            {
                File.Delete(runSettingsFile);
            }

            var invalidPlatform = "GZZ64";
            Enum.IsDefined(typeof(Architecture), invalidPlatform).Should().BeFalse("because we want to provide a value that is not defined in Architecture enum");

            var fileContents = $@"<RunSettings>
                                    <RunConfiguration>
                                        <TargetPlatform>{invalidPlatform}</TargetPlatform>
                                    </RunConfiguration>
                                </RunSettings>";

            File.WriteAllText(runSettingsFile, fileContents);

            string[] args = { "/settings:" + runSettingsFile };
            var mockOutput = new MockOutput();

            var exitCode = new Executor(mockOutput, _mockTestPlatformEventSource.Object, new ProcessHelper(), new PlatformEnvironment()).Execute(args);

            mockOutput.Messages
                .Where(m => m.Level == OutputLevel.Error)
                .Select(m => m.Message)
                .Should().ContainMatch($"*Invalid setting 'RunConfiguration'. Invalid value '{invalidPlatform}' specified for 'TargetPlatform'.*");

            Assert.AreEqual(1, exitCode, "Exit code should be 1 because execution exited with error.");
        }
        finally
        {
            File.Delete(runSettingsFile);
            RunSettingsManager.Instance.SetActiveRunSettings(activeRunSetting);
        }
    }

    [TestMethod]
    [TestCategory("Windows")]
    public void ExecutorShouldPrintWarningIfRunningEmulatedOnARM64()
    {
        var mockOutput = new MockOutput();
        Mock<IProcessHelper> processHelper = new();
        processHelper.Setup(x => x.GetCurrentProcessArchitecture()).Returns(PlatformArchitecture.X64);
        processHelper.Setup(x => x.GetCurrentProcessId()).Returns(0);
        processHelper.Setup(x => x.GetCurrentProcessFileName()).Returns(@"X:\vstest.console.exe");
        Mock<IEnvironment> environment = new();
        environment.Setup(x => x.Architecture).Returns(PlatformArchitecture.ARM64);

        var exitCode = new Executor(mockOutput, _mockTestPlatformEventSource.Object, processHelper.Object, environment.Object).Execute();
        var assemblyVersion = typeof(Executor).GetTypeInfo().Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;

        Assert.AreEqual("vstest.console.exe is running in emulated mode as x64. For better performance, please consider using the native runner vstest.console.arm64.exe.",
            mockOutput.Messages[2].Message);
        Assert.AreEqual(OutputLevel.Warning,
            mockOutput.Messages[2].Level);
    }

    [TestMethod]
    public void ExecutorShouldPrintRunnerArchitecture()
    {
        var mockOutput = new MockOutput();
        Mock<IProcessHelper> processHelper = new();
        processHelper.Setup(x => x.GetCurrentProcessArchitecture()).Returns(PlatformArchitecture.X64);
        processHelper.Setup(x => x.GetCurrentProcessId()).Returns(0);
        Mock<IEnvironment> environment = new();
        environment.Setup(x => x.Architecture).Returns(PlatformArchitecture.X64);

        var exitCode = new Executor(mockOutput, _mockTestPlatformEventSource.Object, processHelper.Object, environment.Object).Execute();
        var assemblyVersion = typeof(Executor).GetTypeInfo().Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;

        Assert.IsTrue(Regex.IsMatch(mockOutput.Messages[0].Message!, @"Microsoft \(R\) Test Execution Command Line Tool Version .* \(x64\)"));
        Assert.IsFalse(mockOutput.Messages.Any(message => message.Message!.Contains("vstest.console.exe is running in emulated mode")));
    }

    private class MockOutput : IOutput
    {
        public List<OutputMessage> Messages { get; set; } = new List<OutputMessage>();

        public void Write(string? message, OutputLevel level)
        {
            Messages.Add(new OutputMessage() { Message = message, Level = level });
        }

        public void WriteLine(string? message, OutputLevel level)
        {
            Messages.Add(new OutputMessage() { Message = message, Level = level });
        }
    }

    private class OutputMessage
    {
        public string? Message { get; set; } = "";
        public OutputLevel Level { get; set; }
    }
}
