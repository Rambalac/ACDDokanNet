namespace Azi.Cloud.DokanNet
{
    using System.IO;
    using Common;
    using Tools;

    public class NewFileBlockWriter : AbstractBlockStream
    {
        private readonly FSItem item;
        private readonly FileStream stream;
        private bool disposedValue;
        private string filePath;

        public NewFileBlockWriter(FSItem item, string filePath)
        {
            this.item = item;
            this.filePath = filePath;
            stream = new FileStream(filePath, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);
        }

        public override void Close()
        {
            item.Length = stream.Length;
            stream.Dispose();

            Log.Trace($"Closed New file: {item.Path} of {item.Length} bytes");
            base.Close();
        }

        public override int Read(long position, byte[] buffer, int offset, int count, int timeout = 1000)
        {
            lock (stream)
            {
                stream.Position = position;
                return stream.Read(buffer, offset, count);
            }
        }

        public override void Write(long position, byte[] buffer, int offset, int count, int timeout = 1000)
        {
            lock (stream)
            {
                // if (lastPosition != position) Log.Warn($"Write Position in New file was changed from {lastPosition} to {position}");
                stream.Position = position;
                stream.Write(buffer, offset, count);

                item.Length = stream.Length;
            }

            // Log.Trace("Write byte: " + count);
        }

        public override void Flush()
        {
            stream.Flush();
        }

        public override void SetLength(long len)
        {
            lock (stream)
            {
                stream.SetLength(len);
            }

            item.Length = len;
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