// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.


using BenchmarkDotNet.Attributes;

using Microsoft.TestPlatform.AdapterUtilities;

namespace MyBench;


public static class PRogram
{
    public static void Main(string[] _)
    {
        BenchmarkDotNet.Running.BenchmarkRunner.Run<Bench>();
    }
}

[MemoryDiagnoser]
// [ShortRunJob]
public class Bench
{
    static string ManagedType { get; } = "this is a long name for something";
    static string ManagedMethod { get; } = "MyLongMethodName";

    static string FullyQualifiedName { get; } = "this is a long name for something.MyLongMethodName.name name";

    static Uri Uri { get; } = new Uri("executor://MStest/v2");
    static string UriString { get; } = new Uri("executor://MStest/v2").ToString();

    [Benchmark]
#pragma warning disable CA1822 // Mark members as static
    public void Id()
#pragma warning restore CA1822 // Mark members as static
    {
        var idProvider = new TestIdProvider();
        idProvider.AppendString(UriString);

        // Below comment is copied over from Test Platform.
        // If source is a file name then just use the filename for the identifier since the file might have moved between
        // discovery and execution (in appx mode for example). This is not elegant because the Source contents should be
        // a black box to the framework.
        // For example in the database adapter case this is not a file path.
        // As discussed with team, we found no scenario for netcore, & fullclr where the Source is not present where ID
        // is generated, which means we would always use FileName to generate ID. In cases where somehow Source Path
        // contained garbage character the API Path.GetFileName() we are simply returning original input.
        // For UWP where source during discovery, & during execution can be on different machine, in such case we should
        // always use Path.GetFileName().
        string filePath = @"C:\Users\jajares\Downloads\Report20240530-1621.diagsession";
        try
        {
            filePath = Path.GetFileName(filePath);
        }
        catch (ArgumentException)
        {
            // In case path contains invalid characters.
        }

        idProvider.AppendString(filePath);

        if (true)
        {
            idProvider.AppendString(ManagedType);
            idProvider.AppendString(ManagedMethod);
        }

#pragma warning disable IDE0059 // Unnecessary assignment of a value
        var guid = idProvider.GetId();
#pragma warning restore IDE0059 // Unnecessary assignment of a value
    }

}
