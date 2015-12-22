using Azi.Amazon.CloudDrive;
using Azi.Amazon.CloudDrive.JsonObjects;
using Azi.Tools;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tools;

namespace Azi.ACDDokanNet
{

    public class NodeTreeCache
    {
        class DirItem
        {
            private static readonly FuncEqualityComparer<FSItem> dirItemComparer = new FuncEqualityComparer<FSItem>((a) => a.Name);
            public readonly DateTime ExpirationTime;
            public readonly HashSet<FSItem> Items;
            public DirItem(IList<FSItem> items, int expirationSeconds)
            {
                Items = new HashSet<FSItem>(items, dirItemComparer);
                ExpirationTime = DateTime.UtcNow.AddSeconds(expirationSeconds);
            }

            public bool IsExpired => DateTime.UtcNow > ExpirationTime;
        }

        private readonly ReaderWriterLockSlim lok = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
        private readonly Dictionary<string, FSItem> pathToNode = new Dictionary<string, FSItem>();
        private readonly Dictionary<string, DirItem> pathToDirItem = new Dictionary<string, DirItem>();
        public int DirItemsExpirationSeconds = 60;
        public int FSItemsExpirationSeconds = 5 * 60;


        public void DeleteDir(string filePath)
        {
            lok.EnterWriteLock();
            try
            {
                var dirPath = Path.GetDirectoryName(filePath);
                DirItem dirItem;
                if (pathToDirItem.TryGetValue(dirPath, out dirItem))
                {
                    dirItem.Items.RemoveWhere(i => i.Path == filePath);
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

        public FSItem GetNode(string filePath)
        {
            lok.EnterUpgradeableReadLock();
            FSItem item;
            try
            {
                if (!pathToNode.TryGetValue(filePath, out item)) return null;
                if (item.IsExpired(FSItemsExpirationSeconds))
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
        public IEnumerable<FSItem> GetDir(string filePath)
        {
            DirItem item;
            lok.EnterUpgradeableReadLock();
            try
            {
                if (!pathToDirItem.TryGetValue(filePath, out item)) return null;
                if (!item.IsExpired) return item.Items.ToList();
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

        public void Add(FSItem item)
        {
            lok.EnterWriteLock();
            try
            {
                pathToNode[item.Path] = item;
                DirItem dirItem;
                if (pathToDirItem.TryGetValue(item.Dir, out dirItem)) dirItem.Items.Add(item);
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
                pathToDirItem[folderPath] = new DirItem(items, DirItemsExpirationSeconds);
                foreach (var item in items)
                    pathToNode[item.Path] = item;
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

                DirItem dir;
                if (pathToDirItem.TryGetValue(newNode.Dir, out dir))
                {
                    dir.Items.Add(newNode);
                }
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

                DirItem dir;
                if (pathToDirItem.TryGetValue(newNode.Dir, out dir))
                {
                    dir.Items.Add(newNode);
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
                DirItem dirItem;
                if (pathToDirItem.TryGetValue(dirPath, out dirItem))
                {
                    dirItem.Items.RemoveWhere(i => i.Path == filePath);
                }
                pathToNode.Remove(filePath);
            }
            finally
            {
                lok.ExitWriteLock();
            }
        }
    }
}