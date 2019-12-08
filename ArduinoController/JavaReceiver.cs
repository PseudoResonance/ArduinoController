using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ArduinoController
{
    class JavaReceiver
    {
        public static Thread receiverThread;

        public static String ip = "127.0.0.1";
        public static int port = 2400;
        private static IPAddress socketAddress;
        private static IPEndPoint socketEndpoint;
        private static Socket socket;
        private static bool isOpen;

        private static ManualResetEvent socketReset = new ManualResetEvent(false);

        public static void SetupReceiver()
        {
            SetupSocket(ip, port);
        }

        public static void SetSocketIP(String ip)
        {
            JavaReceiver.ip = ip;
            CloseSocket();
            SetupSocket(ip, port);
        }

        public static void SetSocketPort(int port)
        {
            JavaReceiver.port = port;
            CloseSocket();
            SetupSocket(ip, port);
        }

        private static void SetupSocket(String ip, int port)
        {
            Console.WriteLine("Opening socket");
            socketAddress = IPAddress.Parse(ip);
            socketEndpoint = new IPEndPoint(socketAddress, port);
            receiverThread = new Thread(() =>
            {
                Thread.CurrentThread.IsBackground = true;
                while (!MainWindow.shutdown)
                {
                    RunSocket();
                }
            });
            receiverThread.Start();
        }

        private static void RunSocket()
        {
            try
            {
                socket = new Socket(socketAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                socket.Bind(socketEndpoint);
                socket.Listen(1);
                isOpen = true;

                while (isOpen)
                {
                    socketReset.Reset();
                    Console.WriteLine("Listening for connections on: " + socketAddress.ToString() + ":" + port);
                    socket.BeginAccept(new AsyncCallback(JavaReceiver.AcceptCallback), socket);
                    socketReset.WaitOne();
                }
            }
            catch (SocketException)
            {
                Console.WriteLine("Error while opening socket!");
            }
            Console.WriteLine("Closing socket!");
        }

        public static void AcceptCallback(IAsyncResult ar)
        {
            socketReset.Set();

            if (!isOpen) return;

            Socket listener = (Socket) ar.AsyncState;
            Socket handler = listener.EndAccept(ar);

            StateObject state = new StateObject();
            state.workSocket = handler;
            handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(JavaReceiver.ReadCallback), state);
        }

        public static void ReadCallback(IAsyncResult ar)
        {
            StateObject state = (StateObject) ar.AsyncState;
            Socket handler = state.workSocket;

            // Read data from the client socket.  
            int read = handler.EndReceive(ar);

            // Data was read from the client socket.  
            if (read > 0)
            {
                state.sb.Append(Encoding.ASCII.GetString(state.buffer, 0, read));
                handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                    new AsyncCallback(ReadCallback), state);
            }
            else
            {
                if (state.sb.Length > 1)
                {
                    // All the data has been read from the client;  
                    // display it on the console.  
                    string content = state.sb.ToString();
                    Console.WriteLine($"Read {content.Length} bytes from socket.\n Data : {content}");
                }
                handler.Close();
            }
        }

        private static void CloseSocket()
        {
            isOpen = false;
            socket.Close();
            receiverThread.Abort();
        }
    }

    public class StateObject
    {
        public Socket workSocket = null;
        public const int BufferSize = 4096;
        public byte[] buffer = new byte[BufferSize];
        public StringBuilder sb = new StringBuilder();
    }
}
