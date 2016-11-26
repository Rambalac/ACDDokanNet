namespace Azi.Cloud.DokanNet
{
    using System;
    using System.Threading.Tasks;
    using Tools;

    public abstract class AbstractBlockStream : IBlockStream
    {
        public Func<Task> OnClose { get; set; }

        public abstract void Flush();

        public abstract Task<int> Read(long position, byte[] buffer, int offset, int count, int timeout = 1000);

        public abstract Task Write(long position, byte[] buffer, int offset, int count, int timeout = 1000);

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
        }

        public virtual void Close()
        {
            try
            {
                OnClose?.Invoke();
                Dispose();
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
        }

        public abstract void SetLength(long len);

        protected abstract void Dispose(bool disposing);
    }
}