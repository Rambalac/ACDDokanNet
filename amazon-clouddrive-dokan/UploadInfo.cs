namespace Azi.Cloud.DokanNet
{
    using System.Collections.Concurrent;
    using System.Linq;
    using Common;
    using System.Threading;

    public class UploadInfo
    {
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

        public long Length { get; set; }

        public string Id { get; set; }

        public string Path { get; set; }

        public string ParentId { get; set; }

        public bool Overwrite { get; set; } = false;

        public string FailReason { get; set; }

        public CancellationTokenSource Cancellation { get; } = new CancellationTokenSource();

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
    }
}