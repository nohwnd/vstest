// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using FluentAssertions;
using FluentAssertions.Extensions;

using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities;
using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities.Interfaces;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using System.Text;
using Microsoft.VisualStudio.TestPlatform.PlatformAbstractions;
using System.Collections.Generic;
using Microsoft.Diagnostics.Tracing.Parsers.MicrosoftWindowsTCPIP;
using System.Security.AccessControl;

namespace Microsoft.TestPlatform.PerformanceTests;

[TestClass]
// [Ignore("The timing can vary significantly based on the system running the test. Convert them to report the results and not fail.")]
public class SocketTests
{
    [TestMethod]
    public async Task SocketThroughput5()
    {
        try
        {
            // Measure the throughput with socket communication v2 (SocketServer, SocketClient)
            // implementation.
            var server = new AsyncSocketServer("server");
            var client = new AsyncSocketClient("client");
            ManualResetEventSlim dataTransferred = new(false);
            ManualResetEventSlim clientConnected = new(false);
            ManualResetEventSlim serverConnected = new(false);
            int dataReceived = 0;
            var watch = new Stopwatch();
            var repetitions = 20;


            // Setup server
            Func<CommunicationEndpointHandle, string, CancellationToken, Task> messageReceivedAsync = (server, message, cancellationToken) =>
            {
                // Keep count of bytes
                dataReceived += message.Length;

                if (dataReceived >= 65536 * repetitions)
                {
                    dataTransferred.Set();
                    watch.Stop();

                    server.Abort();
                }

                return Task.CompletedTask;
            };

            var ip = await server.StartAsync(IPAddress.Loopback.ToString() + ":0", CancellationToken.None);
            await client.StartAsync(ip, CancellationToken.None);

            var serverInstance = await server.ConnectAsync(messageReceivedAsync, CancellationToken.None);
            var clientInstance = await client.ConnectAsync(messageReceivedAsync: null, CancellationToken.None);

            await clientInstance.Connected;
            await SendDataAsync(clientInstance, watch);


            dataTransferred.Wait();

            await serverInstance.Completion;
            await clientInstance.Completion;

            Console.WriteLine($"Done in {watch.ElapsedMilliseconds} ms");
            watch.Elapsed.Should().BeLessOrEqualTo(15.Seconds());



            async Task SendDataAsync(CommunicationEndpointHandle client, Stopwatch watch)
            {
                var dataBytes = new byte[65536];
                for (int i = 0; i < dataBytes.Length; i++)
                {
                    dataBytes[i] = 0x65;
                }

                var dataBytesStr = Encoding.UTF8.GetString(dataBytes);

                watch.Start();
                for (int i = 0; i < repetitions; i++)
                {
                    await client.SendAsync(dataBytesStr, CancellationToken.None);
                }
            }
        }
        catch (Exception ex)
        {

        }
    }

    [TestMethod]
    public void SocketThroughput2()
    {
        // Measure the throughput with socket communication v2 (SocketServer, SocketClient)
        // implementation.
        var server = new SocketServer();
        var client = new SocketClient();
        ICommunicationChannel? serverChannel = null;
        ICommunicationChannel? clientChannel = null;
        ManualResetEventSlim dataTransferred = new(false);
        ManualResetEventSlim clientConnected = new(false);
        ManualResetEventSlim serverConnected = new(false);
        int dataReceived = 0;
        var watch = new Stopwatch();
        var thread = new Thread(() => SendData(clientChannel, watch));

        // Setup server
        server.Connected += (sender, args) =>
        {
            serverChannel = args.Channel;
            serverChannel!.MessageReceived += (channel, messageReceived) =>
            {
                // Keep count of bytes
                dataReceived += messageReceived.Data!.Length;

                if (dataReceived >= 65536 * 20000)
                {
                    dataTransferred.Set();
                    watch.Stop();
                }
            };

            clientConnected.Set();
        };

        client.Connected += (sender, args) =>
        {
            clientChannel = args.Channel;

            thread.Start();

            serverConnected.Set();
        };

        var port = server.Start(IPAddress.Loopback.ToString() + ":0")!;
        client.Start(port);

        clientConnected.Wait();
        serverConnected.Wait();
        thread.Join();
        dataTransferred.Wait();

        Console.WriteLine($"Done in {watch.ElapsedMilliseconds} ms");
        watch.Elapsed.Should().BeLessOrEqualTo(15.Seconds());
    }

