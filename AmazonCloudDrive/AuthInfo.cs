using System;

namespace Azi.Cloud.DokanNet.AmazonCloudDrive
{
    internal class AuthInfo
    {
        public string AuthRenewToken { get; internal set; }

        public string AuthToken { get; internal set; }

        public DateTime AuthTokenExpiration { get; internal set; }
    }
}