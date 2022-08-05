// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

using Microsoft.VisualStudio.TestPlatform.CommandLine;

namespace Microsoft.VisualStudio.TestPlatform.CommandLine2;

internal class Option<T> : Option
{
    public Option(string name, string helpDescriptionResource)
        : this(new[] { name }, helpDescriptionResource)
    {
    }

    public Option(string[] aliases, string helpDescriptionResource)
        : base(aliases, helpDescriptionResource, new Argument<T>())
    {
    }
}

internal class Option
{
    // Unlike System.CommandLine we ignore case here.
    private protected readonly HashSet<string> _aliases = new(StringComparer.OrdinalIgnoreCase);

    internal Option(string[] aliases, string helpDescriptionResource, Argument argument)
    {
        if (aliases is null)
        {
            throw new ArgumentNullException(nameof(aliases));
        }

        if (aliases.Length == 0)
        {
            throw new ArgumentException("An option must have at least one alias.", nameof(aliases));
        }

        AddAliases(aliases);

        Name = GetLongestAlias();
        Description = helpDescriptionResource;
        Argument = argument;
    }

    private void AddAliases(string[] aliases)
    {
        foreach (var alias in aliases)
        {
            Option.ThrowIfAliasIsInvalid(alias);
            _aliases.Add(alias);
        }
    }

    private static void ThrowIfAliasIsInvalid(string alias)
    {
        if (StringUtils.IsNullOrWhiteSpace(alias))
        {
            throw new ArgumentException("An alias cannot be null, empty, or consist entirely of whitespace.");
        }

        for (var i = 0; i < alias.Length; i++)
        {
            if (char.IsWhiteSpace(alias[i]))
            {
                throw new ArgumentException($"Alias cannot contain whitespace: '{alias}'", nameof(alias));
            }
        }
    }

    public string Name { get; }
    public string Description { get; }

    public Argument Argument { get; }

    public IReadOnlyCollection<string> Aliases => _aliases;

    private string GetLongestAlias()
    {
        // Same as in System.CommandLine a small bug when you register --a and /ab
        // you get back 'a' because the length of prefix is not considered.
        var max = string.Empty;
        foreach (var alias in _aliases)
        {
            if (alias.Length > max.Length)
            {
                max = alias;
            }
        }

        return max.RemoveArgumentPrefix();
    }
}

public static class CommandLine2StringExtensions
{
    public static string RemoveArgumentPrefix(this string option)
    {
        // for /abc syntax
        if (option[0] == '/')
        {
            return option.Substring(1, option.Length - 2);
        }

        // for --abc syntax
        if (option[0] == '-' && option[1] == '-')
        {
            return option.Substring(2, option.Length - 3);
        }

        // for -abc syntax
        if (option[0] == '-' && option[1] == '-')
        {
            return option.Substring(1, option.Length - 2);
        }

        return option;
    }
}
