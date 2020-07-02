using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities;
using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities.Interfaces;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SocketPerf
{
    class Program
    {
        static int messageSize = 65536;
        static int messageCount = 20000;

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
            var watch = Stopwatch.StartNew();
            var thread = new Thread(() => SendData(clientChannel, watch));


            // Setup server
            server.Connected += (sender, args) =>
            {
                serverChannel = args.Channel;
                var counter = 0;
                serverChannel.MessageReceived += (channel, messageReceived) =>
                {
                    // Keep count of bytes
                    var o = JsonDataSerializer.Instance.Deserialize<TestResult>(messageReceived.Data);
                    counter++;

                    if (counter >= messageCount)
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
            Console.WriteLine( $"Transfer took: {watch.ElapsedMilliseconds} ms ({watch.ElapsedMilliseconds / messageCount} ms per message)");
            Console.ReadLine();
        }

        private static void SendData(ICommunicationChannel channel, Stopwatch watch)
        {
            var dataBytes = new byte[messageSize];
            for (int i = 0; i < dataBytes.Length; i++)
            {
                dataBytes[i] = 0x65;
            }

            var dataBytesStr = System.Text.Encoding.UTF8.GetString(dataBytes);

            Console.WriteLine($"Setup took {watch.ElapsedMilliseconds}");
            
            watch.Start();
            for (int i = 0; i < messageCount; i++)
            {
//                channel.Send(dataBytesStr);
                
                var result = new TestResult(new TestCase("namealskdfjaslkdfjaslkdfj", new Uri("http://google.com"),"lskjdflaksjdflkasjdflkajsldkfj"));

                channel.Send(JsonDataSerializer.Instance.Serialize(result, 2));
                
            }
        }
    }


}
