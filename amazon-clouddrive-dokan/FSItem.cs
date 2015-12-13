using System;

namespace amazon_clouddrive_dokan
{
    public class FSItem
    {
        public string Path;
        public bool IsDir;

        public string Name
        {
            get
            {
                return System.IO.Path.GetFileName(Path);
            }
        }

        public DateTime LastAccessTime { get; internal set; }
        public DateTime LastWriteTime { get; internal set; }
        public DateTime CreationTime { get; internal set; }
    }
}