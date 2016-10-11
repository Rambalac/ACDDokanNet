namespace Azi.Cloud.DokanNet
{
    using System;
    using System.IO;
    using System.Threading;
    using Tools;

    public class FileBlockReader : AbstractBlockStream
    {
        private const int WaitForFile = 50;

        private readonly FileStream stream;
        private readonly long expectedLength;
        private readonly string filePath;
        private bool disposedValue; // To detect redundant calls

        private FileBlockReader(string path, long length)
        {
            filePath = path;
            expectedLength = length;
            stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        }

        public static FileBlockReader Open(string filePath, long length)
        {
            var result = new FileBlockReader(filePath, length);

            return result;
        }

        public override void Close()
        {
            Log.Trace(Path.GetFileName(filePath));

            stream.Dispose();

            base.Close();
        }

        public override int Read(long position, byte[] buffer, int offset, int count, int timeout)
        {
            if (count == 0 || expectedLength == 0)
            {
                return 0;
            }

            var timeouttime = DateTime.UtcNow.AddMilliseconds(timeout);
            int red;

            lock (stream)
            {
                int totalred = 0;
                stream.Position = position;
                do
                {
                    red = stream.Read(buffer, offset, Math.Min(count, buffer.Length - offset));
                    totalred += red;
                    offset += red;
                    count -= red;
                    if (stream.Position < expectedLength && red == 0)
                    {
                        Thread.Sleep(WaitForFile);
                    }

                    if (DateTime.UtcNow > timeouttime)
                    {
                        throw new TimeoutException();
                    }
                }
                while (stream.Position < expectedLength && count > 0);
                return totalred;
            }
        }

        public override void Write(long position, byte[] buffer, int offset, int count, int timeout = 1000)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long len)
        {
            throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        protected override void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    stream.Dispose();
                }

                disposedValue = true;
            }
        }
    }
}