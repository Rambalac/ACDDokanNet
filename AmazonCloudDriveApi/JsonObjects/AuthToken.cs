using System;

namespace Azi.Amazon.CloudDrive.JsonObjects
{
    public class AuthToken
    {
        public DateTime createdTime = DateTime.UtcNow;
        public bool IsExpired => DateTime.UtcNow > (createdTime + TimeSpan.FromSeconds(expires_in - 60));
        public string token_type { get; set; }
        public int expires_in { get; set; }
        public string refresh_token { get; set; }
        public string access_token { get; set; }
    }
}