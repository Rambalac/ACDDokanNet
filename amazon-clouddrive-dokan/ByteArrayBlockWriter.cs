namespace Azi.Cloud.DokanNet
{
    using System;
    using System.IO;
    using System.Threading.Tasks;

    public class ByteArrayBlockWriter : AbstractBlockStream
    {
        public MemoryStream Content { get; } = new MemoryStream();

        public override void Flush()
        {
        }

        public override Task<int> Read(long position, byte[] buffer, int offset, int count, int timeout = 1000)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long len)
        {
        }

        public override Task Write(long position, byte[] buffer, int offset, int count, int timeout = 1000)
        {
            Content.SetLength(position + count);

            Array.Copy(buffer, offset, Content.GetBuffer(), position, count);
            return Task.FromResult(true);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Content.Dispose();
            }
        }
    }
}