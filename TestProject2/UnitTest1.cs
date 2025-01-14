// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.


[assembly: Property("nunit assembly", "nunit prop value")]

namespace TestProject2;

[Property("nunit fixture", "nunit prop value")]
public class Tests
{
    [SetUp]
    public void Setup()
    {
    }

    [Test]
    [Property("nunit test", "nunit prop value")]
    [Property("nunit test2", 123)]
    public void Test1()
    {
        Assert.Pass();
    }
}
