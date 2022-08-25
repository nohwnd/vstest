// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

using Microsoft.VisualStudio.TestPlatform.CommandLine.Processors;

using Newtonsoft.Json.Linq;

using CommandLineResources = Microsoft.VisualStudio.TestPlatform.CommandLine.Resources.Resources;

namespace Microsoft.VisualStudio.TestPlatform.CommandLine;

internal class Parser
{
    public Parser()
    {
    }

    internal ParseResult Parse(string[]? args, IReadOnlyList<ArgumentProcessor> argumentProcessors, bool ignoreExtraParameters = false)
    {
        List<string> errors = new();

        // Ensure there are some arguments
        if (args == null || args.Length == 0)
        {
            errors.Add(CommandLineResources.NoArgumentsProvided);
            return new ParseResult
            {
                Parser = this,
                Processors = argumentProcessors,
                Errors = errors,
            };
        }

        List<string> argList = args.ToList();

        // Split to arguments and literal text that is after --.
        var (arguments, dashDashOptions) = GetDoubleDashOptions(argList);

        // Expand all arguments and options we get from @ files.
        var (expandedAruments, expandedOptions) = ExpandResponseFiles(arguments, dashDashOptions, ref errors);

        if (errors.Any())
        {
            return new ParseResult
            {
                Parser = this,
                Processors = argumentProcessors,
                Args = arguments,
                Options = expandedOptions,
                Errors = errors,
            };
        }

        // Find all parameters we can link to an argument processor, and connect them together.
        var (bound, unbound) = Parser.Bind(expandedAruments, argumentProcessors, ref errors);
        if (errors.Any())
        {
            return new ParseResult
            {
                Parser = this,
                Processors = argumentProcessors,
                Args = expandedAruments,
                Options = expandedOptions,
                Errors = errors,
            };
        }

        // Convert all values to their final types.
        var typed = Parser.Convert(bound, ref errors);
        if (errors.Any())
        {
            return new ParseResult
            {
                Parser = this,
                Processors = argumentProcessors,
                Args = expandedAruments,
                Options = expandedOptions,
                Bound = bound,
                Unbound = unbound,
                Errors = errors,
            };
        }

        // Validate all parameters
        var validated = Parser.ValidateParseResult(bound, unbound, argumentProcessors, ignoreExtraParameters, ref errors);

        if (errors.Any())
        {
            return new ParseResult
            {
                Parser = this,
                Processors = argumentProcessors,
                Args = expandedAruments,
                Bound = bound,
                Unbound = unbound,
                Typed = typed,
                Options = expandedOptions,
                Errors = errors,
            };
        }

        return new ParseResult
        {
            Parser = this,
            Processors = argumentProcessors,
            Args = expandedAruments,
            Bound = bound,
            Unbound = unbound,
            Typed = typed,
            Options = expandedOptions,
            Errors = errors,
        };
    }

