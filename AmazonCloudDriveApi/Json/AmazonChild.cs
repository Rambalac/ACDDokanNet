using AmazonCloudDriveApi;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Azi.Amazon.CloudDrive.Json
{
    public class Token
    {
        public string token_type { get; set; }
        public int expires_in { get; set; }
        public string refresh_token { get; set; }
        public string access_token { get; set; }
    }

    public class AmazonChild
    {
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