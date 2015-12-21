using System;

namespace Azi.Amazon.CloudDrive.JsonObjects
{

    public class Usage
    {
        public class TotalAndBillable
        {
            public class Amount
            {
                public long bytes { get; set; }
                public long count { get; set; }
            }

            public Amount total { get; set; }
            public Amount billable { get; set; }
        }

        public TotalAndBillable total
        {
            get
            {
                return new TotalAndBillable
                {
                    total = new TotalAndBillable.Amount
                    {
                        bytes = other.total.bytes + doc.total.bytes + photo.total.bytes + video.total.bytes,
                        count = other.total.count + doc.total.count + photo.total.count + video.total.count
                    },
                    billable = new TotalAndBillable.Amount
                    {
                        bytes = other.billable.bytes + doc.billable.bytes + photo.billable.bytes + video.billable.bytes,
                        count = other.billable.count + doc.billable.count + photo.billable.count + video.billable.count
                    }
                };
            }
        }
        public DateTime lastCalculated { get; set; }
        public TotalAndBillable other { get; set; }
        public TotalAndBillable doc { get; set; }
        public TotalAndBillable photo { get; set; }
        public TotalAndBillable video { get; set; }
    }
}