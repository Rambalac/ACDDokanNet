namespace Azi.Cloud.Common
{
    using System.Collections.Generic;
    using System.Configuration;

    [SettingsSerializeAs(SettingsSerializeAs.Xml)]
    public class CloudInfoCollection : List<CloudInfo>
    {
        public CloudInfoCollection()
        {
        }

        public CloudInfoCollection(IEnumerable<CloudInfo> clouds)
            : base(clouds)
        {
        }
    }
}