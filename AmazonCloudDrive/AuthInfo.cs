using System;

namespace Azi.Cloud.AmazonCloudDrive
{
    public class AuthInfo
    {
        public string AuthRenewToken { get; set; }

        public string AuthToken { get; set; }

        public DateTime AuthTokenExpiration { get; set; }
    }
}