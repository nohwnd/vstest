using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities;
using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SocketPerf2
{
    class Program
    {
        static void Main(string[] _)
        {
            // Measure the throughput with socket communication v2 (SocketServer, SocketClient)
            // implementation.
            var server = new SocketServer();
            var client = new SocketClient();
            ICommunicationChannel serverChannel = null;
            ICommunicationChannel clientChannel = null;
            ManualResetEventSlim dataTransferred = new ManualResetEventSlim(false);
            ManualResetEventSlim clientConnected = new ManualResetEventSlim(false);
            ManualResetEventSlim serverConnected = new ManualResetEventSlim(false);
            int dataReceived = 0;
            var watch = new Stopwatch();
            var thread = new Thread(() => SendData(clientChannel, watch));

            // Setup server
            server.Connected += (sender, args) =>
            {
                serverChannel = args.Channel;
                serverChannel.MessageReceived += (channel, messageReceived) =>
                {
                    // Keep count of bytes
                    dataReceived += messageReceived.Data.Length;

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

            var port = server.Start(IPAddress.Loopback.ToString() + ":0");
            client.Start(port);

            clientConnected.Wait();
            serverConnected.Wait();
            thread.Join();
            dataTransferred.Wait();

            watch.Stop();
            Console.WriteLine("Elapsed: " + watch.Elapsed);
            Console.ReadLine();
        }

        private static void SendData(ICommunicationChannel channel, Stopwatch watch)
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
                channel.Send(dataBytesStr);
            }
        }
    }


}
