using System.Collections.Generic;

namespace Azi.Cloud.Common
{
    public class ACDDokanNetItemInfo : ACDDokanNetAssetInfo
    {
        public const string ACDDokanNetItemInfoStreamName = "ACDDokanNetInfo";


        public string Type => nameof(ACDDokanNetItemInfo);

        public IList<ACDDokanNetAssetInfo> Assets { get; set; }
    }
}