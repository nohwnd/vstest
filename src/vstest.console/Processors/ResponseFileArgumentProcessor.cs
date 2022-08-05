// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;

using CommandLineResources = Microsoft.VisualStudio.TestPlatform.CommandLine.Resources.Resources;

namespace Microsoft.VisualStudio.TestPlatform.CommandLine.Processors;

/// <summary>
///  An argument processor that allows the user to specify additional arguments from a response file.
///  for test run.
/// </summary>
internal class ResponseFileArgumentProcessor : ArgumentProcessor<FileInfo>
{
    // This has no executor, we use it to just get it to help, the parser needs to be aware of this special name
    // because it holds all the parameters.
    public ResponseFileArgumentProcessor()
        : base("@", typeof(NullExecutor))
    {
        // REVEW: not sure why.
        IsHidden = true;
        HelpContentResourceName = CommandLineResources.ResponseFileArgumentHelp;
        HelpPriority = HelpContentPriority.ResponseFileArgumentProcessorHelpPriority;
    }
}
