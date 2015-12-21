using System;
using System.Collections.Generic;

namespace Azi.Amazon.CloudDrive.JsonObjects
{

    public class AmazonNode
    {
        public long Length => contentProperties?.size ?? 0;

        public readonly DateTime FetchTime = DateTime.UtcNow;
        public string eTagResponse { get; set; }
        public string id { get; set; }
        public string name { get; set; }
        public string kind { get; set; }
        public int version { get; set; }
        public DateTime modifiedDate { get; set; }
        public DateTime createdDate { get; set; }
        public IList<string> labels { get; set; }
        public string createdBy { get; set; }
        public IList<string> parents { get; set; }
        public string status { get; set; }
        public bool restricted { get; set; }

        public ContentProperties contentProperties { get; set; }
    }
}