using System;

namespace Azi.Amazon.CloudDrive.JsonObjects
{

    public class Quota
    {
        public long quota { get; set; }
        public DateTime lastCalculated { get; set; }
        public long available { get; set; }
    }
}