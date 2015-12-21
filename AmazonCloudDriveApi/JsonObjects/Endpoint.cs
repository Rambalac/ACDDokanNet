using System;

namespace Azi.Amazon.CloudDrive.JsonObjects
{
    public class Endpoint
    {
        public readonly DateTime lastCalculated = DateTime.UtcNow;
        public bool customerExists { get; set; }
        public string contentUrl { get; set; }
        public string metadataUrl { get; set; }
    }
}
