namespace Azi.Cloud.DokanNet
{
    using System;
    using System.Collections.Concurrent;
    using System.IO;
    using System.Threading;
    using Azi.Tools;

    public class FileBlockReader : AbstractBlockStream
    {
        private const int WaitForFile = 50;

        private readonly ConcurrentBag<FileStream> files;
        private readonly long expectedLength;
        private readonly string filePath;
        private readonly object closeLock = new object();
        private bool disposedValue; // To detect redundant calls
        private int closed;

        private FileBlockReader(string path, long length)
        {
            filePath = path;
            expectedLength = length;
            files = new ConcurrentBag<FileStream>();
        }

        public static FileBlockReader Open(string filePath, long length)
        {
            var result = new FileBlockReader(filePath, length);

            result.ReleaseFile(new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
            return result;
        }

        public override void Close()
        {
            if (Interlocked.CompareExchange(ref closed, 1, 0) == 1)
            {
                return;
            }

            Log.Trace(Path.GetFileName(filePath));

            foreach (var file in files)
            {
                file.Close();
            }

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
            var file = GetFile();
            int totalred = 0;
            try
            {
                file.Position = position;
                do
                {
                    red = file.Read(buffer, offset, Math.Min(count, buffer.Length - offset));
                    totalred += red;
                    offset += red;
                    count -= red;
                    if (file.Position < expectedLength && red == 0)
                    {
                        Thread.Sleep(WaitForFile);
                    }

                    if (DateTime.UtcNow > timeouttime)
                    {
                        throw new TimeoutException();
                    }
                }
                while (file.Position < expectedLength && count > 0);
                return totalred;
            }
            finally
            {
                ReleaseFile(file);
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
                    Close();
                }

                disposedValue = true;
            }
        }

        private FileStream GetFile()
        {
            FileStream result;
            if (files.TryTake(out result))
            {
                return result;
            }

            lock (closeLock)
            {
                if (closed == 1)
                {
                    throw new IOException("File is already closed");
                }

                return new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            }
        }

        private void ReleaseFile(FileStream file)
        {
            lock (closeLock)
            {
                if (closed != 1)
                {
                    files.Add(file);
                }
                else
                {
                    file.Close();
                }
            }
        }
    }
}