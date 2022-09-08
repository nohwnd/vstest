// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

using FluentAssertions;

using Microsoft.VisualStudio.TestPlatform.CommandLine.Processors;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using CommandLineResources = Microsoft.VisualStudio.TestPlatform.CommandLine.Resources.Resources;

namespace Microsoft.VisualStudio.TestPlatform.CommandLine.UnitTests;

[TestClass]
public class ParserTests
{
    [TestClass]
    public class BindingTests
    {
        [TestMethod]
        [DataRow(null)]
        [DataRow(new string[0])]
        public void ErrorIsReportedWhenNoArgumentsAreProvided(string[]? args)
        {
            var parseResult = new Parser().Parse(args, new List<ArgumentProcessor>());
            parseResult.Errors.Should().ContainEquivalentOf(CommandLineResources.NoArgumentsProvided);
        }

        [TestMethod]
        // Edge cases.
        [Row(new[] { "--" }, new string[0])]
        [Row(new[] { "1.dll", "--" }, new string[0])]
        [Row(new[] { "--", "RunConfiguration.MaxCpuCount=1" }, new[] { "RunConfiguration.MaxCpuCount=1" })]
        // Together with args and parameters.
        [Row(
            new[] { "1.dll", "--", "RunConfiguration.MaxCpuCount=1" },
            new[] { "RunConfiguration.MaxCpuCount=1" })]
        [Row(
            new[] { "1.dll", "--", "RunConfiguration.MaxCpuCount=1", "RunConfiguration.Background=True" },
            new[] { "RunConfiguration.MaxCpuCount=1", "RunConfiguration.Background=True" })]
        [Row(
            new[] { "1.dll", "--switch", "--", "RunConfiguration.MaxCpuCount=1" },
            new[] { "RunConfiguration.MaxCpuCount=1" })]
        [Row(
            new[] { "1.dll", "--parameter", "value", "--", "RunConfiguration.MaxCpuCount=1" },
            new[] { "RunConfiguration.MaxCpuCount=1" })]
        [Row(
            new[] { "1.dll", "--parameter", "value", "value2", "--", "RunConfiguration.MaxCpuCount=1" },
            new[] { "RunConfiguration.MaxCpuCount=1" })]
        // Handling of spaces depends on what the user provides,
        // When they correctly use quotes -- RunConfiguration.TestResultDirectory="C:\My Path".
        [Row(
          new[] { "--", @"RunConfiguration.TestResultDirectory=C:\My Path" },
          new[] { @"RunConfiguration.TestResultDirectory=C:\My Path" })]
        // When they fail to use quotes -- RunConfiguration.TestResultDirectory=C:\My Path.
        [Row(
          new[] { "--", @"RunConfiguration.TestResultDirectory=C:\My", "Path" },
          new[] { @"RunConfiguration.TestResultDirectory=C:\My", "Path" })]
        public void InlineRunSettingsAreTakenAsWholeIntoOptions(string[] args, string[] expected)
        {
            var parseResult = new Parser().Parse(args, new List<ArgumentProcessor>(), ignoreExtraParameters: true);
            parseResult.Errors.Should().BeEmpty();
            parseResult.Options.Should().ContainInOrder(expected);
        }

        [TestMethod]
        [Row(
           new[] { "@file" }, "1.dll --parameter value -- RunConfiguration.MaxCpuCount=1",
           new[] { "1.dll", "--parameter", "value" }, new[] { "RunConfiguration.MaxCpuCount=1" })]
        [Row(
           new[] { "2.dll", "@file", "--parameter2", "value2", "--", "RunConfiguration.TargetPlatform=x86" },
            "1.dll --parameter value -- RunConfiguration.MaxCpuCount=1",
           new[] { "2.dll", /* from responses -> */ "1.dll", "--parameter", "value" /* <- */, "--parameter2", "value2" },
            new[] { /* from responses -> */ "RunConfiguration.MaxCpuCount=1",/* <- */ "RunConfiguration.TargetPlatform=x86" })]
        public void ResponseFileIsExpandedToParametersInPlaceAndOptionsAreAppendedInOrder(string[] arguments, string responses, string[] expectedArgs, string[] expectedOptions)
        {
            string? responseFile = null;
            try
            {
                responseFile = Path.GetTempFileName();
                File.WriteAllText(responseFile, responses);

                var args = arguments.Select(a => a == "@file" ? $"@{responseFile}" : a).ToArray();

                var parseResult = new Parser().Parse(args, new List<ArgumentProcessor>(), ignoreExtraParameters: true);
                parseResult.Errors.Should().BeEmpty();
                parseResult.Unbound.Should().ContainInOrder(expectedArgs);
                parseResult.Options.Should().ContainInOrder(expectedOptions);
            }
            finally
            {
                if (responseFile != null)
                {
                    File.Delete(responseFile);
                }
            }
        }

