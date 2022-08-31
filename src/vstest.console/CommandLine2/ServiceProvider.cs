// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

using Microsoft.VisualStudio.TestPlatform.Utilities;

namespace Microsoft.VisualStudio.TestPlatform.CommandLine2;

internal class ServiceProvider : IServiceProvider
{
    private readonly Dictionary<Type, Func<IServiceProvider, object>> _services = new();

    public void AddService<T>(Func<IServiceProvider, T> factory) => _services[typeof(T)] = p => factory(p)!;

    public void AddService(Type serviceType, Func<IServiceProvider, object> factory) => _services[serviceType] = factory;

    public IReadOnlyCollection<Type> AvailableServiceTypes => _services.Keys;

    public T GetService<T>()
    {
        return (T)GetService(typeof(T));
    }

    public object GetService(Type serviceType)
    {
        if (_services.TryGetValue(serviceType, out var factory) && factory != null)
        {
            if (serviceType == typeof(ConsoleOutput))
            {
                // We are resolving a concrete dependency. Who called us?
            }
            return factory(this);
        }

        throw new InvalidOperationException($"Could not resolve service of type: '{serviceType}'.");
    }
}
