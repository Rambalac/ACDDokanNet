using System.Collections.Generic;

namespace ShellExtension
{
    public class ACDDokanNetItemInfo : ACDDokanNetAssetInfo
    {
        public IList<ACDDokanNetAssetInfo> Assets { get; set; }
    }
}