using Azi.Amazon.CloudDrive;
using Azi.Amazon.CloudDrive.JsonObjects;
using Azi.Tools;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;


namespace Azi.ACDDokanNet
{
    class DirItem
    {
        public readonly DateTime FetchTime = DateTime.UtcNow;
        public readonly ConcurrentBag<FSItem> Items;
        public DirItem(IList<FSItem> items)
        {
            Items = new ConcurrentBag<FSItem>(items);
        }
    }
    public class FSProvider
    {
        static readonly string[] fsItemKinds = { "FILE", "FOLDER" };
        const string folderKind = "FOLDER";

        readonly AmazonDrive amazon;
        readonly ConcurrentDictionary<string, FSItem> pathToNode = new ConcurrentDictionary<string, FSItem>();
        readonly ConcurrentDictionary<string, DirItem> pathToDirItems = new ConcurrentDictionary<string, DirItem>();
        readonly TimeSpan DirItemsExpiration = TimeSpan.FromSeconds(60);
        readonly SmallFileCache smallFileCache;

        public FSProvider(AmazonDrive amazon)
        {
            this.amazon = amazon;
            smallFileCache = SmallFileCache.GetInstance(amazon);
        }

        public long AvailableFreeSpace => amazon.Account.GetQuota().Result.available;
        public long TotalSize => amazon.Account.GetQuota().Result.quota;
        public long TotalFreeSpace => amazon.Account.GetQuota().Result.available;

        public string VolumeName => "Cloud Drive";

        public void DeleteFile(string fileName)
        {
            var node = GetItem(fileName);
            var dir = Path.GetDirectoryName(fileName);
            var dirNode = GetItem(dir);
            if (node != null)
            {
                amazon.Nodes.Delete(node.Id).Wait();
                DirItem remove;
                pathToDirItems.TryRemove(dir, out remove);
                FSItem rem;
                pathToNode.TryRemove(fileName, out rem);
            }

        }

        public bool Exists(string fileName)
        {
            return GetItem(fileName) != null;
        }

        public void CreateDir(string fileName)
        {
            var dir = Path.GetDirectoryName(fileName);
            var dirNode = GetItem(dir);

            var name = Path.GetFileName(fileName);
            var node = amazon.Nodes.CreateFolder(dirNode.Id, name).Result;

            DirItem result;
            if (pathToDirItems.TryGetValue(dir, out result))
                result.Items.Add(FromNode(fileName, node));

        }

        public IBlockStream OpenFile(string fileName, FileMode mode, FileAccess fileAccess, FileShare share, FileOptions options)
        {
            if (fileAccess == FileAccess.ReadWrite) return null;
            if (fileAccess == FileAccess.Read)
            {
                var node = GetItem(fileName);
                if (node == null) return null;
                return smallFileCache.OpenRead(node);
            }

            if (mode == FileMode.CreateNew)
            {
                var dir = Path.GetDirectoryName(fileName);
                var name = Path.GetFileName(fileName);
                var dirNode = GetItem(dir);
                var uploader = new NewBlockFileUploader(dirNode, name, amazon);

                var fake = FromFake(fileName, uploader.CachedName);
                pathToNode.TryAdd(fileName, fake);
                DirItem result;

                if (pathToDirItems.TryGetValue(dir, out result)) result.Items.Add(fake);

                uploader.OnUpload = (parent, node) =>
                  {
                      fake.Id = node.id;
                      fake.Length = node.Length;
                      File.Move(Path.Combine(SmallFileCache.CachePath, uploader.CachedName), Path.Combine(SmallFileCache.CachePath, node.id));
                  };

                return uploader;
            }

            return null;
        }

        private FSItem FromFake(string path, string cachedName)
        {
            var now = DateTime.UtcNow;
            return new FSItem
            {
                Length = 0,
                Id = cachedName,
                Path = path,
                IsDir = false,
                CreationTime = now,
                LastAccessTime = now,
                LastWriteTime = now
            };
        }

        public void DeleteDir(string fileName)
        {
            DeleteFile(fileName);
        }

        public async Task<IList<FSItem>> GetDirItems(string folderPath)
        {
            DirItem result;
            var found = pathToDirItems.TryGetValue(folderPath, out result);
            if (found && (DateTime.UtcNow - result.FetchTime) < DirItemsExpiration) return result.Items.ToList();

            var folderNode = GetItem(folderPath);
            var nodes = await amazon.Nodes.GetChildren(folderNode?.Id);
            var items = new List<FSItem>(nodes.Count);
            var curdir = folderPath;
            if (curdir == "\\") curdir = "";
            foreach (var node in nodes.Where(n => fsItemKinds.Contains(n.kind)))
            {
                var path = folderPath + "\\" + node.name;
                pathToNode[path] = FromNode(path, node);
                items.Add(FromNode(path, node));
            }

            pathToDirItems[folderPath] = new DirItem(items);
            return items;
        }

        public FSItem GetItem(string itemPath)
        {
            return FetchNode(itemPath).Result;
        }

        private FSItem FromNode(string itemPath, AmazonChild node)
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

        private async Task<FSItem> FetchNode(string itemPath)
        {
            if (itemPath == "\\" || itemPath == "") return FromNode(itemPath, await amazon.Nodes.GetRoot());
            FSItem result;
            if (pathToNode.TryGetValue(itemPath, out result)) return result;

            var folders = new LinkedList<string>();
            var curpath = itemPath;
            FSItem node = null;
            do
            {
                folders.AddFirst(Path.GetFileName(curpath));
                curpath = Path.GetDirectoryName(curpath);
                if (curpath == "\\" || string.IsNullOrEmpty(curpath)) break;
            } while (!pathToNode.TryGetValue(curpath, out node));
            if (node == null) node = FromNode("\\", await amazon.Nodes.GetRoot());
            foreach (var name in folders)
            {
                var newnode = await amazon.Nodes.GetChild(node.Id, name);
                if (newnode == null) return null;

                curpath = curpath + "\\" + name;
                node = FromNode(curpath, newnode);
                pathToNode[curpath] = node;
            }
            return node;
        }

        public void MoveFile(string oldPath, string newPath, bool replace)
        {
            if (oldPath == newPath) return;

            var oldDir = Path.GetDirectoryName(oldPath);
            var oldName = Path.GetFileName(oldPath);
            var newDir = Path.GetDirectoryName(newPath);
            var newName = Path.GetFileName(newPath);

            var node = GetItem(oldPath);
            if (oldName != newName)
            {
                node = FromNode(newPath, amazon.Nodes.Rename(node.Id, newName).Result);
            }

            if (oldDir != newDir)
            {
                var oldDirNodeTask = FetchNode(oldDir);
                var newDirNodeTask = FetchNode(newDir);
                Task.WaitAll(oldDirNodeTask, newDirNodeTask);
                node = FromNode(newPath, amazon.Nodes.Move(node.Id, oldDirNodeTask.Result.Id, newDirNodeTask.Result.Id).Result);
            }

            FSItem removed;
            DirItem dir;
            if (pathToDirItems.TryGetValue(newDir, out dir)) dir.Items.Add(node);
            pathToDirItems.TryRemove(oldDir, out dir);
            pathToNode.TryRemove(oldPath, out removed);
            node.Path = newPath;
            pathToNode[newPath] = node;
        }
    }
}
