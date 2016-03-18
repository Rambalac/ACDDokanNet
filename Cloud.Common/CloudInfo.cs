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
    public class CloudInfo
    {
        public string ClassName { get; set; }

        public char DriveLetter { get; set; }

        public bool AutoMount { get; set; }

        public string AuthSave { get; set; }

        public string Name { get; set; }

        public string Id { get; set; }

        public bool ReadOnly { get; set; }
    }
}