        [TestMethod]
        public void MultipleResponseFilesAreExpanded()
        {
            string? responseFile1 = null;
            string? responseFile2 = null;
            try
            {
                responseFile1 = Path.GetTempFileName();
                File.WriteAllText(responseFile1, "--parameter1 value1 -- Option1=1");

                responseFile2 = Path.GetTempFileName();
                File.WriteAllText(responseFile2, "--parameter2 value2 -- Option2=2");

                var parseResult = new Parser().Parse(new string[] { "1.dll", $"@{responseFile1}", $"@{responseFile2}", "--parameter3", "value3", "--", "Option3=3" },
                    new List<ArgumentProcessor>(), ignoreExtraParameters: true);
                parseResult.Errors.Should().BeEmpty();
                parseResult.Unbound.Should().ContainInOrder("1.dll", "--parameter1", "value1", "--parameter2", "value2", "--parameter3", "value3");
                parseResult.Options.Should().ContainInOrder("Option1=1", "Option2=2", "Option3=3");
            }
            finally
            {
                if (responseFile1 != null)
                {
                    File.Delete(responseFile1);
                }

                if (responseFile2 != null)
                {
                    File.Delete(responseFile2);
                }
            }
        }

        [TestMethod]
        // Edge cases
        [Row(new[] { "1.dll", "--parameter:" }, new[] { "1.dll", "--parameter", })]
        [Row(new[] { "1.dll", "--parameter:a:a:a" }, new[] { "1.dll", "--parameter", "a:a:a" })]
        // Normal usage
        [Row(new[] { "1.dll", "--parameter:aaa" }, new[] { "1.dll", "--parameter", "aaa", })]
        [Row(new[] { "1.dll", "--parameter:aaa", "--", "RunConfiguration.MaxCpuCount=1" }, new[] { "1.dll", "--parameter", "aaa", })]
        public void ParameterValuesThatAreJoinedByColonGetSplit(string[] args, string[] expected)
        {
            // -- Arrange

            // -- Act
            var parseResult = new Parser().Parse(args, new List<ArgumentProcessor>(), ignoreExtraParameters: true);

            // -- Assert
            parseResult.Errors.Should().BeEmpty();
            parseResult.Unbound.Should().ContainInOrder(expected);
        }

        [TestMethod]
        [Row(new[] { "1.dll", "2.dll", }, new[] { "1.dll", "2.dll", })]
        [Row(new[] { "1.dll", "--parameter", "value", }, new[] { "1.dll", "--parameter", "value", })]
        [Row(new[] { "--parameter", "value", }, new[] { "--parameter", "value", })]
        public void ArgumentsAndParametersArePutIntoUnboundWhenThereIsNoDefaultProcessor(string[] args, string[] expected)
        {
            var parseResult = new Parser().Parse(args, new List<ArgumentProcessor>(), ignoreExtraParameters: true);
            parseResult.Errors.Should().BeEmpty();
            parseResult.Unbound.Should().ContainInOrder(expected);
        }

