// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestPlatform.CommandLine.Processors;
using Microsoft.VisualStudio.TestPlatform.Utilities;

using CommandLineResources = Microsoft.VisualStudio.TestPlatform.CommandLine.Resources.Resources;

namespace Microsoft.VisualStudio.TestPlatform.CommandLine2;

//internal class Executor2
//{
//    public Executor2()
//    {

//    }

//    public async Task<int> ExecuteAsync()
//    {
//        List<OptionWithMiddleware> options = new()
//        {
//            new OptionWithMiddleware(new HelpArgumentProcessor(), FuncFactory.Create<IOutput>((output) => new HelpArgumentExecutor(output))),
//        };

//        var commands = new List<(Option, Func<IServiceProvider, IArgumentExecutor>)> {
//            (new Option<bool>(new string[] {"-?", "-h", "--help" }, CommandLineResources.HelpArgumentHelp),
//                FuncFactory.Create<IOutput>((output) => new HelpArgumentExecutor(output))),
//        };


//        var builder = new CommandLineBuilder();

//        foreach (var command in commands)
//        {
//            builder.AddOption(command.Option);
//            builder.AddMiddleware(command.Middleware);
//        }

//    }
//}

internal class ParamBuilder
{
    public ParamBuilder()
    {
    }
}

internal class FuncFactory
{
    public static Func<IServiceProvider, IArgumentExecutor> Create<TDependency>(Func<TDependency, IArgumentExecutor> argumentExecutorFactory)
    {
        return (sp) =>
        {
            var arguments = Resolve(sp, argumentExecutorFactory);
            return argumentExecutorFactory((TDependency)arguments[0]);
        };
    }

    public static Func<IServiceProvider, IArgumentExecutor> Create<TDependency1, TDependency2>(Func<TDependency1, TDependency2, IArgumentExecutor> argumentExecutorFactory)
    {
        return (sp) =>
        {
            var arguments = Resolve(sp, argumentExecutorFactory);
            return argumentExecutorFactory((TDependency1)arguments[0], (TDependency2)arguments[1]);
        };
    }

    static object[] Resolve(IServiceProvider serviceProvider, Delegate func)
    {
        Type[] argumentTypes = func.Method.GetGenericArguments();

        var services = new object[argumentTypes.Length];
        for (int i = 0; i < argumentTypes.Length; i++)
        {
            services[i] = serviceProvider.GetService(argumentTypes[i]);
        }

        return services;
    }
}