    [TestMethod]
    public void SocketThroughput1()
    {
        // Measure the throughput with socket communication v1 (SocketCommunicationManager)
        // implementation.
        var server = new SocketCommunicationManager();
        var client = new SocketCommunicationManager();
        var watch = new Stopwatch();

        int port = server.HostServer(new IPEndPoint(IPAddress.Loopback, 0)).Port;
        client.SetupClientAsync(new IPEndPoint(IPAddress.Loopback, port)).Wait();
        server.AcceptClientAsync().Wait();

        server.WaitForClientConnection(1000);
        client.WaitForServerConnection(1000);

        var clientThread = new Thread(() => SendData2(client, watch));
        clientThread.Start();

        var dataReceived = 0;
        while (dataReceived < 65536 * 20000)
        {
            dataReceived += server.ReceiveRawMessage()!.Length;
        }

        watch.Stop();
        clientThread.Join();

        Console.WriteLine($"Done in {watch.ElapsedMilliseconds} ms");
        watch.Elapsed.Should().BeLessOrEqualTo(20.Seconds());
    }

    private static void SendData(ICommunicationChannel? channel, Stopwatch watch)
    {
        var dataBytes = new byte[65536];
        for (int i = 0; i < dataBytes.Length; i++)
        {
            dataBytes[i] = 0x65;
        }

        var dataBytesStr = System.Text.Encoding.UTF8.GetString(dataBytes);

        watch.Start();
        for (int i = 0; i < 20000; i++)
        {
            channel!.Send(dataBytesStr);
        }
    }

    private static void SendData2(ICommunicationManager communicationManager, Stopwatch watch)
    {
        var dataBytes = new byte[65536];
        for (int i = 0; i < dataBytes.Length; i++)
        {
            dataBytes[i] = 0x65;
        }

        var dataBytesStr = System.Text.Encoding.UTF8.GetString(dataBytes);

        watch.Start();
        for (int i = 0; i < 20000; i++)
        {
            communicationManager.SendRawMessage(dataBytesStr);
        }
    }
}

public delegate Task AsyncEventHandler<TEventArgs>(object? sender, TEventArgs e, CancellationToken cancellationToken);
public delegate Task AsyncEventHandler<TEventArgs, TEventArgs2>(object? sender, TEventArgs e, TEventArgs2 e2, CancellationToken cancellationToken);

internal class LengthPrefixCommunicationChannelAsync : IDisposable
{
    private readonly Stream _stream;
    private readonly AsyncLock _writeLock = new();
    private readonly Stream _bufferedWriter;
    private readonly BinaryWriter _writer;

    public LengthPrefixCommunicationChannelAsync(Stream stream)
    {
        _stream = stream;

        // Using the Buffered stream while writing, it improves write performance by reducing the number of writes, because we only
        // write after a whole logical operation, rather than using
        _bufferedWriter = new PlatformStream().CreateBufferedStream(stream, SocketConstants.BufferSize);
        _writer = new BinaryWriter(_bufferedWriter, Encoding.UTF8, leaveOpen: true);
    }

    public void Dispose()
    {
        try { _stream.Dispose(); } catch { }
        try { _bufferedWriter.Dispose(); } catch { }
        try { _writer.Dispose(); } catch { }
    }

