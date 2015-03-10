using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Windows.Forms;
using NightBitsLibrary.ThreadOperations;

namespace NightBitsNetwork.Streaming
{
    /// <summary>
    ///     Provides a streaming server that can be used to stream any images source
    ///     to any client.
    /// </summary>
    public class StreamingImageServer : IDisposable
    {
        private readonly List<Socket> _clients;
        private Thread _thread;
        private RichTextBox _logBook;
        private Socket _server;
        private object _batton = new object();

        // Create the constructor
        public StreamingImageServer(int width = 800, int height = 600, bool showCursor = true) : this(Screen.Snapshots(width, height, showCursor))
        {
            // Do nothing
        }

        public StreamingImageServer(IEnumerable<Image> imagesSource)
        {
            _clients = new List<Socket>();
            _thread = null;

            ImagesSource = imagesSource;
            Interval = 50;
        }

        /// <summary>
        ///     Gets or sets the source of images that will be streamed to the
        ///     any connected client.
        /// </summary>
        public IEnumerable<Image> ImagesSource { get; set; }

        /// <summary>
        ///     Gets or sets the interval in milliseconds (or the delay time) between
        ///     the each image and the other of the stream (the default is .
        /// </summary>
        public int Interval { get; set; }

        /// <summary>
        ///     Gets a collection of client sockets.
        /// </summary>
        public IEnumerable<Socket> Clients
        {
            get { return _clients; }
        }

        public void SetLogBook(RichTextBox logBook)
        {
            _logBook = logBook;
        }

        /// <summary>
        ///     Returns the status of the server. True means the server is currently
        ///     running and ready to serve any client requests.
        /// </summary>
        public bool IsRunning
        {
            get { return (_thread != null && _thread.IsAlive); }
        }

        public void Dispose()
        {
            Stop();
        }

        /// <summary>
        ///     Starts the server to accepts any new connections on the specified port.
        /// </summary>
        /// <param name="port"></param>
        public void Start(int port)
        {
            lock (_batton)
            {
                _thread = new Thread(ServerThread) { IsBackground = true };
                _thread.Start(port);
            }
        }

        /// <summary>
        ///     Starts the server to accepts any new connections
        /// </summary>
        public void Start()
        {
            Start(8080);
        }

        public void Stop()
        {
            if (IsRunning)
            {
                try
                {
                    _server.Close();
                    //TODO: Abrupt abortion for now FIX!!!!!
                    _thread.Abort();
                }
                finally
                {
                    lock (_clients)
                    {
                        foreach (var currentClient in _clients)
                        {
                            try
                            {
                                currentClient.Close();
                            }
                            catch (Exception exception)
                            {
                                WriteToLogbook(exception.Message);
                            }
                        }
                        _clients.Clear();
                    }
                    _thread = null;
                }
            }
        }

        /// <summary>
        /// Write a message to the internal logbook
        /// </summary>
        /// <param name="message"></param>
        private void WriteToLogbook(String message)
        {
            if (_logBook != null)
            {
                _logBook.UIThread(() => _logBook.AppendText(message + Environment.NewLine));
            }
            else
            {
                Debug.WriteLine(message);
            }
        }

        /// <summary>
        ///     This the main thread of the server that serves all the new
        ///     connections from clients.
        /// </summary>
        /// <param name="state"></param>
        private void ServerThread(object state)
        {
            try
            {
                _server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                _server.Bind(new IPEndPoint(IPAddress.Any, (int)state));
                _server.Listen(10);

                WriteToLogbook(string.Format("Server started on port: {0}.", state));

                foreach (var client in _server.IncomingConnections())
                {
                    ThreadPool.QueueUserWorkItem(ClientThread, client);
                }
            }
            catch (ThreadAbortException threadAbortException)
            {
                WriteToLogbook(string.Format("Server stopped"));
            }
            catch (Exception exception)
            {
                WriteToLogbook(exception.Message);
            }

            Stop();
        }

        /// <summary>
        ///     Each client connection will be served by this thread.
        /// </summary>
        /// <param name="client"></param>
        private void ClientThread(object client)
        {
            var socket = (Socket)client;

            WriteToLogbook(string.Format("New client from: {0}", socket.RemoteEndPoint));

            lock (_clients)
            {
                _clients.Add(socket);
            }

            try
            {
                using (var mjpegWriter = new MjpegWriter(new NetworkStream(socket, true)))
                {
                    // Writes the response header to the client.
                    mjpegWriter.WriteHeader();

                    // Streams the images from the source to the client.
                    foreach (var imageStream in ImagesSource.Streams())
                    {
                        if (Interval > 0)
                        {
                            Thread.Sleep(Interval);
                        }

                        mjpegWriter.Write(imageStream);
                    }
                }
            }
            catch (Exception exception)
            {
                WriteToLogbook(exception.Message);
            }
            finally
            {
                lock (_clients)
                {
                    _clients.Remove(socket);
                }
            }
        }
    }

    internal static class SocketExtensions
    {
        public static IEnumerable<Socket> IncomingConnections(this Socket server)
        {
            while (true)
            {
                yield return server.Accept();
            }
        }
    }

    internal static class Screen
    {
        public static IEnumerable<Image> Snapshots()
        {
            return Snapshots(System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width,
                System.Windows.Forms.Screen.PrimaryScreen.Bounds.Height, true);
        }

        /// <summary>
        ///     Returns a
        /// </summary>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="showCursor"></param>
        /// <returns></returns>
        public static IEnumerable<Image> Snapshots(int width, int height, bool showCursor)
        {
            var size = new Size(System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width,
                System.Windows.Forms.Screen.PrimaryScreen.Bounds.Height);

            var sourceImage = new Bitmap(size.Width, size.Height);
            var sourceGraphics = Graphics.FromImage(sourceImage);

            var scaled = (width != size.Width || height != size.Height);

            var destinationImage = sourceImage;
            var destinationGraphics = sourceGraphics;

            if (scaled)
            {
                destinationImage = new Bitmap(width, height);
                destinationGraphics = Graphics.FromImage(destinationImage);
            }

            var source = new Rectangle(0, 0, size.Width, size.Height);
            var destination = new Rectangle(0, 0, width, height);
            var currentSize = new Size(32, 32);

            while (true)
            {
                sourceGraphics.CopyFromScreen(0, 0, 0, 0, size);

                if (showCursor)
                {
                    Cursors.Default.Draw(sourceGraphics, new Rectangle(Cursor.Position, currentSize));
                }

                if (scaled)
                {
                    destinationGraphics.DrawImage(sourceImage, destination, source, GraphicsUnit.Pixel);
                }
                yield return destinationImage;
            }
        }

        internal static IEnumerable<MemoryStream> Streams(this IEnumerable<Image> source)
        {
            var memoryStream = new MemoryStream();

            foreach (var image in source)
            {
                memoryStream.SetLength(0);
                image.Save(memoryStream, ImageFormat.Jpeg);
                yield return memoryStream;
            }

            memoryStream.Close();
        }
    }
}