using System;

namespace Azi.Amazon.CloudDrive.Json
{

    public class Image
    {
        public string make { get; set; }
        public string model { get; set; }
        public string exposureTime { get; set; }
        public DateTime dateTimeOriginal { get; set; }
        public string flash { get; set; }
        public string focalLength { get; set; }
        public DateTime dateTime { get; set; }
        public DateTime dateTimeDigitized { get; set; }
        public string software { get; set; }
        public string orientation { get; set; }
        public string colorSpace { get; set; }
        public string meteringMode { get; set; }
        public string exposureProgram { get; set; }
        public string exposureMode { get; set; }
        public string whiteBalance { get; set; }
        public string sensingMethod { get; set; }
        public string xResolution { get; set; }
        public string yResolution { get; set; }
        public string resolutionUnit { get; set; }
        public int width { get; set; }
        public int height { get; set; }
    }
}