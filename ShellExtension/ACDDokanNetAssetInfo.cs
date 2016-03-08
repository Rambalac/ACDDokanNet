using System.Collections.Generic;

namespace ShellExtension
{
    public class ACDDokanNetAssetInfo : INodeExtendedInfoTempLink, INodeExtendedInfoWebLink
    {
        public string WebLink => "https://www.amazon.com/clouddrive/folder/" + Id;

        public string Id { get; set; }

        public string TempLink { get; set; }

        public ACDDokanNetAssetInfoImage Image { get; set; }

        public ACDDokanNetAssetInfoImage Video { get; set; }
    }
}