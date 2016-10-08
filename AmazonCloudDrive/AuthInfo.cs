namespace Azi.Cloud.AmazonCloudDrive
{
    using System;

    public class AuthInfo
    {
        public string AuthRenewToken { get; set; }

        public string AuthToken { get; set; }

        public DateTime AuthTokenExpiration { get; set; }
    }
}