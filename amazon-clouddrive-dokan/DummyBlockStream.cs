namespace Azi.Cloud.DokanNet
{
    using System.Threading.Tasks;

    public class DummyBlockStream : AbstractBlockStream
    {
        public override void Flush()
        {
        }

        public override Task<int> Read(long position, byte[] buffer, int offset, int count, int timeout = 1000)
        {
            return Task.FromResult(0);
        }

        public override void SetLength(long len)
        {
        }

        public override Task Write(long position, byte[] buffer, int offset, int count, int timeout = 1000)
        {
            return Task.FromResult(0);
        }

        protected override void Dispose(bool disposing)
        {
        }
    }
}