using System.Collections.Generic;

namespace ShellExtension
{
    public class ACDDokanNetAssetInfoImage
    {
        public int Width { get; set; }
        public int Height { get; set; }
    }
    public class ACDDokanNetAssetInfo
    {
        public string WebLink => "https://www.amazon.com/clouddrive/folder/" + Id;

        public string Id { get; set; }

        public string TempLink { get; set; }

        public ACDDokanNetAssetInfoImage Image { get; set; }
        public ACDDokanNetAssetInfoImage Video { get; set; }
    }

    public class ACDDokanNetItemInfo : ACDDokanNetAssetInfo
    {
        public IList<ACDDokanNetAssetInfo> Assets { get; set; }
    }
}