﻿namespace Azi.Cloud.DokanNet
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Common;
    using Nito.AsyncEx;
    using Tools;

    public class Downloader
    {
        private long downloaded;
        private bool failed;
        private AsyncLock lck = new AsyncLock();
        private List<AsyncAutoResetEvent> monitor = new List<AsyncAutoResetEvent>();
        private Task task;

        public Downloader(FSItem item, string path)
        {
            Item = item;
            Path = path;
        }

        public long Downloaded
        {
            get
            {
                return Interlocked.Read(ref downloaded);
            }

            set
            {
                if (value == 0)
                {
                    throw new Exception($"Downloaded is set to Zero, item length: {Item.Length}");
                }

                using (lck.Lock())
                {
                    Interlocked.Exchange(ref downloaded, value);
                    Pulse();
                }
            }
        }

        public FSItem Item { get; private set; }

        public string Path { get; private set; }

        public Task Task
        {
            get
            {
                return task;
            }

            set
            {
                if (task != null)
                {
                    throw new InvalidOperationException("Cannot reset task");
                }

                if (Downloaded == 0 && value.IsCompleted)
                {
                    throw new Exception("Downloaded Zero but IsCompleted");
                }

                task = value;
            }
        }

        public static Downloader CreateCompleted(FSItem item, string path, long length)
        {
            return new Downloader(item, path)
            {
                Downloaded = length,
                Task = Task.FromResult(true),
            };
        }

        public async Task Failed()
        {
            using (await lck.LockAsync())
            {
                failed = true;
                Pulse();
            }
        }

        public async Task WaitToPosition(long pos, CancellationToken token)
        {
            var ev = new AsyncAutoResetEvent(false);
            using (await lck.LockAsync())
            {
                if (Downloaded > pos)
                {
                    return;
                }

                monitor.Add(ev);
            }

            while (Downloaded <= pos)
            {
                await ev.WaitAsync(token);
                if (failed)
                {
                    throw new Exception("Download failed");
                }
            }

            Log.Trace($"Finished WaitToPosition: {Item.Name} - {Item.Id} Downloaded: {Downloaded} Pos: {pos}");
        }

        public async Task WaitToTheEnd(CancellationToken token)
        {
            await WaitToPosition(Item.Length, token);
        }

        private void Pulse()
        {
            foreach (var ev in monitor)
            {
                ev.Set();
            }
        }
    }
}