// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities.Interfaces;
using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestPlatform.PlatformAbstractions;

namespace Microsoft.VisualStudio.TestPlatform.CommunicationUtilities;

/// <summary>
///  A common interface that represents a communication endpoint over which two processes communicate. It can take a form of a client
///  or of a server. The main difference between a server and a client are who is trying to connect to who. Once connection is established
///  it is a two way communication channel, and there is no distinction between the server and client side.
/// </summary>
internal interface IAsyncCommunicationEndPoint<TMessage>
{
    ///// <summary>
    ///// gets notified when message comes back, all additional context is captured by closure, and user is respoinsible for unsubscribing.
    ///// </summary>
    //event AsyncEventHandler<IAsyncCommunicationEndPoint, Message> MessageReceivedAsync;

    //// vs.

    /// <summary>
    /// gets invoked when a message comes back, the provided context is used for the callback, and once replaced by another registration it is not notified anymore (e.g moving from discovery to run).
    /// </summary>
    /// <typeparam name="TContext"></typeparam>
    /// <param name="messageReceivedAsync"></param>
    /// <param name="context"></param>
    // TODO: rename to SetHandler, because we can replace one with another
    void SetHandler<TContext>(Func<IAsyncCommunicationEndPoint<TMessage>, TContext, TMessage, CancellationToken, Task> messageReceivedAsync, TContext context);

    Task SendAsync(TMessage message, CancellationToken cancellationToken);

    /// <summary>
    /// Starts listening on the endPoint, and returns the actual address to be used to connect to it.
    /// A server should use this to reseve OS resources, such as getting a port and starting listening on it.
    /// Clients can simply return the given endpoint address. 
    /// </summary>
    /// <returns>The real address on which this endpoint is reachable.</returns>
    Task<string> ListenAsync(CancellationToken cancellationToken);

    /// <summary>
    /// A client connects to an endpoint. A server waits for a client to connect to it.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task ConnectAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Stop the current connection, and dispose all the resources. This should not notify <see cref="DisconnectedAsync" >.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task StopAsync(CancellationToken cancellationToken);

    /// <summary>
    /// A task that is completed when the endpoint stops or on failure.
    /// </summary>
    Task Completion { get; }
}

internal class StringMessage
{
    public StringMessage(string messageType, int version, string data)
    {
        MessageType = messageType;
        Version = version;
        Data = data;
    }

    /// <summary>
    /// MessageType, that is used as routing information to figure out what the request means and who should handle it. e.g. "TestDiscovery.Start";
    /// </summary>
    public string MessageType { get; }
    public int Version { get; }
    public string Data { get; }

    public override string ToString()
    {
        return $"({MessageType}) -> {Data}";
    }
}

internal interface IAsyncCommunicationEndPoint : IAsyncCommunicationEndPoint<StringMessage>
{

}

internal class AsyncCommunicationEndPointWrapper : IAsyncCommunicationEndPoint
{
    private readonly IAsyncCommunicationEndPoint<StringMessage> _communicationEndPoint;

    public AsyncCommunicationEndPointWrapper(IAsyncCommunicationEndPoint<StringMessage> communicationEndPoint)
    {
        _communicationEndPoint = communicationEndPoint;
    }

    public Task Completion => _communicationEndPoint.Completion;

    public Task ConnectAsync(CancellationToken cancellationToken)
    {
        return _communicationEndPoint.ConnectAsync(cancellationToken);
    }

    public Task<string> ListenAsync(CancellationToken cancellationToken)
    {
        return _communicationEndPoint.ListenAsync(cancellationToken);
    }

    public Task SendAsync(StringMessage message, CancellationToken cancellationToken)
    {
        return _communicationEndPoint.SendAsync(message, cancellationToken);
    }

    public void SetHandler<TContext>(Func<IAsyncCommunicationEndPoint<StringMessage>, TContext, StringMessage, CancellationToken, Task> messageReceivedAsync, TContext context)
    {
        _communicationEndPoint.SetHandler(messageReceivedAsync, context);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return _communicationEndPoint.StopAsync(cancellationToken);
    }
}

internal abstract class AsyncTcpCommunicationEndpoint<TMessage> : IAsyncCommunicationEndPoint<TMessage>, IDisposable
{
    private readonly string _description;
    private readonly string _providedAddress;
    private readonly ConnectionRole _role;
    private readonly CancellationTokenSource _cancellation = new();
    private readonly AsyncLock _handlerLock = new();
    protected readonly DisposableCollection _disposables = new();
    private readonly TimeSpan _timeout;

    private IPEndPoint? _actualAddress;

    private readonly IMessageSerializer<TMessage> _messageSerializer;
    private AsyncLengthPrefixCommunicationChannel? _channel;
    private Func<IAsyncCommunicationEndPoint<TMessage>, object?, TMessage, CancellationToken, Task>? _handleMessageAsync;
    private object? _context;

