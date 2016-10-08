namespace Azi.Cloud.DokanNet
{
    using System.IO;
    using System.Threading;
    using Azi.Cloud.Common;
    using Azi.Tools;

    public class NewFileBlockWriter : AbstractBlockStream
    {
        private readonly FSItem item;
        private readonly ThreadLocal<FileStream> writer;
        private int closed;
        private long lastPosition;
        private bool disposedValue; // To detect redundant calls
        private string filePath;

        public NewFileBlockWriter(FSItem item, string filePath)
        {
            this.item = item;
            this.filePath = filePath;
            writer = new ThreadLocal<FileStream>(() => new FileStream(filePath, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite), true);
        }

        public override void Close()
        {
            if (Interlocked.CompareExchange(ref closed, 1, 0) == 1)
            {
                return;
            }

            foreach (var stream in writer.Values)
            {
                stream.Close();
            }

            var info = new FileInfo(filePath);
            item.Length = info.Length;

            Log.Trace($"Closed New file: {item.Path} of {item.Length} bytes");
            base.Close();
        }

        public override int Read(long position, byte[] buffer, int offset, int count, int timeout = 1000)
        {
            var stream = writer.Value;
            lock (stream)
            {
                stream.Position = position;
                return stream.Read(buffer, offset, count);
            }
        }

        public override void Write(long position, byte[] buffer, int offset, int count, int timeout = 1000)
        {
            var stream = writer.Value;
            lock (stream)
            {
                // if (lastPosition != position) Log.Warn($"Write Position in New file was changed from {lastPosition} to {position}");
                stream.Position = position;
                stream.Write(buffer, offset, count);
                lastPosition = stream.Position;

                item.RiseLength(lastPosition);
            }

            // Log.Trace("Write byte: " + count);
        }

        public override void Flush()
        {
            foreach (var stream in writer.Values)
            {
                lock (stream)
                {
                    stream.Flush();
                }
            }
        }

        public override void SetLength(long len)
        {
            writer.Value.SetLength(len);
            item.Length = len;
        }

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
    }
}