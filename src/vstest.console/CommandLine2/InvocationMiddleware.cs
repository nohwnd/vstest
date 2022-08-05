// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;

namespace Microsoft.VisualStudio.TestPlatform.CommandLine2;

internal delegate Task InvocationMiddleware(InvocationContext context, Func<InvocationContext, Task> next);
