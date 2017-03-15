namespace Azi.Cloud.DokanNet
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Common;
    using Tools;

    public class ItemsTreeCache : IDisposable
    {
        private readonly ReaderWriterLockSlim lok = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
        private readonly Dictionary<string, DirItem> pathToDirItem = new Dictionary<string, DirItem>();
        private readonly Dictionary<string, FSItem> pathToNode = new Dictionary<string, FSItem>();
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        private bool disposedValue; // To detect redundant calls

        public ItemsTreeCache()
        {
            Task.Factory.StartNew(async () => await Cleaner(), TaskCreationOptions.LongRunning);
        }

        public int DirItemsExpirationSeconds { get; set; } = 60;

        public int FSItemsExpirationSeconds { get; set; } = 5 * 60;

        public void Add(FSItem item)
        {
            lok.EnterWriteLock();
            try
            {
                pathToNode[item.Path] = item;
                if (pathToDirItem.TryGetValue(item.Dir, out DirItem dirItem))
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

        public void DeleteDir(string filePath)
        {
            lok.EnterWriteLock();
            try
            {
                var dirPath = Path.GetDirectoryName(filePath);
                if (dirPath == null)
                {
                    throw new InvalidOperationException($"dirPath is null for '{filePath}'");
                }

                if (pathToDirItem.TryGetValue(dirPath, out DirItem dirItem))
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

        public void DeleteFile(string filePath)
        {
            lok.EnterWriteLock();
            try
            {
                var dirPath = Path.GetDirectoryName(filePath);
                if (dirPath == null)
                {
                    throw new InvalidOperationException($"dirPath is null for '{filePath}'");
                }

                if (pathToDirItem.TryGetValue(dirPath, out DirItem dirItem))
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

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            Dispose(true);
        }

        public IEnumerable<string> GetDir(string filePath)
        {
            lok.EnterUpgradeableReadLock();
            try
            {
                if (!pathToDirItem.TryGetValue(filePath, out DirItem item))
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

        public FSItem GetItem(string filePath)
        {
            lok.EnterUpgradeableReadLock();
            try
            {
                if (!pathToNode.TryGetValue(filePath, out FSItem item))
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

        protected virtual void Dispose(bool disposing)
        {
            if (disposedValue)
            {
                return;
            }

            if (disposing)
            {
                lok.Dispose();
                cancellation.Dispose();
            }

            disposedValue = true;
        }

        private async Task Cleaner()
        {
            var token = cancellation.Token;
            while (!token.IsCancellationRequested && !disposedValue)
            {
                try
                {
                    lok.EnterWriteLock();
                    try
                    {
                        foreach (var key in pathToNode.Where(p => p.Value.IsExpired(FSItemsExpirationSeconds)).Select(p => p.Key).ToList())
                        {
                            pathToNode.Remove(key);
                        }
                    }
                    finally
                    {
                        lok.ExitWriteLock();
                    }

                    lok.EnterWriteLock();
                    try
                    {
                        foreach (var key in pathToDirItem.Where(p => p.Value.IsExpired).Select(p => p.Key).ToList())
                        {
                            pathToNode.Remove(key);
                        }
                    }
                    finally
                    {
                        lok.ExitWriteLock();
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex);
                }

                await Task.Delay(FSItemsExpirationSeconds * 6, token);
            }
        }

        private class DirItem
        {
            public DirItem(IEnumerable<string> items, int expirationSeconds)
            {
                Items = new HashSet<string>(items);
                ExpirationTime = DateTime.UtcNow.AddSeconds(expirationSeconds);
            }

            public bool IsExpired => DateTime.UtcNow > ExpirationTime;

            public HashSet<string> Items { get; }

            private DateTime ExpirationTime { get; }
        }
    }
}