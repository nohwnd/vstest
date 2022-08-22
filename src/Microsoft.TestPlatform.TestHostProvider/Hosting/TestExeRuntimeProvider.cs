using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

using Microsoft.TestPlatform.TestHostProvider.Hosting;
using Microsoft.TestPlatform.TestHostProvider.Resources;
using Microsoft.VisualStudio.TestPlatform.CoreUtilities.Extensions;
using Microsoft.VisualStudio.TestPlatform.CrossPlatEngine.Helpers;
using Microsoft.VisualStudio.TestPlatform.CrossPlatEngine.Helpers.Interfaces;
using Microsoft.VisualStudio.TestPlatform.DesktopTestHostRuntimeProvider;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client.Interfaces;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Host;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Utilities;
using Microsoft.VisualStudio.TestPlatform.PlatformAbstractions;
using Microsoft.VisualStudio.TestPlatform.PlatformAbstractions.Interfaces;
using Microsoft.VisualStudio.TestPlatform.Utilities;
using Microsoft.VisualStudio.TestPlatform.Utilities.Helpers;
using Microsoft.VisualStudio.TestPlatform.Utilities.Helpers.Interfaces;

#nullable disable

/// <summary>
/// Provides a testhost to vstest.console when asked to run tests from Test1.exe.
/// </summary>
[ExtensionUri(Uri)]
[FriendlyName(FriendlyName)]
internal class TestExeRuntimeProvider : IInternalTestRuntimeProvider
{
    private const string Uri = $"HostProvider://{nameof(TestExeRuntimeProvider)}";
    private const string FriendlyName = nameof(TestExeRuntimeProvider);
    // Any version (older or newer) that is not in this list will use the default testhost.exe that is built using net451.
    // TODO: Add net481 when it is published, if it uses a new moniker.
    private static readonly ImmutableArray<string> SupportedTargetFrameworks = ImmutableArray.Create("net452", "net46", "net461", "net462", "net47", "net471", "net472", "net48");

    private Architecture _architecture;
    private Framework _targetFramework;
    private readonly IProcessHelper _processHelper;
    private readonly IFileHelper _fileHelper;
    private readonly IEnvironment _environment;
    private readonly IDotnetHostHelper _dotnetHostHelper;

    private ITestHostLauncher _customTestHostLauncher;
    private Process _testHostProcess;
    private StringBuilder _testHostProcessStdError;
    private IMessageLogger _messageLogger;
    private bool _hostExitedEventRaised;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultTestHostManager"/> class.
    /// </summary>
    public TestExeRuntimeProvider()
        : this(new ProcessHelper(), new FileHelper(), new PlatformEnvironment(), new DotnetHostHelper())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultTestHostManager"/> class.
    /// </summary>
    /// <param name="processHelper">Process helper instance.</param>
    /// <param name="fileHelper">File helper instance.</param>
    /// <param name="environment">Instance of platform environment.</param>
    /// <param name="dotnetHostHelper">Instance of dotnet host helper.</param>
    internal TestExeRuntimeProvider(IProcessHelper processHelper, IFileHelper fileHelper, IEnvironment environment, IDotnetHostHelper dotnetHostHelper)
    {
        _processHelper = processHelper;
        _fileHelper = fileHelper;
        _environment = environment;
        _dotnetHostHelper = dotnetHostHelper;
    }

    /// <inheritdoc/>
    public event EventHandler<HostProviderEventArgs> HostLaunched;

    /// <inheritdoc/>
    public event EventHandler<HostProviderEventArgs> HostExited;

    /// <inheritdoc/>
    public bool Shared => false;

    /// <summary>
    /// Gets the properties of the test executor launcher. These could be the targetID for emulator/phone specific scenarios.
    /// </summary>
    public IDictionary<string, string> Properties => new Dictionary<string, string>();

    /// <summary>
    /// Gets callback on process exit
    /// </summary>
    private Action<object> ExitCallBack => (process) => TestHostManagerCallbacks.ExitCallBack(_processHelper, process, _testHostProcessStdError, OnHostExited);

    /// <summary>
    /// Gets callback to read from process error stream
    /// </summary>
    private Action<object, string> ErrorReceivedCallback => (process, data) => TestHostManagerCallbacks.ErrorReceivedCallback(_testHostProcessStdError, data);

    /// <inheritdoc/>
    public void SetCustomLauncher(ITestHostLauncher customLauncher)
    {
        _customTestHostLauncher = customLauncher;
    }

