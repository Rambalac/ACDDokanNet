using Azi.Amazon.CloudDrive.JsonObjects;
using System;

namespace Azi.ACDDokanNet
{
    public class FSItem
    {
        public readonly DateTime FetchTime = DateTime.UtcNow;

        const string folderKind = "FOLDER";

        public bool IsFake { get; internal set; } = false;
        public string Path { get; internal set; }
        public string Id { get; internal set; }
        public bool IsDir { get; internal set; }
        public long Length { get; internal set; }

        public string Dir => System.IO.Path.GetDirectoryName(Path);
        public string Name => System.IO.Path.GetFileName(Path);

        public DateTime LastAccessTime { get; internal set; }
        public DateTime LastWriteTime { get; internal set; }
        public DateTime CreationTime { get; internal set; }

        private FSItem()
        {

        }

        public FSItem(FSItem item)
        {
            IsFake = item.IsFake;
            Path = item.Path;
            Id = item.Id;
            IsDir = item.IsDir;
            Length = item.Length;
            LastAccessTime = item.LastAccessTime;
            LastWriteTime = item.LastWriteTime;
            CreationTime = item.CreationTime;
        }

        public void NotFake()
        {
            IsFake = false;
        }

        public bool IsExpired(int expirationSeconds) => DateTime.UtcNow > FetchTime.AddSeconds(expirationSeconds);

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

        internal static FSItem FromRoot(AmazonNode amazonNode)
        {
            return FromNode("\\", amazonNode);
        }
    }
}