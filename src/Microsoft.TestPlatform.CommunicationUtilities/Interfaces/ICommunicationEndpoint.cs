// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;

namespace Microsoft.VisualStudio.TestPlatform.CommunicationUtilities.Interfaces;

public interface ICommunicationEndPoint
{
    /// <summary>
    /// Event raised when an endPoint is connected.
    /// </summary>
    event EventHandler<ConnectedEventArgs> Connected;

    /// <summary>
    /// Event raised when an endPoint is disconnected on failure. It should not be notified when we are just closing the connection after success.
    /// </summary>
    event EventHandler<DisconnectedEventArgs> Disconnected;

    /// <summary>
    /// Starts the endPoint and channel.
    /// </summary>
    /// <param name="endPoint">Address to connect</param>
    /// <returns>Address of the connected endPoint</returns>
    string? Start(string endPoint);

    /// <summary>
    /// Stops the endPoint and closes the underlying communication channel.
    /// </summary>
    void Stop();
}

/// <summary>
///  A common interface that represents a communication endpoint over which two processes communicate. It can take a form of a client
///  or of a server. The main difference between a server and a client are who is trying to connect to who. Once connection is established
///  it is a two way communication channel, and there is no distinction between the server and client side.
/// </summary>
public interface IAsyncCommunicationEndPoint
{
    ///// <summary>
    ///// Event raised when an endPoint connects (client) or is connected to (server).
    ///// </summary>
    //event AsyncEventHandler<ConnectedEventArgs> ConnectedAsync;

    ///// <summary>
    ///// Event raised when the other side disconnects unexpectedly. Most likely because of a failure. It should not be notified when we are just closing the connection by <see cref="StopAsync"/>.
    ///// </summary>
    //event AsyncEventHandler<DisconnectedEventArgs> DisconnectedAsync;


    /// <summary>
    /// gets notified when message comes back, all additional context is captured by closure, and user is respoinsible for unsubscribing.
    /// </summary>
    event AsyncEventHandler<IAsyncCommunicationEndPoint, Message> MessageReceivedAsync;

    // vs.

    /// <summary>
    /// gets invoked when a message comes back, the provided context is used for the callback, and once replaced by another registration it is not notified anymore (e.g moving from discovery to run).
    /// </summary>
    /// <typeparam name="TContext"></typeparam>
    /// <param name="messageReceivedAsync"></param>
    /// <param name="context"></param>
    void RegisterHandler<TContext>(Func<IAsyncCommunicationEndPoint, TContext, Message, CancellationToken, Task> messageReceivedAsync, TContext context);



    Task SendAsync(Message message, CancellationToken cancellationToken);

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
    Task ConnectAsync(TimeSpan timeout, CancellationToken cancellationToken);

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

internal class DesignModeClientAsync
{
    public async Task ListenAndProcessRequests(string address, TimeSpan timeout, CancellationToken cancellationToken)
    {
        IAsyncCommunicationEndPoint communicationEndpoint = AsyncCommunicationEndpointFactory.Create(address, ConnectionRole.Client, Transport.Sockets);
        var _ = communicationEndpoint.ListenAsync(cancellationToken);

        var incomingMessages = new WorkQueue<Message>();
        Func<IAsyncCommunicationEndPoint, WorkQueue<Message>, Message, CancellationToken, Task> handleReceivedMessageAsync = (server, context, message, cancellationToken) =>
        // communicationEndpoint.MessageReceivedAsync += async (sender, communicationEndpoint, message, cancellationToken) =>
        {
            switch (message.MessageType)
            {
                case MessageType.StartDiscovery:
                    context.AddJob(message);
                    break;

                case MessageType.CancelDiscovery:
                    context.AddJob(message);
                    break;
            }
        };

        communicationEndpoint.RegisterHandler<WorkQueue<Message>>(handleReceivedMessageAsync, incomingMessages);

        await communicationEndpoint.ConnectAsync(timeout, cancellationToken);


    }
}

internal class WorkQueue<T>
{
    public WorkQueue()
    {
    }

    internal void AddJob(Message message)
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
        Message cancelMessage = null!;
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

        communicationEndpoint.MessageReceivedAsync += async (sender, communicationEndpoint, message, cancellationToken) =>
        {
            switch (message.MessageType)
            {
                case MessageType.TestCasesFound:
                    Message updateMessage = null!;
                    await communicationEndpoint.SendAsync(updateMessage, cancellationToken);
                    break;

                case MessageType.DiscoveryComplete:
                    await communicationEndpoint.StopAsync(cancellationToken);
                    var result = new DiscoveryCompleteEventArgs();
                    discoveryDone.SetResult(result);
                    break;
            };
        };

        Message discoveryMessage = null!;
        await communicationEndpoint.SendAsync(discoveryMessage, cancellationToken);
        var discoveryCompleteEventArgs = await discoveryDone.Task;

       return discoveryCompleteEventArgs;
    }

    public async Task<IAsyncCommunicationEndPoint> StartSessionAsync(CancellationToken cancellationToken)
    {
        // async lock over all of this, passing the cancellation token to the wait
        // so we dont' start 2 sessions at the same time accidentally.

        TimeSpan connectionTimeout = TimeSpan.FromSeconds(90);

        IAsyncCommunicationEndPoint communicationEndpoint = AsyncCommunicationEndpointFactory.Create("127.0.0.1:0", ConnectionRole.Client, Transport.Sockets);

        var realAddress = await communicationEndpoint.ListenAsync(cancellationToken);

        await StartTheOtherProcessAsync(realAddress, cancellationToken);

        await communicationEndpoint.ConnectAsync(connectionTimeout, cancellationToken);

        // detect error here?


        return communicationEndpoint;
    }

    private Task StartTheOtherProcessAsync(string realAddress, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}

internal class AsyncCommunicationEndpointFactory
{
    internal static IAsyncCommunicationEndPoint Create(string address, ConnectionRole client, Transport sockets)
    {
        throw new NotImplementedException();
    }
}

public delegate Task AsyncEventHandler<TEventArgs>(object? sender, TEventArgs e, CancellationToken cancellationToken);
public delegate Task AsyncEventHandler<TEventArgs, TAdditionalEventArgs>(object? sender, TEventArgs e, TAdditionalEventArgs e2, CancellationToken cancellationToken);