    /// <inheritdoc/>
    public TestHostConnectionInfo GetTestHostConnectionInfo()
    {
        return new TestHostConnectionInfo { Endpoint = "127.0.0.1:0", Role = ConnectionRole.Client, Transport = Transport.Sockets };
    }

    /// <inheritdoc/>
    public Task<bool> LaunchTestHostAsync(TestProcessStartInfo testHostStartInfo, CancellationToken cancellationToken)
    {
        // Do NOT offload this to thread pool using Task.Run, we already are on thread pool
        // and this would go into a queue after all the other startup tasks. Meaning we will start
        // testhost much later, and not immediately.
        return Task.FromResult(LaunchHost(testHostStartInfo, cancellationToken));
    }

    /// <inheritdoc/>
    public virtual TestProcessStartInfo GetTestHostProcessStartInfo(
        IEnumerable<string> sources,
        IDictionary<string, string> environmentVariables,
        TestRunnerConnectionInfo connectionInfo)
    {
        // We said we are not shared, so we should always get just 1.
        string testhostProcessPath = sources.Single();

        var argumentsString = " " + connectionInfo.ToCommandLineOptions();

        EqtTrace.Verbose($"TestExeRuntimeProvider: Running {testhostProcessPath}{argumentsString}");

        var processWorkingDirectory = Path.GetDirectoryName(testhostProcessPath);

        return new TestProcessStartInfo
        {
            FileName = testhostProcessPath,
            Arguments = argumentsString,
            EnvironmentVariables = environmentVariables ?? new Dictionary<string, string>(),
            WorkingDirectory = processWorkingDirectory
        };
    }

    /// <inheritdoc/>
    public IEnumerable<string> GetTestPlatformExtensions(IEnumerable<string> sources, IEnumerable<string> extensions)
    {
        // Do not include any of the external dlls, just use adapters from the
        // source directory. We need this until the local registration in the test.exe
        // is able to register the extensions to test extension cache directly.
        var ext = sources.SelectMany(s => _fileHelper.EnumerateFiles(Path.GetDirectoryName(s), SearchOption.TopDirectoryOnly, "TestAdapter.dll"));

        ext = FilterExtensionsBasedOnVersion(ext);

        return ext;
    }

    /// <inheritdoc/>
    public IEnumerable<string> GetTestSources(IEnumerable<string> sources)
    {
        return sources;
    }

