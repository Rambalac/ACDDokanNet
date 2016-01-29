using Azi.Amazon.CloudDrive;
using Azi.Amazon.CloudDrive.JsonObjects;
using Azi.Tools;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Diagnostics;

namespace Azi.ACDDokanNet
{

    public class FileBlockReader : AbstractBlockStream
    {
        private readonly ConcurrentBag<FileStream> files;
        private readonly long expectedLength;
        private readonly string filePath;
        private object closeLock = new object();

        private FileBlockReader(string path, long length)
        {
            filePath = path;
            expectedLength = length;
            files = new ConcurrentBag<FileStream>();
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

        public static FileBlockReader Open(string filePath, long length)
        {
            var result = new FileBlockReader(filePath, length);

            result.ReleaseFile(new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
            return result;
        }

        private int closed = 0;

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

        private const int WaitForFile = 50;

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
                    red = file.Read(buffer, offset, count);
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
                } while (file.Position < expectedLength && count > 0);
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

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

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
        #endregion
    }
}