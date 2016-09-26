namespace Azi.Cloud.DokanNet
{
    using System;
    using System.Linq;
    using Common;

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

        internal FSItem ToFSItem()
        {
            throw new NotImplementedException();
        }
    }
}