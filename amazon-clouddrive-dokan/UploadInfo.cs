namespace Azi.Cloud.DokanNet
{
    using System;
    using System.Collections.Concurrent;
    using System.Linq;
    using System.Threading;
    using Common;
    using Newtonsoft.Json;

    public class UploadInfo : IDisposable
    {
        private bool disposedValue = false;

        public UploadInfo()
        {
        }

        public UploadInfo(FSItem item)
        {
            Id = item.Id;
            Path = item.Path;
            ParentId = item.ParentIds.First();
            Length = item.Length;
        }

        [JsonIgnore]
        public CancellationTokenSource Cancellation { get; } = new CancellationTokenSource();

        [JsonIgnore]
        public string FailReason { get; set; }

        public string Id { get; set; }

        public long Length { get; set; }

        public bool Overwrite { get; set; } = false;

        public string ParentId { get; set; }

        public string Path { get; set; }

        public void Dispose()
        {
            Dispose(true);
        }

        internal FSItem ToFSItem()
        {
            return new FSItem.Builder()
            {
                Id = Id,
                Name = System.IO.Path.GetFileName(Path),
                Length = Length,
                ParentIds = new ConcurrentBag<string>(new string[] { ParentId }),
                ParentPath = System.IO.Path.GetDirectoryName(Path)
            }.Build();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    Cancellation.Dispose();
                }

                disposedValue = true;
            }
        }
    }
}