    public async Task ProcessAsync(Func<CommunicationEndpointHandle, string, CancellationToken, Task> onMessageReceivedAsync, CommunicationEndpointHandle handle, CancellationToken cancellationToken)
    {
        var maxHeaderSize = 5;
        var bufferSize = SocketConstants.BufferSize;// 8192;
        var buffer = new byte[bufferSize];
        var headerBuffer = new byte[maxHeaderSize];
        var headerMemoryBuffer = new MemoryStream(headerBuffer);
        // var headerReader = new BinaryReader(headerMemoryBuffer);
        var stringBuilder = new StringBuilder();

        var stream = _stream;

        while (!cancellationToken.IsCancellationRequested)
        {
            stringBuilder.Clear();

            // PERF: Can potentially clean just the non written elements, or not clear at all.
            Array.Clear(buffer, 0, buffer.Length);
            Array.Clear(headerBuffer, 0, headerBuffer.Length);

            // Attempt to read the size, we don't know how long it will be but we know that it will be 
            // at max 5 bytes long, because that is the maximum number of bytes that can represent the 
            // 7 bit encoded integer.
            var headerCandidateReadCount = await stream.ReadAsync(buffer, 0, maxHeaderSize, cancellationToken);
            if (headerCandidateReadCount == 0)
            {
                break;
            }

            Array.Copy(buffer, headerBuffer, Math.Min(headerCandidateReadCount, maxHeaderSize));

            var stringSizeInBytes = Read7BitEncodedInt(headerMemoryBuffer);
            var headerSize = (int)headerMemoryBuffer.Position;
            headerMemoryBuffer.Position = 0;

            if (stringSizeInBytes < maxHeaderSize)
            {
                throw new InvalidOperationException("oh noooo!");
            }

            var bufferOffset = headerCandidateReadCount;
            var remainingBytesCount = stringSizeInBytes + headerSize - headerCandidateReadCount;
            var freeBufferSpace = bufferSize - headerCandidateReadCount;
            var readCount = Math.Min(freeBufferSpace, remainingBytesCount);

            var bytesRead = await stream.ReadAsync(buffer, bufferOffset, readCount, cancellationToken);

            var dataStart = headerSize;

            var dataEnd = headerCandidateReadCount - headerSize + bytesRead;

            var data = Encoding.Default.GetString(buffer, dataStart, dataEnd);
            stringBuilder.Append(data);

            var remainingBytes = remainingBytesCount - readCount;
            while (remainingBytes > 0)
            {
                var dataReadSize = await stream.ReadAsync(buffer, 0, Math.Min(bufferSize, remainingBytes), cancellationToken);
                stringBuilder.Append(Encoding.Default.GetString(buffer, 0, dataReadSize));
                remainingBytes -= dataReadSize;
            }

            await onMessageReceivedAsync(handle, stringBuilder.ToString(), cancellationToken);
        }

        // From library code, .NET exposes this as public (and has better implementation imho)
        static int Read7BitEncodedInt(MemoryStream memoryStream)
        {
            int num = 0;
            int num2 = 0;
            byte b;
            do
            {
                if (num2 == 35)
                {
                    throw new FormatException("Format_Bad7BitInt32");
                }

                b = (byte)memoryStream.ReadByte();
                num |= (b & 0x7F) << num2;
                num2 += 7;
            }
            while ((b & 0x80u) != 0);
            return num;
        }
    }

    // TODO: rename to SendAsync
    public async Task Send(string data, CancellationToken cancellationToken)
    {
        try
        {
            using (await _writeLock.LockAsync())
            {
                _writer.Write(data);
                await _bufferedWriter.FlushAsync(cancellationToken);
            }
        }
        catch (NotSupportedException ex) when (!_writer.BaseStream.CanWrite)
        {
            // As we are simply creating streams around some stream passed as ctor argument, we
            // end up in some unsynchronized behavior where it's possible that the outside stream
            // was disposed and we are still trying to write something. In such case we would fail
            // with "System.NotSupportedException: Stream does not support writing.".
            // To avoid being too generic in that catch, I am checking if the stream is not writable.
            EqtTrace.Verbose("LengthPrefixCommunicationChannel.Send: BaseStream is not writable (most likely it was dispose). {0}", ex);
        }
        catch (Exception ex)
        {
            EqtTrace.Error("LengthPrefixCommunicationChannel.Send: Error sending data: {0}.", ex);
            throw new CommunicationException("Unable to send data over channel.", ex);
        }
    }
}