    private static readonly Task CompletedTask =
#if NET451
            Task.FromResult(0);
#else
    Task.CompletedTask;
#endif

    public AsyncTcpCommunicationEndpoint(string description, string address, ConnectionRole role, TimeSpan timeout, IMessageSerializer<TMessage> messageSerializer)
    {
        _description = description;
        _providedAddress = address;
        _role = role;
        _timeout = timeout;
        _messageSerializer = messageSerializer;
    }

    public Task? Completion { get; private set; }

    protected abstract IPEndPoint TcpListen(IPEndPoint address, CancellationToken cancellationToken);
    protected abstract Task<TcpClient> TcpConnectAsync(IPEndPoint address, CancellationToken cancellationToken);

    public Task<string> ListenAsync(CancellationToken cancellationToken)
    {
        if (_role == ConnectionRole.Host)
        {
            EqtTrace.Info("AsyncSocketCommunicationEndpoint.Start: Instance '{0}' is starting with address: {1}", _description, _providedAddress);
        }

        var endpoint = GetIpEndPoint(_providedAddress);

        // Listen: Server dynamically figures out the address by asking the OS for a free port. Client just returns what it was given.
        _actualAddress = TcpListen(endpoint, cancellationToken);
        EqtTrace.Info("AsyncSocketCommunicationEndpoint.Start: Instance '{0}' is listening on address: {1}", _description, _actualAddress);

        return Task.FromResult(_actualAddress.ToString());
    }

    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        if (_actualAddress == null)
        {
            throw new InvalidOperationException("Address is null, call Listen first.");
        }

        if (_handleMessageAsync == null)
        {
            throw new InvalidOperationException("Handler is null, call SetHandler first.");
        }

        var cancellationSource = new CancellationTokenSource();

        var joinedCancellationTokenWithTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationSource.Token, cancellationToken, new CancellationTokenSource(_timeout).Token).Token;
        TcpClient? tcpClient = null;
        using (joinedCancellationTokenWithTimeout.Register(StopEndpoint))
        {
            tcpClient = await TcpConnectAsync(_actualAddress, joinedCancellationTokenWithTimeout);
        } // we connected, and are no longer interested in cancelling on timeout

        _disposables.Add(tcpClient);
        tcpClient.Client.NoDelay = true;

        _channel = new AsyncLengthPrefixCommunicationChannel(tcpClient.GetStream());
        _disposables.Add(_channel);

        EqtTrace.Verbose("SocketServer.OnClientConnected: Client connected for endPoint: {0}, starting MessageLoopAsync:", _actualAddress);
        var joinedCancellationToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationSource.Token, cancellationToken).Token;
        joinedCancellationToken.Register(StopEndpoint);

        Completion = _channel.ProcessAsync(async (stringMessage, cancellationToken) =>
        {
            var message = _messageSerializer.DeserializeMessage(stringMessage);
            using (await _handlerLock.LockAsync())
            {
                await _handleMessageAsync(this, _context, message, cancellationToken);
            }
        }, joinedCancellationToken);
    }

    public async Task SendAsync(TMessage message, CancellationToken cancellationToken)
    {
        if (_channel == null)
        {
            throw new InvalidOperationException("Channel is null, call Connect first.");
        }

        var stringMessage = _messageSerializer.SerializeMessage(message);
        await _channel.SendAsync(stringMessage, cancellationToken);
    }

    public void SetHandler<TContext>(Func<IAsyncCommunicationEndPoint<TMessage>, TContext, TMessage, CancellationToken, Task> messageReceivedAsync, TContext context)
    {
        using (_handlerLock.LockSync())
        {
            _handleMessageAsync = (self, context, message, cancellationToken) => messageReceivedAsync(self, (TContext)context!, message, cancellationToken);
            _context = context;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _cancellation.Cancel();
        _disposables.Dispose();
        return CompletedTask;
    }

    private void StopEndpoint()
    {
        _disposables.Dispose();
    }

    private static IPEndPoint GetIpEndPoint(string? value)
    {
        return Uri.TryCreate(string.Concat("tcp://", value), UriKind.Absolute, out Uri? uri)
            ? new IPEndPoint(IPAddress.Parse(uri.Host), uri.Port < 0 ? 0 : uri.Port)
            : new IPEndPoint(IPAddress.Loopback, 0);
    }

    public void Dispose()
    {
        _disposables.Dispose();
    }
}

internal interface IMessageSerializer<TMessage>
{
    TMessage DeserializeMessage(string stringMessage);
    string SerializeMessage(TMessage message);
}

internal sealed class DisposableCollection : List<IDisposable>, IDisposable
{
    private bool _disposed;

