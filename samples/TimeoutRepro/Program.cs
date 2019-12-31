using System;
using System.Diagnostics;
using System.IO;
using Microsoft.VisualStudio.TestPlatform.CoreUtilities.Extensions;

namespace TimeoutRepro
{
    class Program
    {
        public static void Main(string[] args)
        {
            // Spawn of vstest.console with a run tests from the current execting folder.
            var executingLocation = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
            Debug.Assert(executingLocation != null, "executingLocation != null");

            // Remove Microsoft.VisualStudio.TestPlatform.TestFramework.*.dll if they are present
            if (File.Exists(Path.Combine(executingLocation, "Microsoft.VisualStudio.TestPlatform.TestFramework.dll")))
            {
                File.Delete(Path.Combine(executingLocation, "Microsoft.VisualStudio.TestPlatform.TestFramework.dll"));
            }

            if (File.Exists(Path.Combine(executingLocation, "Microsoft.VisualStudio.TestPlatform.TestFramework.Extensions.dll")))
            {
                File.Delete(Path.Combine(executingLocation, "Microsoft.VisualStudio.TestPlatform.TestFramework.Extensions.dll"));
            }

            // Start vstest.console with sample test assembly
            var runnerLocation = Path.Combine(executingLocation, "vstest.console.exe");
            var testadapterPath = Path.Combine(executingLocation, "Adapter");
            var testAssembly = Path.Combine(executingLocation, "UnitTestProject.dll");

            var arguments = string.Concat(testAssembly.AddDoubleQuote(), " /testadapterpath:", testadapterPath.AddDoubleQuote());

            var process = new Process
            {
                StartInfo =
                                      {
                                          UseShellExecute = false,
                                          CreateNoWindow = false,
                                          FileName = runnerLocation,
                                          Arguments = arguments
                                      },
                EnableRaisingEvents = true
            };
            process.Start();
            process.WaitForExit();
        }
    }
}
