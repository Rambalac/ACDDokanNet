namespace Azi.Cloud.DokanNet
{
    using System;
    using System.Diagnostics.Contracts;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Nito.AsyncEx;

    public class Block : IDisposable
    {
        private readonly IAbsoluteCacheItem item;
        private int currentSize;
        private DateTime? lastUpdate;

        private AsyncAutoResetEvent updateEvent = new AsyncAutoResetEvent(false);
        private object updateSync = new object();

        public Block(IAbsoluteCacheItem item, long n, int expectedSize)
        {
            this.item = item;
            BlockIndex = n;
            Data = new byte[expectedSize];
            lastUpdate = DateTime.UtcNow;
        }

        public long BlockIndex { get; }

        public int CurrentSize => currentSize;

        public byte[] Data { get; }

        public bool IsComplete { get; private set; } = false;

        public DateTime? LastUpdate => lastUpdate;

        internal int RefCounter { get; set; }

        public void Dispose()
        {
            item.ReleaseBlock(this);
        }

        public void MakeComplete()
        {
            IsComplete = true;
            Update();
        }

        public async Task<int> ReadFromStream(Stream stream) => await ReadFromStream(stream, CancellationToken.None).ConfigureAwait(false);

        public async Task<int> ReadFromStream(Stream stream, CancellationToken token)
        {
            Contract.Ensures(!IsComplete);

            var red = await stream.ReadAsync(Data, CurrentSize, Data.Length - CurrentSize, token).ConfigureAwait(false);
            Interlocked.Add(ref currentSize, red);

            Update();

            return red;
        }

        public async Task<DateTime> WaitUpdate(DateTime prevUpdate) => await WaitUpdate(prevUpdate, CancellationToken.None).ConfigureAwait(false);

        public async Task<DateTime> WaitUpdate(DateTime prevUpdate, CancellationToken token)
        {
            do
            {
                lock (updateSync)
                {
                    if (IsComplete || (lastUpdate != null && lastUpdate > prevUpdate))
                    {
                        return lastUpdate.Value;
                    }
                }

                await updateEvent.WaitAsync(token).ConfigureAwait(false);
            }
            while (true);
        }

        private void Update()
        {
            lock (updateSync)
            {
                lastUpdate = DateTime.UtcNow;
                updateEvent.Set();
            }
        }
    }
}