    public void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                foreach (var disposable in this)
                {
                    try { disposable.Dispose(); } catch { }
                }

                Clear();
            }

            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}

internal class AsyncLengthPrefixCommunicationChannel : IDisposable
{
    private readonly Stream _stream;
    private readonly AsyncLock _writeLock = new();
    private readonly Stream _bufferedWriter;
    private readonly BinaryWriter _writer;

    public AsyncLengthPrefixCommunicationChannel(Stream stream)
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

    public async Task ProcessAsync(Func<string, CancellationToken, Task> onMessageReceivedAsync, CancellationToken cancellationToken)
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

            var data = Encoding.UTF8.GetString(buffer, dataStart, dataEnd);
            stringBuilder.Append(data);

            var remainingBytes = remainingBytesCount - readCount;
            while (remainingBytes > 0)
            {
                var dataReadSize = await stream.ReadAsync(buffer, 0, Math.Min(bufferSize, remainingBytes), cancellationToken);
                stringBuilder.Append(Encoding.UTF8.GetString(buffer, 0, dataReadSize));
                remainingBytes -= dataReadSize;
            }

            await onMessageReceivedAsync(stringBuilder.ToString(), cancellationToken);
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

    public async Task SendAsync(string data, CancellationToken cancellationToken)
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

internal class AsyncTcpClient<TMessage> : AsyncTcpCommunicationEndpoint<TMessage>
{
    public AsyncTcpClient(string description, string address, TimeSpan timeout, IMessageSerializer<TMessage> messageSerializer)
        : base(description, address, ConnectionRole.Client, timeout, messageSerializer)
    {
    }

    protected override async Task<TcpClient> TcpConnectAsync(IPEndPoint address, CancellationToken cancellationToken)
    {
        var tcpClient = new TcpClient { NoDelay = true };
        //TODO: cancel when cancellation token is cancelled
        await tcpClient.ConnectAsync(address.Address, address.Port);

        return tcpClient;
    }

    protected override IPEndPoint TcpListen(IPEndPoint address, CancellationToken cancellationToken)
    {
        // This is a client and we don't need to start listening to figure out the real address.
        return address;
    }
}

internal class AsyncTcpServer<TMessage> : AsyncTcpCommunicationEndpoint<TMessage>
{
    private TcpListener? _tcpListener;

    public AsyncTcpServer(string description, string address, TimeSpan timeout, IMessageSerializer<TMessage> messageSerializer)
        : base(description, address, ConnectionRole.Host, timeout, messageSerializer)
    {
    }

    protected override async Task<TcpClient> TcpConnectAsync(IPEndPoint address, CancellationToken cancellationToken)
    {
        if (_tcpListener == null)
        {
            throw new InvalidOperationException("TcpListener is null, call TcpListen first");
        }

        // TODO: cancel on cancellation token
#if NET
        var client = await _tcpListener.AcceptTcpClientAsync(cancellationToken);
#else
        var client = await _tcpListener.AcceptTcpClientAsync();
#endif

        return client;
    }

    protected override IPEndPoint TcpListen(IPEndPoint address, CancellationToken cancellationToken)
    {
        _tcpListener = new TcpListener(address.Address, address.Port);
        _disposables.Add(new DisposableAdapter<TcpListener>(_tcpListener, l => l.Stop()));
        _tcpListener.Start();

        return (IPEndPoint)_tcpListener.LocalEndpoint;
    }
}

internal class DisposableAdapter<T> : IDisposable
{
    private readonly T _item;
    private readonly Action<T> _disposeAction;

    public DisposableAdapter(T item, Action<T> disposeAction)
    {
        _item = item;
        _disposeAction = disposeAction;
    }

    public void Dispose()
    {
        try { _disposeAction(_item); } catch { }
    }
}


// Stephen Toubs code: https://www.hanselman.com/blog/comparing-two-techniques-in-net-asynchronous-coordination-primitives
// except for LockSync
internal sealed class AsyncLock
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

    public IDisposable LockSync()
    {
        //TODO: this is probably bad. But so far I only need it when exchanging handlers which is very infrequent.
        return LockAsync().GetAwaiter().GetResult();
    }
}



