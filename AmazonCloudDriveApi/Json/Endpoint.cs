using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmazonCloudDriveApi.Json
{
    public class Endpoint
    {
        public readonly DateTime lastCalculated = DateTime.UtcNow;
        public bool customerExists { get; set; }
        public string contentUrl { get; set; }
        public string metadataUrl { get; set; }
    }

    public class Quota
    {
        public long quota { get; set; }
        public DateTime lastCalculated { get; set; }
        public long available { get; set; }
    }

    public class Usage
    {
        public class TotalBillable
        {
            public class Amount
            {
                public int bytes { get; set; }
                public int count { get; set; }
            }

            public Amount total { get; set; }
            public Amount billable { get; set; }
        }

        public DateTime lastCalculated { get; set; }
        public TotalBillable other { get; set; }
        public TotalBillable doc { get; set; }
        public TotalBillable photo { get; set; }
        public TotalBillable video { get; set; }
    }
}