internal class CommunicationEndpointHandle
{
    private readonly LengthPrefixCommunicationChannelAsync _channel;
    private readonly CancellationTokenSource _cancellation;

    public CommunicationEndpointHandle(string ipEndpoint, LengthPrefixCommunicationChannelAsync channel, CancellationTokenSource cancellation)
    {
        IpEndpoint = ipEndpoint;
        _channel = channel;
        Completion = Task.CompletedTask;
        _cancellation = cancellation;
    }

    public string IpEndpoint { get; }

    public Task Completion { get; set; }

    public Task Connected { get; }

    public void Abort()
    {
        _cancellation.Cancel();
    }

    public void Stop()
    {
        _cancellation.Cancel();
    }

    public async Task SendAsync(string message, CancellationToken cancellationToken)
    {
        await _channel.Send(message, cancellationToken);
    }
}


// Stephen Toubs code: https://www.hanselman.com/blog/comparing-two-techniques-in-net-asynchronous-coordination-primitives
public sealed class AsyncLock
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly Task<IDisposable> _releaser;

    public AsyncLock()
    {
        _releaser = Task.FromResult((IDisposable)new Releaser(this));
    }

    public Task<IDisposable> LockAsync()
    {
        var wait = _semaphore.WaitAsync();
        return wait.IsCompleted ?
                    _releaser :
                    wait.ContinueWith((_, state) => (IDisposable)state,
                        _releaser.Result, CancellationToken.None,
        TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    private sealed class Releaser : IDisposable
    {
        private readonly AsyncLock _toRelease;
        internal Releaser(AsyncLock toRelease) { _toRelease = toRelease; }
        public void Dispose() { _toRelease._semaphore.Release(); }
    }
}

internal class AsyncSocketClient : AsyncSocketCommunicationEndpoint
{
    public AsyncSocketClient(string name) : base(name)
    {
    }

    protected override async Task<TcpClient?> ConnectAsync(IPEndPoint address, CancellationToken cancellationToken)
    {
        var tcpClient = new TcpClient { NoDelay = true };
        //TODO: cancel when cancellation token is cancelled
        await tcpClient.ConnectAsync(address.Address, address.Port);

        return tcpClient;
    }

    protected override IPEndPoint Listen(IPEndPoint address, CancellationToken cancellationToken)
    {
        // This is a client and we don't need to start listening to figure out the real address.
        return address;
    }
}

internal class AsyncSocketServer : AsyncSocketCommunicationEndpoint
{
    private TcpListener _tcpListener;

    public AsyncSocketServer(string name) : base(name)
    {
    }

    protected override async Task<TcpClient> ConnectAsync(IPEndPoint address, CancellationToken cancellationToken)
    {
        // TODO: cancel on cancellation token
        var client = await _tcpListener.AcceptTcpClientAsync();
        client.Client.NoDelay = true;

        return client;
    }

    protected override IPEndPoint Listen(IPEndPoint address, CancellationToken cancellationToken)
    {
        _tcpListener = new TcpListener(address.Address, address.Port);
        Disposables.Add(DisposableItem.Create(_tcpListener, l => l.Stop()));
        _tcpListener.Start();

        return (IPEndPoint)_tcpListener.LocalEndpoint;
    }
}
internal abstract class AsyncSocketCommunicationEndpoint
{
    private readonly TimeSpan _timeout = TimeSpan.FromSeconds(2);
    private bool _stopped;
    private IPEndPoint _endpoint;

    public string Name { get; }

    protected List<DisposableItem> Disposables { get; } = new();

    public AsyncSocketCommunicationEndpoint(string name)
    {
        Name = name;
    }

    protected abstract IPEndPoint Listen(IPEndPoint address, CancellationToken cancellationToken);
    protected abstract Task<TcpClient> ConnectAsync(IPEndPoint address, CancellationToken cancellationToken);

    public Task<string> StartAsync(string address, CancellationToken cancellationToken)
    {
        EqtTrace.Info("AsyncSocketCommunicationEndpoint.Start: Instance '{0}' is connecting to address: {1}", Name, address);

        var endpoint = GetIpEndPoint(address);

        // Server dynamically figures out the address by asking the OS for a free port.
        // Client just returns what it was given.
        // TODO: using for the registration
        var joinedCancellationTokenWithTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, new CancellationTokenSource(_timeout).Token).Token;
        var timeoutRegistration = joinedCancellationTokenWithTimeout.Register(StopEndpoint);
        _endpoint = Listen(endpoint, joinedCancellationTokenWithTimeout);
        timeoutRegistration.Dispose();

        return Task.FromResult(_endpoint.ToString());
    }

    public async Task<CommunicationEndpointHandle> ConnectAsync(Func<CommunicationEndpointHandle, string, CancellationToken, Task>? messageReceivedAsync, CancellationToken cancellationToken)
    {
        var cancellationSource = new CancellationTokenSource();

        var joinedCancellationTokenWithTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationSource.Token, cancellationToken, new CancellationTokenSource(_timeout).Token).Token;
        var timeoutRegistration = joinedCancellationTokenWithTimeout.Register(StopEndpoint);
        var joinedCancellationToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationSource.Token, cancellationToken).Token;
        joinedCancellationToken.Register(StopEndpoint);



        var client = await ConnectAsync(_endpoint, joinedCancellationTokenWithTimeout);
        Disposables.Add(DisposableItem.Create(client));
        // we connected, and are no longer intersted in cancelling on timeout
        timeoutRegistration.Dispose();
        client.Client.NoDelay = true;

        var channel = new LengthPrefixCommunicationChannelAsync(client.GetStream());
        Disposables.Add(DisposableItem.Create(channel));

        var handle = new CommunicationEndpointHandle(_endpoint.ToString(), channel, cancellationSource);
        EqtTrace.Verbose("SocketServer.OnClientConnected: Client connected for endPoint: {0}, starting MessageLoopAsync:", _endpoint);

        var cancelThisMethodOrServerToken = CancellationTokenSource.CreateLinkedTokenSource(joinedCancellationToken, cancellationSource.Token).Token;
        var completion = channel.ProcessAsync(async (handle, message, cancelThisMethodOrServerToken) =>
        {
            if (messageReceivedAsync == null)
            {
                return;
            }

            await messageReceivedAsync(handle, message, cancelThisMethodOrServerToken);
        }, handle, cancelThisMethodOrServerToken);


        handle.Completion = completion;

        return handle;
    }

    private void StopEndpoint()
    {
        if (!_stopped)
        {
            _stopped = true;

            foreach (var disposable in Disposables)
            {
                try { disposable.Dispose(); } catch { }
            }
        }
    }

    internal static IPEndPoint GetIpEndPoint(string? value)
    {
        return Uri.TryCreate(string.Concat("tcp://", value), UriKind.Absolute, out Uri? uri)
            ? new IPEndPoint(IPAddress.Parse(uri.Host), uri.Port < 0 ? 0 : uri.Port)
            : new IPEndPoint(IPAddress.Loopback, 0);
    }
}

internal class DisposableItem<T> : DisposableItem
{
    private readonly object _disposeLock = new();
    private bool _isDisposed;
    private readonly T _item;
    private readonly Action<T> _disposeAction;

    public DisposableItem(T item, Action<T> disposeAction)
    {
        _item = item;
        _disposeAction = disposeAction;
    }

    public override void DisposeInternal()
    {
        if (_isDisposed)
            return;
        lock (_disposeLock)
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            try
            {
                _disposeAction(_item);
            }
            catch
            {
                // do nothing;
            }
        }
    }
}

internal abstract class DisposableItem : IDisposable
{
    public static DisposableItem Create(IDisposable disposable) { return new DisposableItem<IDisposable>(disposable, i => i.Dispose()); }
    public static DisposableItem Create<T>(T item, Action<T> disposeAction) { return new DisposableItem<T>(item, disposeAction); }

    public abstract void DisposeInternal();

    public void Dispose()
    {
        DisposeInternal();
    }
}

internal enum CommunicationEndpointStatus
{
    NotStarted,
    Started,
    Listening,
    Connected
}
