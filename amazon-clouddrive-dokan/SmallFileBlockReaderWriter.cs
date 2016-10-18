namespace Azi.Cloud.DokanNet
{
    using System;
    using System.Collections.Concurrent;
    using System.IO;
    using System.Threading;
    using Common;
    using Tools;

    public class SmallFileBlockReaderWriter : AbstractBlockStream
    {
        private const int WaitForFile = 50;

        private readonly FileStream file;

        private readonly object closeLock = new object();
        private Downloader downloader;
        private bool written;
        private bool disposedValue; // To detect redundant calls

        public SmallFileBlockReaderWriter(Downloader downloader)
        {
            this.downloader = downloader;

            file = new FileStream(downloader.Path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
        }

        public Action<FSItem, string> OnChangedAndClosed { get; set; }

        public override void Close()
        {
            lock (closeLock)
            {
                downloader.Item.Length = file.Length;
                file.Close();

                Log.Trace($"Closed ReadWrite file: {downloader.Item.Path} of {downloader.Item.Length} bytes");
                base.Close();
                if (written)
                {
                    OnChangedAndClosed(downloader.Item, downloader.Path);
                }
            }
        }

        public override int Read(long position, byte[] buffer, int offset, int count, int timeout)
        {
            if (count == 0 || downloader.Item.Length == 0)
            {
                return 0;
            }

            var timeouttime = DateTime.UtcNow.AddMilliseconds(timeout);
            if (!downloader.WaitToTheEnd(timeout))
            {
                Log.Error("File is too big to be downloaded in time for ReadWrite: " + downloader.Item.Path);
                throw new TimeoutException();
            }

            int red;
            int totalred = 0;
            do
            {
                lock (file)
                {
                    file.Position = position;
                    red = file.Read(buffer, offset, count);
                }

                totalred += red;
                offset += red;
                count -= red;
                position += red;

                if (position < downloader.Item.Length && red == 0)
                {
                    Thread.Sleep(WaitForFile);
                }

                if (DateTime.UtcNow > timeouttime)
                {
                    throw new TimeoutException();
                }
            }
            while (file.Position < downloader.Item.Length && count > 0);
            return totalred;
        }

        public override void Write(long position, byte[] buffer, int offset, int count, int timeout = 1000)
        {
            if (position < downloader.Item.Length)
            {
                if (!downloader.WaitToTheEnd(timeout))
                {
                    throw new TimeoutException();
                }
            }

            lock (file)
            {
                file.Position = position;
                file.Write(buffer, offset, count);
            }

            written = true;
            downloader.Item.Length = file.Length;

            // Log.Trace("Write bytes: " + count);
        }

        public override void Flush()
        {
            file.Flush();
        }

        public override void SetLength(long len)
        {
            lock (file)
            {
                file.SetLength(len);
            }

            downloader.Item.Length = len;
            written = true;
        }

        protected override void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    Close();
                    file.Dispose();
                }

                disposedValue = true;
            }
        }
    }
}