    private static (List<BoundParameter> bound, List<string> unbound) Bind(List<string> args, IReadOnlyList<ArgumentProcessor> argumentProcessors, ref List<string> bindingErrors)
    {
        var unbound = new List<string>();

        var allowMultipleBoolProcessors = argumentProcessors.Where(a => a.AllowMultiple && a.ValueType == typeof(bool[]));
        if (allowMultipleBoolProcessors.Any())
        {
            // We don't allow parameters that allow multiple values to take boolean because it complicates the parsing little bit.
            throw new ArgumentException($"Argument processor(s) '{string.Join("', '", allowMultipleBoolProcessors.Select(a => a.Name))}' take boolean and allow multiple values, this is not allowed.");
        }

        var aliasesToProcessorMap = new Dictionary<string, ArgumentProcessor>(StringComparer.OrdinalIgnoreCase);
        foreach (var argumentProcessor in argumentProcessors)
        {
            foreach (var alias in argumentProcessor.Aliases)
            {
                aliasesToProcessorMap[alias] = argumentProcessor;
            }
        }

        // default argument consumer (we have only one, /RunTests). TODO: make this more generic to assign to a single argument, (or multiple if we want to have zeroOrOneArity).
        var hasDefaultArgument = aliasesToProcessorMap.ContainsKey(NormalizeParameterName("--RunTests", out var _));
        var defaultArgument = hasDefaultArgument ? aliasesToProcessorMap[NormalizeParameterName("--RunTests", out var _)] : null;

        var tokenized = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        // We consume the arguments from the list starting at the left side of the command line.
        // If we see value that starts with the argument prefix
        // then we see that we found some --parameter and we need to find definition for it to know
        // what is the arity (how many arguments it can take). We then try to consume as many arguments
        // as the parameter can consume.
        //
        // When the parameter is not found, it will consume all the arguments that follow it until the next parameter,
        // this is necessary to avoid putting values of unknown parameters with AllowMultiple into the default argument.
        //
        // We only specify arity by saying "AllowMultiple" true or false.
        // AllowMultiple=true:
        // Parameter can take multiple values separated by space, or can be specified multiple times.
        // Each of those specifications can then take 1 or more values.
        // AllowMultiple=false:
        // Parameter has arity of 1, which requires it to provide a single value and can be specified only once.
        // A special case is a boolean parameter, which has arity of 1 or 0 to allow providing either True / False to it
        // or provide no value which indicates True (because the parameter is specified). To achieve this, the next
        // value is inspected and we try to parse it to boolean, when that fails we consider the parameter to have 0 arity.
        //
        // All values that are not parameters and don't fit into any parameter are bound to the default argument.
        // This default argument is usually source path, which would consume all provided values. To avoid ambiguity
        // for parsing when unknown parameters are allowed, it is recommended to specify all the sources first to allow
        // them all to bind to the argument rather than putting them at the end where some can be incorrectly bound to
        // unknown parameter.
        for (var i = 0; i < args.Count;)
        {
            // The real argument that user provided.
            var arg = args[i];
            if (IsParameter(arg))
            {
                // Converted parameter to -- syntax, and when / is used, it also outputs
                // -alias as we don't know if the user meant a short or long name (/? = -?, or /help = --help).
                var name = NormalizeParameterName(arg, out string? additionalName);
                // We found a parameter, see if there is any processor for it.
                if (!(aliasesToProcessorMap.TryGetValue(name, out var processor)
                    || (additionalName != null && aliasesToProcessorMap.TryGetValue(additionalName, out processor))))
                {
                    // Could not find processor for this parameter or any of its aliases,
                    // consume values while there are some, because we don't know the arity
                    // of this parameter so we just assume it can take all the values
                    // it was provided.
                    unbound.Add(arg);
                    var values = TakeValuesUntilNextParameter(args, i);
                    unbound.AddRange(values);
                    // In args, move to the position after the values so we can process next parameter
                    // or end.
                    i += 1 + values.Count; // 1 (the parameter) + the number of values it took
                    continue;
                }
                else
                {
                    // We found the parameter processor, consume as many values as it is allowed.
                    if (processor.AllowMultiple)
                    {
                        // We are allowed to take multiple values, so we take values until the next parameter or end,
                        // and merge the values with what already exists in tokens.
                        List<string> values = TakeValuesUntilNextParameter(args, i);
                        ValidateParameter(tokenized, processor, arg, values, bindingErrors);
                        if (!tokenized.ContainsKey(processor.Name))
                        {
                            tokenized[processor.Name] = values;
                        }
                        else
                        {
                            tokenized[processor.Name].AddRange(values);
                        }

                        // In args, move to the position after the values so we can process next parameter
                        // or end.
                        i += 1 + values.Count; // 1 (the parameter) + the number of values it took
                        continue;
                    }
                    else
                    {
                        // We are allowed to take single value, so try to take it.
                        string? value = TakeSingleValueOrUntilNextParameter(args, i, isBool: processor.ValueType == typeof(bool));
                        List<string> values = value == null ? new List<string>() : new List<string> { value };
                        ValidateParameter(tokenized, processor, arg, values, bindingErrors);
                        AddOrUpdateParameterEntry(tokenized, processor, values);

                        // In args, move to the position after the values so we can process next parameter
                        // or end.
                        i += 1 + values.Count; // 1 (the parameter) + the number of values it took
                        continue;
                    }
                }
            }
            else
            {
                // We found a value, take all the values until the next parameter
                // and bind them to the default argument, or make them unbound.
                List<string> values = new[] { arg }.Concat(TakeValuesUntilNextParameter(args, i)).ToList();
                if (hasDefaultArgument)
                {
                    ValidateParameter(tokenized, defaultArgument, arg, values, bindingErrors);
                    AddOrUpdateParameterEntry(tokenized, defaultArgument, values);
                }
                else
                {
                    unbound.AddRange(values);
                }

                // In args, move to the position after the values so we can process next parameter
                // or end.
                i += values.Count;
                continue;
            }

        }

        var bound = tokenized.Select(p => new BoundParameter(aliasesToProcessorMap[p.Key], p.Value)).ToList();
        return (bound, unbound);
    }

