namespace Azi.Cloud.DokanNet
{
    using System;
    using System.Threading;

    public abstract class AbstractBlockStream : IBlockStream
    {
        public Action OnClose { get; set; }

        public abstract void Flush();

        public abstract int Read(long position, byte[] buffer, int offset, int count, int timeout = 1000);

        public abstract void Write(long position, byte[] buffer, int offset, int count, int timeout = 1000);

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
        }

        public virtual void Close()
        {
            OnClose?.Invoke();
        }

        public abstract void SetLength(long len);

        protected abstract void Dispose(bool disposing);
    }
}