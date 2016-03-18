using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Azi.Cloud.Common
{
    [SettingsSerializeAs(SettingsSerializeAs.Xml)]
    public class CloudInfoCollection : Dictionary<string, CloudInfo>
    {
    }
}
