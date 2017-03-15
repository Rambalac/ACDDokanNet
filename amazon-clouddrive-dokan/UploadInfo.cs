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
        private bool disposedValue;

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

        public string SourcePath { get; set; }

        /// <summary>
        /// Gets or sets original remote file path
        /// </summary>
        public string Path { get; set; }

        [JsonIgnore]
        public string ContentId { get; set; }

        public void Dispose()
        {
            Dispose(true);
        }

        internal FSItem ToFSItem()
        {
            var result = new FSItem.Builder()
            {
                Id = Id,
                Name = System.IO.Path.GetFileName(Path),
                Length = Length,
                ParentIds = new ConcurrentBag<string>(new[] { ParentId }),
                ParentPath = System.IO.Path.GetDirectoryName(Path)
            }.Build();
            result.MakeUploading();
            return result;
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