namespace Azi.Cloud.DokanNet
{
    using Common;

    public class DownloadStatisticInfo : AStatisticFileInfo
    {
        private readonly FSItem info;

        public DownloadStatisticInfo(FSItem info)
        {
            this.info = info;
        }

        public override long Total => info.Length;

        public override string Id => info.Id;

        public override string FileName => info.Name;

        public override string Path => info.Path;
    }
}