    /// <inheritdoc/>
    public bool CanExecuteCurrentRunConfiguration(string runsettingsXml)
    {
        throw new NotSupportedException();
    }
    public bool CanExecute(CanExecuteInfo canExecuteInfo)
    {
        // TODO: obviously not good enough for Linux.
        var canExecute = canExecuteInfo.Source.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);
        return canExecute;
    }

    /// <inheritdoc/>
    public void Initialize(IMessageLogger logger, string runsettingsXml)
    {
        _testHostProcess = null;
        _hostExitedEventRaised = false;
    }

    /// <inheritdoc/>
    public Task CleanTestHostAsync(CancellationToken cancellationToken)
    {
        try
        {
            _processHelper.TerminateProcess(_testHostProcess);
        }
        catch (Exception ex)
        {
            EqtTrace.Warning("DefaultTestHostManager: Unable to terminate test host process: " + ex);
        }

        _testHostProcess?.Dispose();

        return Task.FromResult(true);
    }

    /// <inheritdoc />
    public bool AttachDebuggerToTestHost()
    {
        return _customTestHostLauncher switch
        {
            ITestHostLauncher3 launcher3 => launcher3.AttachDebuggerToProcess(new AttachDebuggerInfo { ProcessId = _testHostProcess.Id, TargetFramework = _targetFramework.ToString() }, CancellationToken.None),
            ITestHostLauncher2 launcher2 => launcher2.AttachDebuggerToProcess(_testHostProcess.Id),
            _ => false,
        };
    }

    /// <summary>
    /// Filter duplicate extensions, include only the highest versioned extension
    /// </summary>
    /// <param name="extensions">Entire list of extensions</param>
    /// <returns>Filtered list of extensions</returns>
    private IEnumerable<string> FilterExtensionsBasedOnVersion(IEnumerable<string> extensions)
    {
        Dictionary<string, string> selectedExtensions = new();
        Dictionary<string, Version> highestFileVersions = new();
        Dictionary<string, Version> conflictingExtensions = new();

        foreach (var extensionFullPath in extensions)
        {
            // assemblyName is the key
            var extensionAssemblyName = Path.GetFileNameWithoutExtension(extensionFullPath);

            if (selectedExtensions.TryGetValue(extensionAssemblyName, out var oldExtensionPath))
            {
                // This extension is duplicate
                var currentVersion = GetAndLogFileVersion(extensionFullPath);

                var oldVersionFound = highestFileVersions.TryGetValue(extensionAssemblyName, out var oldVersion);
                if (!oldVersionFound)
                {
                    oldVersion = GetAndLogFileVersion(oldExtensionPath);
                }

                // If the version of current file is higher than the one in the map
                // replace the older with the current file
                if (currentVersion > oldVersion)
                {
                    highestFileVersions[extensionAssemblyName] = currentVersion;
                    conflictingExtensions[extensionAssemblyName] = currentVersion;
                    selectedExtensions[extensionAssemblyName] = extensionFullPath;
                }
                else
                {
                    if (currentVersion < oldVersion)
                    {
                        conflictingExtensions[extensionAssemblyName] = oldVersion;
                    }

                    if (!oldVersionFound)
                    {
                        highestFileVersions.Add(extensionAssemblyName, oldVersion);
                    }
                }
            }
            else
            {
                selectedExtensions.Add(extensionAssemblyName, extensionFullPath);
            }
        }

        // Log warning if conflicting version extensions are found
        if (conflictingExtensions.Any())
        {
            var extensionsString = string.Join("\n", conflictingExtensions.Select(kv => string.Format("  {0} : {1}", kv.Key, kv.Value)));
            string message = string.Format(CultureInfo.CurrentCulture, Resources.MultipleFileVersions, extensionsString);
            _messageLogger.SendMessage(TestMessageLevel.Warning, message);
        }

        return selectedExtensions.Values;
    }

    private Version GetAndLogFileVersion(string path)
    {
        var fileVersion = _fileHelper.GetFileVersion(path);
        EqtTrace.Verbose("FileVersion for {0} : {1}", path, fileVersion);

        return fileVersion;
    }

    /// <summary>
    /// Raises HostLaunched event
    /// </summary>
    /// <param name="e">host provider event args</param>
    private void OnHostLaunched(HostProviderEventArgs e)
    {
        HostLaunched.SafeInvoke(this, e, "HostProviderEvents.OnHostLaunched");
    }

    /// <summary>
    /// Raises HostExited event
    /// </summary>
    /// <param name="e">host provider event args</param>
    private void OnHostExited(HostProviderEventArgs e)
    {
        if (!_hostExitedEventRaised)
        {
            _hostExitedEventRaised = true;
            HostExited.SafeInvoke(this, e, "HostProviderEvents.OnHostExited");
        }
    }

    private bool LaunchHost(TestProcessStartInfo testHostStartInfo, CancellationToken cancellationToken)
    {
        _testHostProcessStdError = new StringBuilder(0, Microsoft.VisualStudio.TestPlatform.CoreUtilities.Constants.StandardErrorMaxLength);
        EqtTrace.Verbose("Launching default test Host Process {0} with arguments {1}", testHostStartInfo.FileName, testHostStartInfo.Arguments);

        // We launch the test host process here if we're on the normal test running workflow.
        // If we're debugging and we have access to the newest version of the testhost launcher
        // interface we launch it here as well, but we expect to attach later to the test host
        // process by using its PID.
        // For every other workflow (e.g.: profiling) we ask the IDE to launch the custom test
        // host for us. In the profiling case this is needed because then the IDE sets some
        // additional environmental variables for us to help with probing.
        if ((_customTestHostLauncher == null)
            || (_customTestHostLauncher.IsDebug
                && _customTestHostLauncher is ITestHostLauncher2))
        {
            EqtTrace.Verbose("DefaultTestHostManager: Starting process '{0}' with command line '{1}'", testHostStartInfo.FileName, testHostStartInfo.Arguments);
            cancellationToken.ThrowIfCancellationRequested();
            _testHostProcess = _processHelper.LaunchProcess(
                testHostStartInfo.FileName,
                testHostStartInfo.Arguments,
                testHostStartInfo.WorkingDirectory,
                testHostStartInfo.EnvironmentVariables,
                ErrorReceivedCallback,
                ExitCallBack,
                null) as Process;
        }
        else
        {
            int processId = _customTestHostLauncher.LaunchTestHost(testHostStartInfo, cancellationToken);
            _testHostProcess = Process.GetProcessById(processId);
            _processHelper.SetExitCallback(processId, ExitCallBack);
        }

        OnHostLaunched(new HostProviderEventArgs("Test Runtime launched", 0, _testHostProcess.Id));
        return _testHostProcess != null;
    }
}
