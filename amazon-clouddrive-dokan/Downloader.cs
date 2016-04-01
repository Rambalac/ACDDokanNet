namespace Azi.Cloud.DokanNet
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Azi.Cloud.Common;

    public class Downloader
    {
        private Task task;
        private long downloaded = 0;

        public Downloader(FSItem item, string path)
        {
            Item = item;
            Path = path;
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

                task = value;
            }
        }

        public long Downloaded
        {
            get
            {
                return Interlocked.Read(ref downloaded);
            }

            set
            {
                Interlocked.Exchange(ref downloaded, value);
            }
        }

        public static Downloader CreateCompleted(FSItem item, string path, long length)
        {
            return new Downloader(item, path)
            {
                Task = Task.FromResult<bool>(true),
                Downloaded = length
            };
        }

        public bool WaitToTheEnd(int timeout)
        {
            return Task.Wait(timeout);
        }
    }
}