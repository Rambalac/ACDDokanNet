using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Azi.Cloud.Common
{
    public class CloudMount
    {
        private IHttpCloud instance;

        public string ClassName { get; set; }

        public char DriveLetter { get; set; }

        public bool AutoMount { get; set; }

        public string AuthSave { get; set; }

        public string Name { get; set; }

        [JsonIgnore]
        public IHttpCloud Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = Activator.CreateInstance(null, ClassName).Unwrap() as IHttpCloud;
                }

                return instance;
            }
        }
    }
}
