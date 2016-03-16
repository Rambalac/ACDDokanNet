namespace Azi.Cloud.Common
{
    public class CloudDokanNetAssetInfo : INodeExtendedInfoTempLink, INodeExtendedInfoWebLink
    {
        public string WebLink => "https://www.amazon.com/clouddrive/folder/" + Id;

        public string Id { get; set; }

        public string TempLink { get; set; }

        public CloudDokanNetAssetInfoImage Image { get; set; }

        public CloudDokanNetAssetInfoImage Video { get; set; }
    }
}