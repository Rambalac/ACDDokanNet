using Azi.Amazon.CloudDrive.JsonObjects;
using System;
using System.Collections.Concurrent;

namespace Azi.ACDDokanNet
{
    public class FSItem
    {
        public readonly DateTime FetchTime = DateTime.UtcNow;

        public bool IsFake { get; internal set; } = false;

        // To replacement to cache files in path that does not exist
        public bool NotExistingDummy { get; private set; } = false;
        public string Path { get; internal set; }

        public ConcurrentBag<string> ParentIds { get; private set; }
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
            ParentIds = new ConcurrentBag<string>(item.ParentIds);
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
                IsDir = node.kind == AmazonNodeKind.FOLDER,
                CreationTime = node.createdDate,
                LastAccessTime = node.modifiedDate,
                LastWriteTime = node.modifiedDate,
                ParentIds = new ConcurrentBag<string>(node.parents)
            };
        }

        public static FSItem MakeNotExistingDummy(string path)
        {
            return new FSItem
            {
                Path = path,
                NotExistingDummy = true
            };
        }

        public static FSItem FromFake(string path, string cachedName, string parentId)
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
                LastWriteTime = now,
                ParentIds = new ConcurrentBag<string>(new string[] { parentId })
            };
        }

        internal static FSItem FromRoot(AmazonNode amazonNode)
        {
            return FromNode("\\", amazonNode);
        }
    }
}