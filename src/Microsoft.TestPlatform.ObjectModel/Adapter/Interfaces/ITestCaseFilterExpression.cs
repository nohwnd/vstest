// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter
{
    using System;

    /// <summary>
    /// It represents expression for filtering test cases.
    /// </summary>
    [System.Obsolete]
    public interface ITestCaseFilterExpression
    {
        /// <summary>
        /// Gets original string for test case filter.
        /// </summary>
        string TestCaseFilterValue { get; }

        /// <summary>
        /// Matched test case with test case filtering criteria.
        /// </summary>
        bool MatchTestCase(TestCase testCase, Func<string, object> propertyValueProvider);
    }
}