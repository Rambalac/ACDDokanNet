using AmazonCloudDriveApi;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Azi.Amazon.CloudDrive.Json
{

    internal class Children
    {
        public int count { get; set; }
        public string nextToken { get; set; }
        public IList<AmazonChild> data { get; set; }
    }
}