        [TestMethod]
        // All arguments go to default processor
        [Row(new[] { "1.dll", "2.dll", }, new[] { "1.dll", "2.dll", })]
        // The first argument goes to default processor, rest goes to unknown-parameter,
        // because we don't know its arity.
        [Row(new[] { "1.dll", "--unknown-parameter", "value", }, new[] { "1.dll" })]
        [Row(new[] { "1.dll", "--unknown-parameter", "value", "value2" }, new[] { "1.dll" })]
        // The first argument goes to default processor, --known-parameter-multi-value takes value1 and value2.
        [Row(new[] { "1.dll", "--known-parameter-multi-value", "value", "value2" }, new[] { "1.dll" })]
        // The first argument goes to default processor, as well as value2 because --known-parameter only
        // takes 1 value.
        [Row(new[] { "1.dll", "--known-parameter", "value", "value2" }, new[] { "1.dll", "value2" })]
        // All arguments go to default processor, --known-bool-parameter takes no value, because "value" is not bool.
        [Row(new[] { "1.dll", "--known-parameter-bool", "value", "value2" }, new[] { "1.dll", "value", "value2" })]
        // 1.dll and value2 arguments go to default processor, --known-bool-parameter takes "true", because it is bool.
        [Row(new[] { "1.dll", "--known-parameter-bool", "true", "value2" }, new[] { "1.dll", "value2" })]
        public void ArgumentsAreBoundToDefaultProcessor(string[] args, string[] expected)
        {
            // -- Arrange
            var defaultProcessor = new ArgumentProcessor<string[]>("--default", typeof(NullArgumentExecutor)) { IsDefault = true };
            var knownParameter = new ArgumentProcessor<string>("--known-parameter", typeof(NullArgumentExecutor));
            var knownParameterMultiValue = new ArgumentProcessor<string>("--known-parameter-multi-value", typeof(NullArgumentExecutor));
            var knownParameterBool = new ArgumentProcessor<bool>("--known-parameter-bool", typeof(NullArgumentExecutor));
            var argumentProcessors = new List<ArgumentProcessor> { defaultProcessor, knownParameter, knownParameterMultiValue, knownParameterBool };

            // -- Act
            var parseResult = new Parser().Parse(args, argumentProcessors, ignoreExtraParameters: true);

            // -- Assert
            parseResult.Errors.Should().BeEmpty();
            parseResult.GetValueFor(defaultProcessor).Should().ContainInOrder(expected);
        }
    }

    [TestClass]
    public class ArityTests
    {
        [TestMethod]
        [Row(new[] { "--parameter", "value" }, "value")]
        [Row(new[] { "--parameter", "value", "value2" }, "value")]
        public void StringParameterWithArity1Takes1Value(string[] args, string expected)
        {
            // -- Arrange
            var parameter = new ArgumentProcessor<string>("--parameter", typeof(NullArgumentExecutor));
            var argumentProcessors = new List<ArgumentProcessor> { parameter };

            // -- Act
            var parseResult = new Parser().Parse(args, argumentProcessors, ignoreExtraParameters: true);

            // -- Assert
            parseResult.Errors.Should().BeEmpty();
            parseResult.GetValueFor(parameter).Should().Be(expected);
        }

        [TestMethod]
        public void NonBoolParameterWithNoValueReportsError()
        {
            // -- Arrange
            var parameter = new ArgumentProcessor<string>("--parameter", typeof(NullArgumentExecutor));
            var argumentProcessors = new List<ArgumentProcessor> { parameter };

            // -- Act
            var parseResult = new Parser().Parse(new[] { "--parameter" }, argumentProcessors, ignoreExtraParameters: true);

            // -- Assert
            parseResult.Errors.Should().ContainMatch("No value was provided for parameter '--parameter'*");
        }

        [TestMethod]
        [Row(new[] { "--parameter" }, true)]
        // False because --parameter is not present.
        [Row(new[] { "--some-other-parameter" }, false)]
        [Row(new[] { "--parameter", "true" }, true)]
        [Row(new[] { "--parameter", "false" }, false)]
        public void BoolParameterWithArity1Takes1Or0Values(string[] args, bool expected)
        {
            // -- Arrange
            var parameter = new ArgumentProcessor<bool>("--parameter", typeof(NullArgumentExecutor));
            var argumentProcessors = new List<ArgumentProcessor> { parameter };

            // -- Act
            var parseResult = new Parser().Parse(args, argumentProcessors, ignoreExtraParameters: true);

            // -- Assert
            parseResult.Errors.Should().BeEmpty();
            parseResult.GetValueFor(parameter).Should().Be(expected);
        }

