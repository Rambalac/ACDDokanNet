namespace Azi.Cloud.Common
{
    public class CloudDokanNetAssetInfo : INodeExtendedInfoTempLink, INodeExtendedInfoWebLink, INodeExtendedInfo
    {
        public const string StreamNameShareReadOnly = "ShareReadOnly";
        public const string StreamNameShareReadWrite = "ShareReadWrite";

        public bool CanShareReadOnly { get; set; } = false;

        public bool CanShareReadWrite { get; set; } = false;

        public string Id { get; set; }

        public CloudDokanNetAssetInfoImage Image { get; set; }

        public string TempLink { get; set; }

        public CloudDokanNetAssetInfoImage Video { get; set; }

        public string WebLink { get; set; }
    }
}