// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.VisualStudio.TestPlatform.CommandLine.Processors;

/// <summary>
/// Defines an argument, parameter or a command that is provided on commandline.
/// Argument: An argument is a standalone value or values that are provided:
/// e.g. vstest.console.exe test.dll --platform x86 -- RunConfiguration.MaxCpuCount=0
///                         ^------^
///
/// Parameter: A parameter is a named flag that can be paired with a value or values, in case of
/// boolean paramters providing the value is not necessary. All -, --  and / prefixes are considered
/// for a parameter.
/// e.g. vstest.console.exe test.dll --platform x86 -- RunConfiguration.MaxCpuCount=0
///                                  ^------------^
/// '--' Is a special type of parameter that does not have a name, and is used to escape everything
/// after to be taken as literal text to provide inline runsettings.
///
/// Command: A command is an action that takes the results of parameter processing, does the associtated action
/// and then terminates the whole chain. In every invocation there can be only one command. A command is either implicit
/// or represented by a parameter e.g. RunTests is a default command that will run tests on command line, and --port
/// is a non-implicit action that starts a server and waits for commands. Once --port is finished, the execution does not
/// continue to run to RunTests.
/// </summary>
internal class ArgumentProcessor<TValue> : ArgumentProcessor
{
    public ArgumentProcessor(string name, Type executorType) : this(new[] { name }, executorType)
    {
    }

    public ArgumentProcessor(string[] aliases, Type executorType) : base(aliases, executorType, typeof(TValue))
    {
    }
}

internal abstract class ArgumentProcessor
{
    private readonly HashSet<string> _aliases = new(StringComparer.OrdinalIgnoreCase);

    protected ArgumentProcessor(string[] aliases, Type executorType, Type valueType)
    {
        //TODO: validate not empty, not null, at least 1 and no spaces in any of the aliases

        foreach (var alias in aliases)
        {
            _aliases.Add(alias.TrimStart('-').TrimStart('/'));
        }

        if (!typeof(IArgumentExecutor).IsAssignableFrom(executorType))
        {
            throw new ArgumentException($"Provided executor type {executorType.GetType()} must derive from {nameof(IArgumentExecutor)}.");
        }

        ExecutorType = executorType;
        ValueType = valueType;

        Name = GetLongestAlias();
    }

    /// <summary>
    /// All the names to use for this command, the longest one (without prefix) is used as the name of the command.
    /// Aliases can be provided with prefix to make them look more familiar in code.
    /// When saved and compared the prefix is ignored, as well as case.
    /// </summary>
    public IReadOnlyCollection<string> Aliases => _aliases;

    /// <summary>
    /// The long name (without the prefix), e.g. parallel, for --parallel.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Indicates if multiple values can be provided to this processor, this includes providing mulitiple items, as well as providing the parameter multiple times on the commandline.
    /// </summary>
    public bool AllowMultiple { get; init; }

    /// <summary>
    /// Indicates that this is hidden and cannot be specified on the command line,
    /// this is useful for implicit action like RunTests.
    /// </summary>
    public bool IsHidden { get; init; }

    /// <summary>
    /// Indicates if the processor should always be executed even if the parameter for it is not provided. This is useful for always doing a transformation
    /// on the parameters or state, but it does not allow invoking multiple commands per invocation.
    /// </summary>
    public bool AlwaysExecute { get; init; }

    /// <summary>
    /// Indicates if the argument processor is a command and so it will terminate the processing.
    /// </summary>
    public bool IsCommand { get; init; }

    /// <summary>
    /// Indicates the priority of the argument processor.
    /// The priority determines the order in which processors are initialized and executed.
    /// </summary>
    public ArgumentProcessorPriority Priority { get; init; } = ArgumentProcessorPriority.Normal;

    /// <summary>
    /// When true the parameter is shown in help, otherwise it is not.
    /// </summary>
    public bool IsHiddenInHelp { get; init; }

    /// <summary>
    /// The resource identifier for the Help Content associated with the decorated argument processor
    /// </summary>
    public string? HelpContentResourceName { get; init; }

    /// <summary>
    /// Based on this enum, corresponding help text will be shown.
    /// </summary>
    public HelpContentPriority HelpPriority { get; init; }

    public Type ExecutorType { get; }

    public Type ValueType { get; }

    public IReadOnlyList<Func<object, string>> Validators { get; internal set; }

    private string GetLongestAlias()
    {
        return _aliases.OrderBy(a => a.Length).First();
    }
}