        [TestMethod]
        public void BoolParameterWithMultipleArityThrows()
        {
            // -- Arrange
            var parameter = new ArgumentProcessor<bool[]>("--parameter", typeof(NullArgumentExecutor));
            var argumentProcessors = new List<ArgumentProcessor> { parameter };

            // -- Act & Assert
            new Parser().Invoking(p => p.Parse(new[] { "--parameter" }, argumentProcessors, ignoreExtraParameters: true))
                .Should().Throw<ArgumentException>()
                .And.Message.Should().Match("*boolean and allow multiple values, this is not allowed.");
        }

        [TestMethod]
        [Row(new[] { "--parameter", "value" }, "value", new string[] { })]
        // Normally we might would expect --parameter with multi arity to take as many values as it can
        // that follow the parameter, but that is not how the previous parser worked, or how System.CommandLine
        // works in dotnet test. So "value2" should end up in unbound.
        [Row(new[] { "--parameter", "value", "value2" }, "value", new string[] { "value2" })]
        [Row(new[] { "--parameter", "value", "value2", "--parameter", "value3" }, "value", new string[] { "value2" })]
        public void StringParameterWithMultipleArityTakes1ValueAfterEachSpecification(string[] args, string expected, string[] unbound)
        {
            // -- Arrange
            var parameter = new ArgumentProcessor<string[]>("--parameter", typeof(NullArgumentExecutor));
            var argumentProcessors = new List<ArgumentProcessor> { parameter };

            // -- Act
            var parseResult = new Parser().Parse(args, argumentProcessors, ignoreExtraParameters: true);

            // -- Assert
            parseResult.Errors.Should().BeEmpty();
            parseResult.GetValueFor(parameter).Should().ContainInOrder(new[] { expected });
            parseResult.Unbound.Should().ContainInOrder(unbound);
        }
    }

    [TestClass]
    public class FuzzinessTests
    {
        [TestMethod]
        // Match short aliases, but don't match
        // short-alias with long name prefix -- (test for that is below).
        [Row(new[] { "-l", "value" }, "value")]
        [Row(new[] { "/l", "value" }, "value")]
        [Row(new[] { "-lt", "value" }, "value")]
        [Row(new[] { "/lt", "value" }, "value")]
        // Match long name.
        [Row(new[] { "--list-tests", "value" }, "value")]
        [Row(new[] { "--listtests", "value" }, "value")]
        [Row(new[] { "/listtests", "value" }, "value")]
        public void ParameterIsFoundNoMatterWhichAliasOrPrefixIsUsed(string[] args, string expected)
        {
            // -- Arrange
            var parameter = new ArgumentProcessor<string>(new string[] { "-l", "-lt", "--listTests", "--list-tests" }, typeof(NullArgumentExecutor));
            var argumentProcessors = new List<ArgumentProcessor> { parameter };

            // -- Act
            var parseResult = new Parser().Parse(args, argumentProcessors, ignoreExtraParameters: true);

            // -- Assert
            parseResult.GetValueFor(parameter).Should().Be(expected);
        }

        [TestMethod]
        public void SingleDashPrefixDoesNotMatchDashDashAlias()
        {
            // -- Arrange
            var parameter = new ArgumentProcessor<string>(new string[] { "-l", "--list-tests" }, typeof(NullArgumentExecutor));
            var argumentProcessors = new List<ArgumentProcessor> { parameter };

            // -- Act
            var parseResult = new Parser().Parse(new string[] { "--l" }, argumentProcessors, ignoreExtraParameters: true);

            // -- Assert
            parseResult.Unbound.Should().Contain("--l", "because short-name prefixed with '--' should not match the short name");
        }

        [TestMethod]
        [Row(new[] { "--parameter", "value" }, "value")]
        [Row(new[] { "--Parameter", "value" }, "value")]
        [Row(new[] { "--PARAMETER", "value" }, "value")]
        [Row(new[] { "--pArAmEtEr", "value" }, "value")]
        public void ParameterIsFoundNoMatterWhatCaseTheNameUses(string[] args, string expected)
        {
            // -- Arrange
            var parameter = new ArgumentProcessor<string>("--parameter", typeof(NullArgumentExecutor));
            var argumentProcessors = new List<ArgumentProcessor> { parameter };

            // -- Act
            var parseResult = new Parser().Parse(args, argumentProcessors, ignoreExtraParameters: true);

            // -- Assert
            parseResult.GetValueFor(parameter).Should().Be(expected);
        }

