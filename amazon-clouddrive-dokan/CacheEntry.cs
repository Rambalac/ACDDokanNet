namespace Azi.Cloud.DokanNet
{
    using System;
    using System.Threading;

    public class CacheEntry : IDisposable
    {
        private readonly ReaderWriterLockSlim lk = new ReaderWriterLockSlim();
        private DateTime accessTime;

        public DateTime AccessTime
        {
            get
            {
                lk.EnterReadLock();
                try
                {
                    return accessTime;
                }
                finally
                {
                    lk.ExitReadLock();
                }
            }

            set
            {
                lk.EnterWriteLock();
                try
                {
                    accessTime = value;
                }
                finally
                {
                    lk.ExitWriteLock();
                }
            }
        }

        public string Id { get; set; }

        public long Length { get; set; }

        public string LinkPath { get; set; }

        public void Dispose()
        {
            lk.Dispose();
        }
    }
}