internal class DesignModeClientAsync
{
    public async Task ListenAndProcessRequests(string address, TimeSpan timeout, CancellationToken cancellationToken)
    {
        IAsyncCommunicationEndPoint communicationEndpoint = new AsyncCommunicationEndpointFactory().Create("DesignModeClient", address, ConnectionRole.Client, Transport.Sockets);
        var _ = communicationEndpoint.ListenAsync(cancellationToken);

        var incomingMessages = new WorkQueue<StringMessage>();
        Func<IAsyncCommunicationEndPoint<StringMessage>, WorkQueue<StringMessage>, StringMessage, CancellationToken, Task> handleReceivedMessageAsync = async (server, context, message, cancellationToken) =>
        {
            switch (message.MessageType)
            {
                case MessageType.StartDiscovery:
                    await context.AddJobAsync(message);
                    break;

                case MessageType.CancelDiscovery:
                    await context.AddJobAsync(message);
                    break;
            }
        };

        communicationEndpoint.SetHandler<WorkQueue<StringMessage>>(handleReceivedMessageAsync, incomingMessages);

        await communicationEndpoint.ConnectAsync(cancellationToken);


    }
}

internal class WorkQueue<TMessage>
{
    public WorkQueue()
    {
    }

    internal Task AddJobAsync(TMessage message)
    {
        throw new NotImplementedException();
    }
}

internal class TranslationLayerAsync
{
    public async Task CancelAsync(CancellationToken cancellationToken)
    {
        // if session is not started just return

        IAsyncCommunicationEndPoint communicationEndpoint = null!;
        StringMessage cancelMessage = null!;
        await communicationEndpoint.SendAsync(cancelMessage, cancellationToken);

        var taskCompletionSource = new TaskCompletionSource<bool>();
        using (cancellationToken.Register((context) => ((TaskCompletionSource<bool>)context!).SetResult(true), taskCompletionSource))
        {
            await Task.WhenAny(taskCompletionSource.Task, communicationEndpoint.Completion);
        }
    }

    public async Task<DiscoveryCompleteEventArgs> DiscoverTestsAsync(CancellationToken cancellationToken)
    {
        var communicationEndpoint = await StartSessionAsync(cancellationToken);


        TaskCompletionSource<DiscoveryCompleteEventArgs> discoveryDone = new();

        communicationEndpoint.SetHandler<object?>(async (communicationEndpoint, _, message, cancellationToken) =>
        {
            switch (message.MessageType)
            {
                case MessageType.TestCasesFound:
                    StringMessage updateMessage = null!;
                    await communicationEndpoint.SendAsync(updateMessage, cancellationToken);
                    break;

                case MessageType.DiscoveryComplete:
                    await communicationEndpoint.StopAsync(cancellationToken);
                    var result = new DiscoveryCompleteEventArgs();
                    discoveryDone.SetResult(result);
                    break;
            };
        }, context: null);

        StringMessage discoveryMessage = null!;
        await communicationEndpoint.SendAsync(discoveryMessage, cancellationToken);
        var discoveryCompleteEventArgs = await discoveryDone.Task;

        return discoveryCompleteEventArgs;
    }

    public async Task<IAsyncCommunicationEndPoint> StartSessionAsync(CancellationToken cancellationToken)
    {
        // async lock over all of this, passing the cancellation token to the wait
        // so we dont' start 2 sessions at the same time accidentally.

        IAsyncCommunicationEndPoint communicationEndpoint = new AsyncCommunicationEndpointFactory().Create("Translation layer", "127.0.0.1:0", ConnectionRole.Client, Transport.Sockets);

        var realAddress = await communicationEndpoint.ListenAsync(cancellationToken);

        await StartTheOtherProcessAsync(realAddress, cancellationToken);

        await communicationEndpoint.ConnectAsync(cancellationToken);

        // detect error here?


        return communicationEndpoint;
    }

    private Task StartTheOtherProcessAsync(string realAddress, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}


internal class AsyncCommunicationEndpointFactory : IAsyncCommunicationEndpointFactory
{
    private readonly TimeSpan _timeout;

    public AsyncCommunicationEndpointFactory()
    {
        // TODO: init from env variable. Or can we configure it also in some other way?
        _timeout = TimeSpan.FromSeconds(90);
    }

    public IAsyncCommunicationEndPoint Create(string name, string address, ConnectionRole role, Transport transport)
    {
        var messageSerializer = new StringMessageSerializer();
        return transport switch
        {
            Transport.Sockets => role switch
            {
                ConnectionRole.Host => new AsyncCommunicationEndPointWrapper(new AsyncTcpServer<StringMessage>(name, address, _timeout, messageSerializer)),
                ConnectionRole.Client => new AsyncCommunicationEndPointWrapper(new AsyncTcpClient<StringMessage>(name, address, _timeout, messageSerializer)),
                _ => throw new NotSupportedException(),
            },
            _ => throw new NotSupportedException(),
        };
    }
}

internal class StringMessageSerializer : IMessageSerializer<StringMessage>
{
    public StringMessageSerializer()
    {
    }

    public StringMessage DeserializeMessage(string stringMessage)
    {
        throw new NotImplementedException();
    }

    public string SerializeMessage(StringMessage message)
    {
        throw new NotImplementedException();
    }
}
