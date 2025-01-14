// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.


using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MSTest1;

public class AzureHealthOwnerAttribute
    : AzureHealthPropertyAttribute
{
    public AzureHealthOwnerAttribute(string owner)
        : base("owner", owner)
    {
    }
}
public class AzureHealthPropertyAttribute
    : TestPropertyAttribute
{
    public AzureHealthPropertyAttribute(string name, string value)
        : base($"azure-health-{name}", value)
    {
    }
}

[TestClass]
public class UnitTest1
{
    [TestMethod]
    [TestProperty("some prop", "some value")]
    [AzureHealthOwner("Jakub")]
    [AzureHealthProperty("service", "my service")]
    public void TestMethod1_1()
    {
        // Thread.Sleep(1000);
    }
}
