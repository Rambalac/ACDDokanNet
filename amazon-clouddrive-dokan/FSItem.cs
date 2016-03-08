using System;
using System.Collections.Concurrent;
using System.Threading;
using Azi.Amazon.CloudDrive.JsonObjects;
using System.Collections.Generic;

namespace Azi.ACDDokanNet
{
    public class FSItem
    {
        private long length;

        public FSItem(FSItem item)
        {
            IsUploading = item.IsUploading;
            Path = item.Path;
            Id = item.Id;
            IsDir = item.IsDir;
            Length = item.Length;
            LastAccessTime = item.LastAccessTime;
            LastWriteTime = item.LastWriteTime;
            CreationTime = item.CreationTime;
            ParentIds = new ConcurrentBag<string>(item.ParentIds);
        }

        private FSItem()
        {
        }

        public DateTime FetchTime { get; } = DateTime.UtcNow;

        public bool IsUploading { get; internal set; } = false;

        // To replacement to cache files in path that does not exist
        public bool NotExistingDummy { get; private set; } = false;

        public string Path { get; internal set; }

        public ConcurrentBag<string> ParentIds { get; private set; }

        public string Id { get; internal set; }

        public bool IsDir { get; internal set; }

        public long Length
        {
            get
            {
                return Interlocked.Read(ref length);
            }

            internal set
            {
                Interlocked.Exchange(ref length, value);
            }
        }

        public string Dir => System.IO.Path.GetDirectoryName(Path);

        public string Name => System.IO.Path.GetFileName(Path);

        public DateTime LastAccessTime { get; internal set; }

        public DateTime LastWriteTime { get; internal set; }

        public DateTime CreationTime { get; internal set; }

        public byte[] Info { get; internal set; }

        public static FSItem MakeNotExistingDummy(string path)
        {
            return new FSItem
            {
                Path = path,
                NotExistingDummy = true
            };
        }

        public static FSItem MakeUploading(string path, string cachedId, string parentId, long length)
        {
            var now = DateTime.UtcNow;
            return new FSItem
            {
                IsUploading = true,
                Length = length,
                Id = cachedId,
                Path = path,
                IsDir = false,
                CreationTime = now,
                LastAccessTime = now,
                LastWriteTime = now,
                ParentIds = new ConcurrentBag<string>(new string[] { parentId })
            };
        }

        public void MakeNotUploading()
        {
            IsUploading = false;
        }

        public void MakeUploading()
        {
            IsUploading = true;
        }

        public bool IsExpired(int expirationSeconds) => DateTime.UtcNow > FetchTime.AddSeconds(expirationSeconds);

        public class Builder
        {
            public DateTime CreationTime { get; internal set; }

            public string Id { get; internal set; }

            public bool IsDir { get; internal set; }

            public DateTime LastAccessTime { get; internal set; }

            public DateTime LastWriteTime { get; internal set; }

            public long Length { get; internal set; }

            public string Name { get; set; }

            public ConcurrentBag<string> ParentIds { get; internal set; }

            public string Path { get; internal set; }

            public FSItem Build()
            {
                return new FSItem
                {
                    Id = Id,
                    CreationTime = CreationTime,
                    IsDir = IsDir,
                    LastAccessTime = LastAccessTime,
                    LastWriteTime = LastWriteTime,
                    Length = Length,
                    ParentIds = new ConcurrentBag<string>(ParentIds),
                    Path = Path
                };
            }

            public Builder FilePath(string filePath)
            {
                Path = filePath;
                return this;
            }

            internal FSItem BuildRoot()
            {
                Path = "\\";
                return Build();
            }
        }
    }
}