namespace Azi.Cloud.DokanNet
{
    using System.IO;
    using System.Threading;
    using Azi.Cloud.Common;
    using Azi.Tools;

    public class NewFileBlockWriter : AbstractBlockStream
    {
        private readonly FSItem item;
        private readonly FileStream writer;
        private object fileLock = new object();
        private int closed;
        private long lastPosition;
        private bool disposedValue; // To detect redundant calls

        public NewFileBlockWriter(FSItem item, string filePath)
        {
            this.item = item;

            writer = new FileStream(filePath, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);
        }

        public override void Close()
        {
            if (Interlocked.CompareExchange(ref closed, 1, 0) == 1)
            {
                return;
            }

            lock (fileLock)
            {
                item.Length = writer.Length;
                writer.Close();
            }

            Log.Trace($"Closed New file: {item.Path} of {item.Length} bytes");
            base.Close();
        }

        public override int Read(long position, byte[] buffer, int offset, int count, int timeout = 1000)
        {
            lock (fileLock)
            {
                writer.Position = position;
                return writer.Read(buffer, offset, count);
            }
        }

        public override void Write(long position, byte[] buffer, int offset, int count, int timeout = 1000)
        {
            lock (fileLock)
            {
                // if (lastPosition != position) Log.Warn($"Write Position in New file was changed from {lastPosition} to {position}");
                writer.Position = position;
                writer.Write(buffer, offset, count);
                lastPosition = writer.Position;
            }

            item.Length = writer.Length;

            // Log.Trace("Write byte: " + count);
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
                item.Length = len;
            }
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