        [TestMethod]
        [Row(new[] { "--parameter", "true" }, true)]
        [Row(new[] { "--parameter", "True" }, true)]
        [Row(new[] { "--parameter", "TRUE" }, true)]
        [Row(new[] { "--parameter", "tRuE" }, true)]
        [Row(new[] { "--parameter", "false" }, false)]
        [Row(new[] { "--parameter", "False" }, false)]
        [Row(new[] { "--parameter", "FALSE" }, false)]
        [Row(new[] { "--parameter", "faLsE" }, false)]
        public void BoolParameterIgnoresCaseOfTheProvidedValue(string[] args, bool expected)
        {
            // -- Arrange
            var parameter = new ArgumentProcessor<bool>("--parameter", typeof(NullArgumentExecutor));
            var argumentProcessors = new List<ArgumentProcessor> { parameter };

            // -- Act
            var parseResult = new Parser().Parse(args, argumentProcessors);

            // -- Assert
            parseResult.Errors.Should().BeEmpty();
            parseResult.GetValueFor(parameter).Should().Be(expected);
        }

        [TestMethod]
        [Row(new[] { "--parameter", "monday" }, DayOfWeek.Monday)]
        [Row(new[] { "--parameter", "Monday" }, DayOfWeek.Monday)]
        [Row(new[] { "--parameter", "MONDAY" }, DayOfWeek.Monday)]
        [Row(new[] { "--parameter", "mOnDaY" }, DayOfWeek.Monday)]
        public void EnumParameterIgnoresCaseOfTheProvidedValue(string[] args, DayOfWeek expected)
        {
            // -- Arrange
            var parameter = new ArgumentProcessor<DayOfWeek>("--parameter", typeof(NullArgumentExecutor));
            var argumentProcessors = new List<ArgumentProcessor> { parameter };

            // -- Act
            var parseResult = new Parser().Parse(args, argumentProcessors);

            // -- Assert
            parseResult.Errors.Should().BeEmpty();
            parseResult.GetValueFor(parameter).Should().Be(expected);
        }
    }

    [TestClass]
    public class ConversionTests
    {
        [TestMethod]
        public void ParameterIsConvertedToStringType()
        {
            // -- Arrange
            var parameter = new ArgumentProcessor<string>("--parameter", typeof(NullArgumentExecutor));
            var argumentProcessors = new List<ArgumentProcessor> { parameter };

            // -- Act
            var parseResult = new Parser().Parse(new[] { "--parameter", "value" }, argumentProcessors);

            // -- Assert
            parseResult.Errors.Should().BeEmpty();
            parseResult.GetValueFor(parameter).Should().BeEquivalentTo("value");
        }

        [TestMethod]
        public void ParameterIsConvertedToStringArrayType()
        {
            // -- Arrange
            var parameter = new ArgumentProcessor<string[]>("--parameter", typeof(NullArgumentExecutor));
            var argumentProcessors = new List<ArgumentProcessor> { parameter };

            // -- Act
            var parseResult = new Parser().Parse(new[] { "--parameter", "value", "--parameter", "value2" }, argumentProcessors);

            // -- Assert
            parseResult.Errors.Should().BeEmpty();
            parseResult.GetValueFor(parameter).Should().BeEquivalentTo("value", "value2");
        }

        [TestMethod]
        public void ParameterIsConvertedToBoolType()
        {
            // -- Arrange
            var parameter = new ArgumentProcessor<bool>("--parameter", typeof(NullArgumentExecutor));
            var argumentProcessors = new List<ArgumentProcessor> { parameter };

            // -- Act
            var parseResult = new Parser().Parse(new[] { "--parameter", "true" }, argumentProcessors);

            // -- Assert
            parseResult.Errors.Should().BeEmpty();
            parseResult.GetValueFor(parameter).Should().Be(true);
        }

