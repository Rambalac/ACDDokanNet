using System.Collections.Generic;

namespace Azi.Cloud.Common
{
    public class CloudDokanNetItemInfo : CloudDokanNetAssetInfo
    {
        public const string CloudDokanNetItemInfoStreamName = "CloudDokanNetInfo";

        public string Type => nameof(CloudDokanNetItemInfo);

        public IList<CloudDokanNetAssetInfo> Assets { get; set; }
    }
}