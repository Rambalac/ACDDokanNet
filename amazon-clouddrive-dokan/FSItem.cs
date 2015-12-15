using System;

namespace Azi.ACDDokanNet
{
    public class FSItem
    {
        public string Path { get; internal set; }
        public bool IsDir { get; internal set; }
        public long Length { get; internal set; }

        public string Name => System.IO.Path.GetFileName(Path);

        public DateTime LastAccessTime { get; internal set; }
        public DateTime LastWriteTime { get; internal set; }
        public DateTime CreationTime { get; internal set; }
    }
}