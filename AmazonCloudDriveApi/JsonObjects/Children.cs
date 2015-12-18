using System.Collections.Generic;

namespace Azi.Amazon.CloudDrive.JsonObjects
{

    internal class Children
    {
        public int count { get; set; }
        public string nextToken { get; set; }
        public IList<AmazonChild> data { get; set; }
    }
}