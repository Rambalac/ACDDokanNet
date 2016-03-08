using System.Collections.Generic;

namespace ShellExtension
{
    public class ACDDokanNetItemInfo : ACDDokanNetAssetInfo
    {
        public string Type => nameof(ACDDokanNetItemInfo);

        public IList<ACDDokanNetAssetInfo> Assets { get; set; }
    }
}