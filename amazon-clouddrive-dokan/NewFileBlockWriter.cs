using System.IO;
using Azi.Tools;
using System.Threading;

namespace Azi.ACDDokanNet
{
    public class NewFileBlockWriter : AbstractBlockStream
    {
        private readonly FSItem Item;
        private readonly FileStream writer;
        private object fileLock = new object();

        public NewFileBlockWriter(FSItem item, string filePath)
        {
            this.Item = item;

            writer = new FileStream(filePath, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);
        }

        private int closed = 0;

        public override void Close()
        {
            if (Interlocked.CompareExchange(ref closed, 1, 0) == 1)
            {
                return;
            }

            lock (fileLock)
            {
                Item.Length = writer.Length;
                writer.Close();
            }

            Log.Trace($"Closed New file: {Item.Path} of {Item.Length} bytes");
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

        private long lastPosition = 0;

        public override void Write(long position, byte[] buffer, int offset, int count, int timeout = 1000)
        {
            lock (fileLock)
            {
                // if (lastPosition != position) Log.Warn($"Write Position in New file was changed from {lastPosition} to {position}");
                writer.Position = position;
                writer.Write(buffer, offset, count);
                lastPosition = writer.Position;
            }

            Item.Length = writer.Length;

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
                Item.Length = len;
            }
        }

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
    }
}