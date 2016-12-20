namespace Azi.Cloud.DokanNet
{
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Common;
    using Tools;

    public class NewFileBlockWriter : AbstractBlockStream
    {
        private readonly FSItem item;
        private readonly FileStream stream;
        private readonly SemaphoreSlim sem = new SemaphoreSlim(1, 1);
        private bool closed;
        private bool disposedValue;

        public NewFileBlockWriter(FSItem item, string filePath)
        {
            this.item = item;
            UploadCachePath = filePath;
            stream = new FileStream(filePath, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite, 4096, true);
        }

        public bool Cancelled { get; private set; }

        public string UploadCachePath { get; }

        public void CancelUpload()
        {
            Cancelled = true;
        }

        public override void Close()
        {
            if (closed)
            {
                return;
            }

            closed = true;

            item.Length = stream.Length;
            stream.Close();
            stream.Dispose();

            Log.Trace($"Closed New file: {item.Path} of {item.Length} bytes");
            base.Close();
        }

        public override void Flush()
        {
            stream.Flush();
        }

        public override async Task<int> Read(long position, byte[] buffer, int offset, int count, int timeout = 1000)
        {
            await sem.WaitAsync();
            try
            {
                stream.Position = position;
                return await stream.ReadAsync(buffer, offset, count);
            }
            finally
            {
                sem.Release();
            }
        }

        public override void SetLength(long len)
        {
            sem.Wait();
            try
            {
                stream.SetLength(len);
            }
            finally
            {
                sem.Release();
            }

            item.Length = len;
        }

        public override async Task Write(long position, byte[] buffer, int offset, int count, int timeout = 1000)
        {
            await sem.WaitAsync();
            try
            {
                // if (lastPosition != position) Log.Warn($"Write Position in New file was changed from {lastPosition} to {position}");
                stream.Position = position;
                await stream.WriteAsync(buffer, offset, count);

                item.Length = stream.Length;
            }
            finally
            {
                sem.Release();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    Close();
                    stream.Dispose();
                    sem.Dispose();
                }

                disposedValue = true;
            }
        }
    }
}