    private static void AddOrUpdateParameterEntry(Dictionary<string, List<string>> tokenized, ArgumentProcessor processor, List<string> values)
    {
        if (!tokenized.ContainsKey(processor.Name))
        {
            tokenized[processor.Name] = values;
        }
        else
        {
            tokenized[processor.Name].AddRange(values);
        }
    }

    /// <summary>
    /// Ensure that parameter was not added more times than it should, and that it did not get more (or less) values than it should.
    /// </summary>
    private static void ValidateParameter(Dictionary<string, List<string>> tokenized, ArgumentProcessor processor, string arg, List<string> values, List<string> parseErrors)
    {
        // Do not check the count of values in the collection here, we want to fail the validation
        // even if the parameter is specified multiple times without any additional value provided, e.g. --blame --blame
        // which is both missing the value for --blame, and is specified multiple times for parameter that does not allow it.
        if (!processor.AllowMultiple && tokenized.ContainsKey(processor.Name))
        {
            parseErrors.Add($"Parameter '{arg}' {(arg != processor.Name ? $"({processor.Name})" : null)} cannot be used multiple times.");
        }

        if (values.Count == 0 && processor.ValueType != typeof(bool))
        {
            parseErrors.Add($"No value was provided for parameter '{arg}' {(arg != processor.Name ? $"({processor.Name})" : null)}.");
        }

        if (!processor.AllowMultiple && values.Count > 1)
        {
            parseErrors.Add($"Multiple values were provided for parameter '{arg}' {(arg != processor.Name ? $"({processor.Name})" : null)}, but it only takes 1 value.");
        }
    }

    private static string NormalizeParameterName(string arg, out string? additionalName)
    {
        if (arg.StartsWith("/"))
        {
            var name = arg.Substring(1, arg.Length - 1);
            additionalName = $"-{name}";
            return $"--{name}";
        }

        additionalName = null;
        return arg;
    }

    private static bool IsParameter(string arg)
    {
        return arg.StartsWith("--") || arg.StartsWith("/") || arg.StartsWith("-");
    }

    private static List<string> TakeValuesUntilNextParameter(List<string> args, int parameterPosition)
    {
        var values = new List<string>();
        for (int i = parameterPosition + 1; i < args.Count; i++)
        {
            if (IsParameter(args[i]))
                break;

            values.Add(args[i]);
        }

        return values;
    }