        [TestMethod]
        public void ParameterIsConvertedToEnumType()
        {
            // -- Arrange
            var parameter = new ArgumentProcessor<DayOfWeek>("--parameter", typeof(NullArgumentExecutor));
            var argumentProcessors = new List<ArgumentProcessor> { parameter };

            // -- Act
            var parseResult = new Parser().Parse(new[] { "--parameter", "Monday" }, argumentProcessors);

            // -- Assert
            parseResult.Errors.Should().BeEmpty();
            parseResult.GetValueFor(parameter).Should().Be(DayOfWeek.Monday);
        }

        [TestMethod]
        public void ParameterIsConvertedToFileInfoType()
        {
            // -- Arrange
            var parameter = new ArgumentProcessor<FileInfo>("--parameter", typeof(NullArgumentExecutor));
            var argumentProcessors = new List<ArgumentProcessor> { parameter };

            // -- Act
            var parseResult = new Parser().Parse(new[] { "--parameter", "C:\\Windows\\notepad.txt" }, argumentProcessors);

            // -- Assert
            parseResult.Errors.Should().BeEmpty();
            parseResult.GetValueFor(parameter).Should().NotBeNull();
            parseResult.GetValueFor(parameter)?.FullName.Should().Be("C:\\Windows\\notepad.txt");
        }

        [TestMethod]
        public void ParameterIsConvertedToDirectoryInfoType()
        {
            // -- Arrange
            var parameter = new ArgumentProcessor<DirectoryInfo>("--parameter", typeof(NullArgumentExecutor));
            var argumentProcessors = new List<ArgumentProcessor> { parameter };

            // -- Act
            var parseResult = new Parser().Parse(new[] { "--parameter", "C:\\Windows\\" }, argumentProcessors);

            // -- Assert
            parseResult.Errors.Should().BeEmpty();
            parseResult.GetValueFor(parameter).Should().NotBeNull();
            parseResult.GetValueFor(parameter)?.FullName.Should().Be("C:\\Windows\\");
        }
    }

    /// <summary>
    /// Unlike DataRow, this allows us to provide two arrays and expands the values in them https://github.com/microsoft/testfx/issues/1180.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    internal class RowAttribute : Attribute, ITestDataSource
    {
        public IEnumerable<object[]> Data { get; private set; }

        public string? DisplayName { get; set; }

        public RowAttribute(object data1)
        {
            Data = new object[1][] { new[] { data1 } };
        }

        public RowAttribute(object data1, object data2)
        {
            Data = new object[1][] { new[] { data1, data2 } };
        }

        public RowAttribute(object data1, object data2, object data3)
        {
            Data = new object[1][] { new object[] { data1, data2, data3 } };
        }

        public RowAttribute(object data1, object data2, object data3, object data4)
        {
            Data = new object[1][] { new object[] { data1, data2, data3, data4 } };
        }

        public IEnumerable<object[]> GetData(MethodInfo methodInfo)
        {
            return Data;
        }

        public string GetDisplayName(MethodInfo methodInfo, object[] data)
        {
            if (!string.IsNullOrWhiteSpace(DisplayName))
            {
                return DisplayName!;
            }

            var stringBuilder = new StringBuilder(methodInfo.Name);
            stringBuilder.Append('(');

            // Add params.
            var first = true;
            foreach (var d in data)
            {
                if (!first)
                {
                    stringBuilder.Append(", ");
                }

                first = false;

                if (d.GetType().IsArray)
                {
                    stringBuilder.Append("@(");
                    var first2 = true;
                    foreach (var v in (IEnumerable)d)
                    {
                        if (!first2)
                        {
                            stringBuilder.Append(", ");
                        }

                        first2 = false;
                        stringBuilder.Append(v?.ToString() ?? "<null>");
                    }
                    stringBuilder.Append(')');
                }
                else
                {
                    stringBuilder.Append(d);
                }
            }

            stringBuilder.Append(')');

            return stringBuilder.ToString();
        }
    }

    internal class NullArgumentExecutor : IArgumentExecutor
    {
        public ArgumentProcessorResult Execute()
        {
            return ArgumentProcessorResult.Success;
        }

        public void Initialize(ParseResult _)
        {
        }
    }
}

