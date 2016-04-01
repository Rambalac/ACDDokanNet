namespace Azi.Cloud.DokanNet
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using Common;

    public class ItemsTreeCache : IDisposable
    {
        private readonly ReaderWriterLockSlim lok = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
        private readonly Dictionary<string, FSItem> pathToNode = new Dictionary<string, FSItem>();
        private readonly Dictionary<string, DirItem> pathToDirItem = new Dictionary<string, DirItem>();
        private bool disposedValue = false; // To detect redundant calls

        public int DirItemsExpirationSeconds { get; set; } = 60;

        public int FSItemsExpirationSeconds { get; set; } = 5 * 60;

        public FSItem GetItem(string filePath)
        {
            lok.EnterUpgradeableReadLock();
            FSItem item;
            try
            {
                if (!pathToNode.TryGetValue(filePath, out item))
                {
                    return null;
                }

                if (!item.IsUploading && item.IsExpired(FSItemsExpirationSeconds))
                {
                    lok.EnterWriteLock();
                    try
                    {
                        pathToNode.Remove(filePath);
                        return null;
                    }
                    finally
                    {
                        lok.ExitWriteLock();
                    }
                }

                return item;
            }
            finally
            {
                lok.ExitUpgradeableReadLock();
            }
        }

        public IEnumerable<string> GetDir(string filePath)
        {
            DirItem item;
            lok.EnterUpgradeableReadLock();
            try
            {
                if (!pathToDirItem.TryGetValue(filePath, out item))
                {
                    return null;
                }

                if (!item.IsExpired)
                {
                    return item.Items;
                }

                lok.EnterWriteLock();
                try
                {
                    pathToDirItem.Remove(filePath);
                    return null;
                }
                finally
                {
                    lok.ExitWriteLock();
                }
            }
            finally
            {
                lok.ExitUpgradeableReadLock();
            }
        }

        public void AddItemOnly(FSItem item)
        {
            lok.EnterWriteLock();
            try
            {
                pathToNode[item.Path] = item;
            }
            finally
            {
                lok.ExitWriteLock();
            }
        }

        public void Add(FSItem item)
        {
            lok.EnterWriteLock();
            try
            {
                pathToNode[item.Path] = item;
                DirItem dirItem;
                if (pathToDirItem.TryGetValue(item.Dir, out dirItem))
                {
                    dirItem.Items.Add(item.Path);
                }
            }
            finally
            {
                lok.ExitWriteLock();
            }
        }

        public void AddDirItems(string folderPath, IList<FSItem> items)
        {
            lok.EnterWriteLock();
            try
            {
                pathToDirItem[folderPath] = new DirItem(items.Select(i => i.Path).ToList(), DirItemsExpirationSeconds);
                foreach (var item in items)
                {
                    pathToNode[item.Path] = item;
                }
            }
            finally
            {
                lok.ExitWriteLock();
            }
        }

        public void MoveFile(string oldPath, FSItem newNode)
        {
            lok.EnterWriteLock();
            try
            {
                DeleteFile(oldPath);
                Add(newNode);
            }
            finally
            {
                lok.ExitWriteLock();
            }
        }

        public void MoveDir(string oldPath, FSItem newNode)
        {
            lok.EnterWriteLock();
            try
            {
                DeleteDir(oldPath);
                Add(newNode);
            }
            finally
            {
                lok.ExitWriteLock();
            }
        }

        public void Update(FSItem newitem)
        {
            lok.EnterWriteLock();
            try
            {
                pathToNode[newitem.Path] = newitem;
            }
            finally
            {
                lok.ExitWriteLock();
            }
        }

        public void DeleteFile(string filePath)
        {
            lok.EnterWriteLock();
            try
            {
                var dirPath = Path.GetDirectoryName(filePath);
                DirItem dirItem;
                if (pathToDirItem.TryGetValue(dirPath, out dirItem))
                {
                    dirItem.Items.Remove(filePath);
                }

                pathToNode.Remove(filePath);
            }
            finally
            {
                // Log.Warn("File deleted: " + filePath);
                lok.ExitWriteLock();
            }
        }

        public void DeleteDir(string filePath)
        {
            lok.EnterWriteLock();
            try
            {
                var dirPath = Path.GetDirectoryName(filePath);
                DirItem dirItem;
                if (pathToDirItem.TryGetValue(dirPath, out dirItem))
                {
                    dirItem.Items.RemoveWhere(i => i == filePath);
                }

                foreach (var key in pathToNode.Keys.Where(v => v.StartsWith(filePath, StringComparison.InvariantCulture)).ToList())
                {
                    pathToNode.Remove(key);
                }

                foreach (var key in pathToDirItem.Keys.Where(v => v.StartsWith(filePath, StringComparison.InvariantCulture)).ToList())
                {
                    pathToDirItem.Remove(key);
                }
            }
            finally
            {
                lok.ExitWriteLock();
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);

            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    lok.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.
                disposedValue = true;
            }
        }

        private class DirItem
        {
            public DirItem(IList<string> items, int expirationSeconds)
            {
                Items = new HashSet<string>(items);
                ExpirationTime = DateTime.UtcNow.AddSeconds(expirationSeconds);
            }

            public DateTime ExpirationTime { get; }

            public HashSet<string> Items { get; }

            public bool IsExpired => DateTime.UtcNow > ExpirationTime;
        }
    }
}