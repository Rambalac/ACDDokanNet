namespace Azi.Cloud.Common
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading;

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
            ContentId = item.ContentId;
            ParentIds = new ConcurrentBag<string>(item.ParentIds);
        }

        private FSItem()
        {
        }

        public DateTime CreationTime { get; set; }

        public string Dir => System.IO.Path.GetDirectoryName(Path);

        public DateTime FetchTime { get; } = DateTime.UtcNow;

        public string Id { get; internal set; }

        public byte[] Info { get; set; }

        public bool IsDir { get; internal set; }

        public bool IsUploading { get; internal set; }

        public DateTime LastAccessTime { get; set; }

        public DateTime LastWriteTime { get; set; }

        public string ContentId { get; internal set; }

        public long Length
        {
            get
            {
                return Interlocked.Read(ref length);
            }

            set
            {
                Interlocked.Exchange(ref length, value);
            }
        }

        public string Name => System.IO.Path.GetFileName(Path);

        // To replacement to cache files in path that does not exist
        public bool NotExistingDummy { get; private set; }

        public ConcurrentBag<string> ParentIds { get; private set; }

        public string Path { get; set; }

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
                ParentIds = new ConcurrentBag<string>(new[] { parentId })
            };
        }

        public bool IsContentIdEqual(FSItem i)
        {
            if (string.IsNullOrWhiteSpace(i?.ContentId) || string.IsNullOrWhiteSpace(ContentId))
            {
                return false;
            }

            return ContentId == i.ContentId;
        }

        public bool IsExpired(int expirationSeconds) => DateTime.UtcNow > FetchTime.AddSeconds(expirationSeconds);

        public void MakeNotUploading()
        {
            IsUploading = false;
        }

        public void MakeUploading()
        {
            IsUploading = true;
        }

        public class Builder
        {
            public DateTime CreationTime { get; set; }

            public string Id { get; set; }

            public bool IsDir { get; set; }

            public DateTime LastAccessTime { get; set; }

            public DateTime LastWriteTime { get; set; }

            public long Length { get; set; }

            public string Name { get; set; }

            public ConcurrentBag<string> ParentIds { get; set; }

            public string ParentPath { get; set; }

            public string ContentId { get; set; }

            public Builder SetParentPath(string path)
            {
                if (path != null)
                {
                    ParentPath = path.StartsWith("\\") ? path : "\\" + path;
                }

                return this;
            }

            public FSItem Build()
            {
                if (ParentPath == null)
                {
                    throw new NullReferenceException("ParentPath should be set");
                }

                var result = new FSItem
                {
                    Id = Id,
                    CreationTime = CreationTime,
                    IsDir = IsDir,
                    LastAccessTime = LastAccessTime,
                    LastWriteTime = LastWriteTime,
                    Length = Length,
                    ContentId = ContentId?.ToLowerInvariant(),
                    ParentIds = new ConcurrentBag<string>(ParentIds),
                    Path = (ParentPath != string.Empty) ? System.IO.Path.Combine(ParentPath, Name) : "\\" + Name
                };

                return result;
            }

            public FSItem BuildRoot()
            {
                ParentPath = "\\";
                Name = string.Empty;
                return Build();
            }
        }
    }
}