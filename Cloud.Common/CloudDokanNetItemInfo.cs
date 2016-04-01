namespace Azi.Cloud.Common
{
    using System.Collections.Generic;

    public class CloudDokanNetItemInfo : CloudDokanNetAssetInfo
    {
        public const string StreamName = "CloudDokanNetInfo";

        public IList<CloudDokanNetAssetInfo> Assets { get; set; }

        public string Type => nameof(CloudDokanNetItemInfo);
    }
}