using System;

namespace Azi.Cloud.DokanNet
{
    internal class ByteArrayBlockReader : AbstractBlockStream
    {
        private byte[] data;

        public ByteArrayBlockReader(byte[] data)
        {
            this.data = data;
        }

        public override void Flush()
        {
            // Nothing
        }

        public override int Read(long position, byte[] buffer, int offset, int count, int timeout = 1000)
        {
            var realCount = (int)(data.Length - position);
            if (realCount > count)
            {
                realCount = count;
            }

            Array.Copy(data, position, buffer, offset, realCount);
            return realCount;
        }

        public override void SetLength(long len)
        {
            throw new NotSupportedException();
        }

        public override void Write(long position, byte[] buffer, int offset, int count, int timeout = 1000)
        {
            throw new NotSupportedException();
        }

        protected override void Dispose(bool disposing)
        {
            // Nothing
        }
    }
}