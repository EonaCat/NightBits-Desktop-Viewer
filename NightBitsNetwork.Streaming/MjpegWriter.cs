using System;
using System.Drawing;
using System.IO;
using System.Text;

namespace NightBitsNetwork.Streaming
{
    /// <summary>
    /// Provides a stream writer that can be used to write images as MJPEG to stream
    /// </summary>
    public class MjpegWriter : IDisposable
    {
        public MjpegWriter(Stream stream) : this(stream, "--boundary")
        {
        }

        public MjpegWriter(Stream stream, string boundary)
        {
            Stream = stream;
            Boundary = boundary;
        }

        public string Boundary { get; private set; }

        public Stream Stream { get; private set; }

        public void WriteHeader()
        {
            Write(
                    "HTTP/1.1 200 OK\r\n" +
                    "Content-Type: multipart/x-mixed-replace; boundary=" +
                    Boundary +
                    "\r\n"
                 );

            Stream.Flush();
        }

        public void Write(Image image)
        {
            var memoryStream = BytesOf(image);
            Write(memoryStream);
        }

        public void Write(MemoryStream imageStream)
        {
            var stringBuilder = new StringBuilder();

            stringBuilder.AppendLine();
            stringBuilder.AppendLine(this.Boundary);
            stringBuilder.AppendLine("Content-Type: image/jpeg");
            stringBuilder.AppendLine("Content-Length: " + imageStream.Length);
            stringBuilder.AppendLine();

            Write(stringBuilder.ToString());
            imageStream.WriteTo(Stream);
            Write("\r\n");

            Stream.Flush();
        }

        private void WriteBytes(byte[] data)
        {
            Stream.Write(data, 0, data.Length);
        }

        private void Write(string text)
        {
            var data = BytesOf(text);
            WriteBytes(data);
        }

        private static byte[] BytesOf(string text)
        {
            return Encoding.ASCII.GetBytes(text);
        }

        private static MemoryStream BytesOf(Image image)
        {
            var memoryStream = new MemoryStream();
            image.Save(memoryStream, System.Drawing.Imaging.ImageFormat.Jpeg);
            return memoryStream;
        }

        public string ReadRequest(int length)
        {
            var data = new byte[length];
            var count = Stream.Read(data, 0, data.Length);

            if (count != 0)
            {
                return Encoding.ASCII.GetString(data, 0, count);
            }
            return null;
        }

        public void Dispose()
        {
            try
            {
                if (Stream != null)
                {
                    Stream.Dispose();
                }
            }
            finally
            {
                Stream = null;
            }
        }
    }
}