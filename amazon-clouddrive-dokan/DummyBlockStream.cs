using System;

namespace Azi.ACDDokanNet
{
    public class DummyBlockStream : AbstractBlockStream
    {
        public override void Flush()
        {
        }

        public override int Read(long position, byte[] buffer, int offset, int count, int timeout = 1000)
        {
            return 0;
        }

        public override void SetLength(long len)
        {
        }

        public override void Write(long position, byte[] buffer, int offset, int count, int timeout = 1000)
        {
        }

        protected override void Dispose(bool disposing)
        {
        }
    }
}