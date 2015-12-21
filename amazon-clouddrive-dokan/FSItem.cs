using Azi.Amazon.CloudDrive.JsonObjects;
using System;

namespace Azi.ACDDokanNet
{
    public class FSItem
    {
        const string folderKind = "FOLDER";

        public bool IsFake { get; internal set; } = false;
        public string Path { get; internal set; }
        public string Id { get; internal set; }
        public bool IsDir { get; internal set; }
        public long Length { get; internal set; }

        public string Name => System.IO.Path.GetFileName(Path);

        public DateTime LastAccessTime { get; internal set; }
        public DateTime LastWriteTime { get; internal set; }
        public DateTime CreationTime { get; internal set; }

        public void NotFake()
        {
            IsFake = false;
        }

        public static FSItem FromNode(string itemPath, AmazonNode node)
        {
            return new FSItem
            {
                Length = node.contentProperties?.size ?? 0,
                Id = node.id,
                Path = itemPath,
                IsDir = node.kind == folderKind,
                CreationTime = node.createdDate,
                LastAccessTime = node.modifiedDate,
                LastWriteTime = node.modifiedDate
            };
        }

        public static FSItem FromFake(string path, string cachedName)
        {
            var now = DateTime.UtcNow;
            return new FSItem
            {
                IsFake = true,
                Length = 0,
                Id = cachedName,
                Path = path,
                IsDir = false,
                CreationTime = now,
                LastAccessTime = now,
                LastWriteTime = now
            };
        }


    }
}