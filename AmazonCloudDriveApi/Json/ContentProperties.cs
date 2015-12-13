using System;

namespace Azi.Amazon.CloudDrive.Json
{

    public class ContentProperties
    {
        public int size { get; set; }
        public int version { get; set; }
        public string contentType { get; set; }
        public string md5 { get; set; }
        public string extension { get; set; }
        public DateTime contentDate { get; set; }
        public Image image { get; set; }
    }
}