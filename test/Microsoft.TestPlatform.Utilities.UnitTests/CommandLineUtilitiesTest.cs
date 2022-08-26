// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

using FluentAssertions;

using Microsoft.VisualStudio.TestPlatform.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.TestPlatform.Utilities.Tests;

[TestClass]
public class CommandLineUtilitiesTest
{
    private static void VerifyCommandLineSplitter(string commandLine, string[] expected)
    {
        var errors = new List<string>();
        var actual = CommandLineUtilities.SplitCommandLine(commandLine, ref errors);

        errors.Should().BeEmpty();
        actual.Should().BeEquivalentTo(expected);
    }

    [TestMethod]
    public void TestCommandLineSplitter()
    {
        VerifyCommandLineSplitter("", Array.Empty<string>());
        VerifyCommandLineSplitter("/testadapterpath:\"c:\\Path\"", new[] { @"/testadapterpath:c:\Path" });
        VerifyCommandLineSplitter("/testadapterpath:\"c:\\Path\" /logger:\"trx\"", new[] { @"/testadapterpath:c:\Path", "/logger:trx" });
        VerifyCommandLineSplitter("/testadapterpath:\"c:\\Path\" /logger:\"trx\" /diag:\"log.txt\"", new[] { @"/testadapterpath:c:\Path", "/logger:trx", "/diag:log.txt" });
    }
}
