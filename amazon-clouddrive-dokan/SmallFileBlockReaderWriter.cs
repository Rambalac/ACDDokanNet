using System;
using System.Threading.Tasks;
using Azi.Amazon.CloudDrive.JsonObjects;
using System.IO;
using Azi.Amazon.CloudDrive;
using Azi.Tools;
using System.Threading;
using System.Security.Cryptography;
using System.Collections.Concurrent;

namespace Azi.ACDDokanNet
{
    public class SmallFileBlockReaderWriter : AbstractBlockStream
    {
        private readonly FileStream writer;
        private readonly ConcurrentBag<FileStream> readers = new ConcurrentBag<FileStream>();

        private object fileLock = new object();
        private object closeLock = new object();
        private Downloader downloader;

        public Action<FSItem, string> OnChangedAndClosed;

        public SmallFileBlockReaderWriter(Downloader downloader)
        {
            this.downloader = downloader;

            writer = new FileStream(downloader.Path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
        }

        int closed = 0;
        public override void Close()
        {
            lock (closeLock)
            {
                if (Interlocked.CompareExchange(ref closed, 1, 0) == 1) return;

                lock (fileLock)
                {
                    downloader.Item.Length = writer.Length;
                    writer.Close();
                }

                foreach (var file in readers)
                {
                    file.Close();
                }

                Log.Trace($"Closed ReadWrite file: {downloader.Item.Path} of {downloader.Item.Length} bytes");
                base.Close();
                if (written) OnChangedAndClosed(downloader.Item, downloader.Path);
            }
        }

        private FileStream GetReader()
        {
            if (closed == 1) throw new IOException("File is alredy closed");

            FileStream result;
            if (readers.TryTake(out result)) return result;
            lock (closeLock)
            {
                if (closed == 1) throw new IOException("File is already closed");
                return new FileStream(downloader.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            }
        }

        private void ReleaseFile(FileStream file)
        {
            lock (closeLock)
            {
                if (closed != 1)
                    readers.Add(file);
                else
                    file.Close();
            }
        }

        const int waitForFile = 50;

        public override int Read(long position, byte[] buffer, int offset, int count, int timeout)
        {
            if (count == 0 || downloader.Item.Length == 0) return 0;
            var timeouttime = DateTime.UtcNow.AddMilliseconds(timeout);
            if (!downloader.WaitToTheEnd(timeout))
            {
                Log.Error("File is too big to be downloaded in time for ReadWrite: " + downloader.Item.Path);
                throw new TimeoutException();
            }
            int red;
            var file = GetReader();
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
                    if (file.Position < downloader.Item.Length && red == 0)
                    {
                        Thread.Sleep(waitForFile);
                    }
                    if (DateTime.UtcNow > timeouttime) throw new TimeoutException();
                } while (file.Position < downloader.Item.Length && count > 0);
                return totalred;
            }
            finally
            {
                ReleaseFile(file);
            }
        }

        long lastPosition = 0;
        bool written = false;
        public override void Write(long position, byte[] buffer, int offset, int count, int timeout = 1000)
        {
            if (position < downloader.Item.Length)
            {
                if (!downloader.WaitToTheEnd(timeout)) throw new TimeoutException();
            }
            lock (fileLock)
            {
                //if (lastPosition != position) Log.Warn($"Write Position in New file was changed from {lastPosition} to {position}");

                writer.Position = position;
                writer.Write(buffer, offset, count);
                written = true;
                lastPosition = writer.Position;
            }
            downloader.Item.Length = writer.Length;
            //Log.Trace("Write bytes: " + count);
        }

        public override void Flush()
        {
            writer.Flush();
        }
        public override void SetLength(long len)
        {
            lock (fileLock)
            {
                writer.SetLength(len);
                downloader.Item.Length = len;
                written = true;
            }
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
                    writer.Dispose();
                }

                disposedValue = true;
            }
        }

        #endregion
    }
}