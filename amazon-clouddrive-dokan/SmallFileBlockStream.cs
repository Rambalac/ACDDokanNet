namespace Azi.Cloud.DokanNet
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Common;
    using Tools;

    public class SmallFileBlockStream : AbstractBlockStream
    {
        private const int Maxtimeout = 5;
        private const int WaitForFile = 50;
        private readonly Downloader downloader;
        private readonly string filePath;
        private readonly FileStream stream;
        private bool closed;
        private bool disposedValue; // To detect redundant calls
        private SemaphoreSlim readStreamSync = new SemaphoreSlim(1, 1);

        private bool writeable;
        private bool written;
        private FSItem item;

        private SmallFileBlockStream(FSItem item, string path, Downloader downloader, bool writeable)
        {
            this.item = item;
            filePath = path;
            this.downloader = downloader;
            this.writeable = writeable;
            stream = new FileStream(filePath, FileMode.Open, writeable ? FileAccess.ReadWrite : FileAccess.Read, FileShare.ReadWrite, 4096, true);
            if (downloader == null && stream.Length != item.Length)
            {
                Log.Error($"FileBlockReader without downloder, but expected Length: {item.Length} when stream Length: {stream.Length}");
            }

            if (downloader != null && downloader.Task == null)
            {
                throw new Exception("Downloader without Task");
            }
        }

        public Func<FSItem, string, Task> OnChangedAndClosed { get; set; }

        public static SmallFileBlockStream OpenReadonly(FSItem item, string filePath, Downloader downloader)
        {
            if (downloader == null)
            {
                Log.Warn("No downloader");
            }

            return new SmallFileBlockStream(item, filePath, downloader, false);
        }

        public static SmallFileBlockStream OpenWriteable(FSItem item, string filePath, Downloader downloader)
        {
            if (downloader == null)
            {
                Log.Warn("No downloader");
            }

            return new SmallFileBlockStream(item, filePath, downloader, true);
        }

        public override void Close()
        {
            if (closed)
            {
                return;
            }

            closed = true;

            Log.Trace(Path.GetFileName(filePath));

            item.Length = stream.Length;
            stream.Close();
            stream.Dispose();

            Log.Trace($"Closed ReadWrite file: {item.Path} of {item.Length} bytes");
            base.Close();
            if (written)
            {
                OnChangedAndClosed(item, downloader.Path);
            }
        }

        public override void Flush()
        {
            if (writeable)
            {
                stream.Flush();
            }
        }

        public override async Task<int> Read(long position, byte[] buffer, int offset, int count, int timeout)
        {
            if (count == 0 || item.Length == 0)
            {
                return 0;
            }

            var timeouttime = DateTime.UtcNow.AddMilliseconds(timeout);
            int red;

            using (var timeoutcancel = new CancellationTokenSource(timeout))
            {
                if (downloader != null)
                {
                    try
                    {
                        if (position >= item.Length)
                        {
                            Log.Error($"Expected length: {item.Length} Position to read: {position}");
                            return 0;
                        }

                        await downloader.WaitToPosition(position, timeoutcancel.Token);
                    }
                    catch (TaskCanceledException)
                    {
                        throw new TimeoutException($"File is too big to be downloaded in time for Read: {item.Name} Downloaded: {downloader.Downloaded} Pos: {position} Downloader completed: {downloader.Task.IsCompleted}");
                    }
                }

                await readStreamSync.WaitAsync();

                try
                {
                    stream.Position = position;
                    if (downloader == null)
                    {
                        red = await stream.ReadAsync(buffer, offset, (int)Math.Min(count, item.Length - position));
                    }
                    else
                    {
                        red = await stream.ReadAsync(buffer, offset, (int)Math.Min(count, downloader.Downloaded - position));
                    }
                }
                finally
                {
                    readStreamSync.Release();
                }

                if (red == 0)
                {
                    throw new Exception($"Red 0 File: {filePath} Len: {item.Length} Pos: {position}");
                }

                return red;
            }
        }

        public override void SetLength(long len)
        {
            if (!writeable)
            {
                throw new NotSupportedException();
            }

            readStreamSync.Wait();
            try
            {
                stream.SetLength(len);
                item.Length = len;
                written = true;
            }
            finally
            {
                readStreamSync.Release();
            }
        }

        public override async Task Write(long position, byte[] buffer, int offset, int count, int timeout = 1000)
        {
            if (!writeable)
            {
                throw new NotSupportedException();
            }

            using (var cancel = new CancellationTokenSource(timeout))
            {
                try
                {
                    await downloader.WaitToPosition(Math.Min(item.Length - 1, position + count), cancel.Token);
                }
                catch (TaskCanceledException)
                {
                    throw new TimeoutException();
                }
            }

            await readStreamSync.WaitAsync();
            try
            {
                stream.Position = position;
                await stream.WriteAsync(buffer, offset, count);
            }
            finally
            {
                readStreamSync.Release();
            }

            written = true;
            item.Length = stream.Length;
        }

        protected override void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    Close();
                    stream.Dispose();
                }

                disposedValue = true;
            }
        }
    }
}