    private static string? TakeSingleValueOrUntilNextParameter(List<string> args, int parameterPosition, bool isBool)
    {
        int valueCandidatePosition = parameterPosition + 1;
        // If we did not reach the end of arguments and the next value is not a parameter, take the value.
        if (valueCandidatePosition < args.Count && !IsParameter(args[valueCandidatePosition]))
        {
            if (!isBool)
            {
                // This is not a boolean parameter just take the value and use it.
                return args[valueCandidatePosition];
            }
            else
            {
                // This is a boolean parameter, try parsing the value into boolean and only take it
                // if it is true or false, otherwise don't take it. This allows --bool-parameter 1.dll
                // to work as a switch, where we determine if the parameter should be enabled simply by
                // its presence.
                if (string.Equals(args[valueCandidatePosition], "true", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(args[valueCandidatePosition], "false", StringComparison.OrdinalIgnoreCase))
                {
                    return args[valueCandidatePosition];
                }
                else
                {
                    return null;
                }
            }
        }

        return null;
    }

    private static List<TypedParameter> Convert(List<BoundParameter> boundParameters, ref List<string> conversionErrors)
    {
        var typed = new List<TypedParameter>();
        foreach (var parameter in boundParameters)
        {
            var processor = parameter.Processor;
            if (processor.ValueType.IsArray)
            {
                List<object> convertedValues = new();
                foreach (var value in parameter.Value)
                {
                    var converted = processor.ValueFactory(value);
                    if (converted == null)
                    {
                        conversionErrors.Add($"Value '{value}' for parameter {processor.Name} is invalid.");
                    }
                    else
                    {
                        convertedValues.Add(converted);
                    }
                }

                if (convertedValues.Count > 0)
                {
                    var arr = Array.CreateInstance(processor.ValueType.GetElementType(), convertedValues.Count);
                    for (var i = 0; i < convertedValues.Count; i++)
                    {
                        arr.SetValue(convertedValues[i], i);
                    }
                    typed.Add(new TypedParameter(processor, arr));
                }
            }
            else
            {
                object? convertedValue = default;
                if (parameter.Value.Count == 0 && parameter.Processor.ValueType == typeof(bool))
                {
                    // We got the parameter, so it was specified, which means "true" when no other
                    // value is specfied.
                    convertedValue = true;
                }
                else
                {
                    var value = parameter.Value.Single();
                    var converted = processor.ValueFactory(value);
                    if (converted == null)
                    {
                        conversionErrors.Add($"Value '{value}' for parameter {processor.Name} is invalid.");
                    }
                    else
                    {
                        convertedValue = converted;
                    }
                }

                if (convertedValue != null)
                {
                    typed.Add(new TypedParameter(processor, convertedValue));
                }
            }
        }

        return typed;
    }


    private static bool ValidateParseResult(
        List<BoundParameter> bound, List<string> unbound,
        IReadOnlyList<ArgumentProcessor> argumentProcessors, bool ignoreExtraParameters, ref List<string> validationErrors)
    {
        if (!ignoreExtraParameters && unbound.Count > 0)
        {
            foreach (var arg in unbound)
            {
                validationErrors.Add(string.Format(CultureInfo.CurrentCulture, CommandLineResources.InvalidArgument, arg));
            }

            return false;
        }

        foreach (var arg in bound)
        {
            foreach (var validator in arg.Processor.Validators)
            {
                var error = validator(arg.Value);
                if (!StringUtils.IsNullOrWhiteSpace(error))
                {
                    validationErrors.Add(error);
                }
            }
        }

        foreach (var processor in argumentProcessors)
        {
            // check that all processors that require a value have a value
        }


        return true;
    }

    private static (List<string> arguments, List<string> options) ExpandResponseFiles(List<string> args, List<string> options, ref List<string> parseErrors)
    {
        var expandedArgs = new List<string>();
        var expandedOptions = new List<string>();

        // Go through each argument and if it is not a response file (does not start with @)
        // keep it in place. Otherwise expand the arguments from the response file. Putting
        // every argument in place where the response file argument was, and putting all options
        // in the order in which the response files appeared, followed options that were provided
        // on command line.
        foreach (var arg in args)
        {
            if (!arg.StartsWith("@", StringComparison.Ordinal))
            {
                expandedArgs.Add(arg);
                continue;
            }

            string path = arg.Substring(1).TrimEnd();
            string? content = null;

            try
            {
                content = File.ReadAllText(path);
            }
            catch (Exception)
            {
                parseErrors.Add(string.Format(CultureInfo.CurrentCulture, CommandLineResources.OpenResponseFileError, path));
            }

            if (content != null)
            {
                // Split the command line the same way it would be split when
                // we get it in the incoming args[] in Main.
                var split = Utilities.CommandLineUtilities.SplitCommandLine(content, ref parseErrors);
                // Take options from every response file and add it to options,
                // rather than expanding the args from all files and putting them together.
                // If we did that instead then the first response file to have options (--) would make
                // everything that follows it also an option.
                var (argsFromFile, optionsFromFile) = GetDoubleDashOptions(split);
                expandedArgs.AddRange(argsFromFile);
                expandedOptions.AddRange(optionsFromFile);
            }
        }

        return (expandedArgs, expandedOptions.Concat(options).ToList());
    }

    private static (List<string> args, List<string> options) GetDoubleDashOptions(List<string> args)
    {
        var doubleDashIndex = args.ToList().IndexOf("--");

        if (doubleDashIndex == -1)
        {
            // Double dash "--" in not found, we have just args.
            return (args, new List<string>());
        }

        // Double dash is found, return the parts before and after it.
        var options = new List<string>(args.Count);
        var argsRemainder = new List<string>(args.Count);
        for (int i = 0; i < args.Count; i++)
        {
            if (i < doubleDashIndex)
            {
                argsRemainder.Add(args[i]);
            }
            else if (i == doubleDashIndex)
            {
                // skip --
            }
            else
            {
                options.Add(args[i]);
            }
        }

        return (argsRemainder, options);
    }
}

internal record BoundParameter(ArgumentProcessor Processor, List<string> Value) { }
internal record TypedParameter(ArgumentProcessor Processor, object Value) { }
