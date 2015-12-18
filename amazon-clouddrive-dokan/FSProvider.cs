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
        readonly ConcurrentDictionary<string, AmazonChild> pathToNode = new ConcurrentDictionary<string, AmazonChild>();
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
            var node = FetchNode(fileName).Result;
            var dir = Path.GetDirectoryName(fileName);
            var dirNode = FetchNode(dir).Result;
            if (node != null)
            {
                amazon.Nodes.Delete(node.id).Wait();
                DirItem remove;
                pathToDirItems.TryRemove(dir, out remove);
            }

        }

        public bool Exists(string fileName)
        {
            return FetchNode(fileName).Result != null;
        }

        public void CreateDir(string fileName)
        {
            var dir = Path.GetDirectoryName(fileName);
            var dirNode = FetchNode(dir).Result;

            var name = Path.GetFileName(fileName);
            var node = amazon.Nodes.CreateFolder(dirNode.id, name).Result;

            DirItem result;
            if (pathToDirItems.TryGetValue(dir, out result))
                result.Items.Add(FromNode(fileName, node));

        }

        public IBlockStream OpenFile(string fileName, FileMode mode, FileAccess fileAccess, FileShare share, FileOptions options)
        {
            if (fileAccess == FileAccess.ReadWrite) return null;
            if (fileAccess == FileAccess.Read)
            {
                var node = FetchNode(fileName).Result;
                if (node == null) return null;
                return smallFileCache.OpenRead(node);
            }

            if (mode == FileMode.CreateNew)
            {
                var dir = Path.GetDirectoryName(fileName);
                var name = Path.GetFileName(fileName);
                var dirNode = FetchNode(dir).Result;
                var uploader = new NewBlockFileUploader(dirNode, name, amazon);
                uploader.OnUpload = (parent, node) =>
                  {
                      DirItem result;
                      File.Move(Path.Combine(SmallFileCache.CachePath, uploader.CachedName), Path.Combine(SmallFileCache.CachePath, node.id));
                      if (pathToDirItems.TryGetValue(dir, out result)) result.Items.Add(FromNode(fileName, node));
                  };
                return uploader;
            }

            return null;
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

            var folderNode = FetchNode(folderPath).Result;
            var nodes = await amazon.Nodes.GetChildren(folderNode?.id);
            var items = new List<FSItem>(nodes.Count);
            if (folderPath == "\\") folderPath = "";
            foreach (var node in nodes.Where(n => fsItemKinds.Contains(n.kind)))
            {
                var path = folderPath + "\\" + node.name;
                pathToNode[path] = node;
                items.Add(FromNode(path, node));
            }

            pathToDirItems[folderPath] = new DirItem(items);
            return items;
        }

        public FSItem GetItem(string itemPath)
        {
            if (itemPath == "\\")
                return new FSItem { Path = itemPath, IsDir = true };
            var node = FetchNode(itemPath).Result;

            return (node != null) ? FromNode(itemPath, node) : null;
        }

        private FSItem FromNode(string itemPath, AmazonChild node)
        {
            return new FSItem
            {
                Length = node.contentProperties?.size ?? 0,
                Path = itemPath,
                IsDir = node.kind == folderKind,
                CreationTime = node.createdDate,
                LastAccessTime = node.modifiedDate,
                LastWriteTime = node.modifiedDate
            };
        }

        private async Task<AmazonChild> FetchNode(string itemPath)
        {
            if (itemPath == "\\" || itemPath == "") return await amazon.Nodes.GetRoot();
            AmazonChild result;
            if (pathToNode.TryGetValue(itemPath, out result)) return result;

            var folders = new LinkedList<string>();
            var curpath = itemPath;
            AmazonChild node = null;
            do
            {
                folders.AddFirst(Path.GetFileName(curpath));
                curpath = Path.GetDirectoryName(curpath);
                if (curpath == "\\" || string.IsNullOrEmpty(curpath)) break;
            } while (!pathToNode.TryGetValue(curpath, out node));
            if (node == null) node = await amazon.Nodes.GetRoot();
            foreach (var name in folders)
            {
                var newnode = await amazon.Nodes.GetChild(node.id, name);
                if (newnode == null) return null;

                curpath = curpath + "\\" + name;
                pathToNode[curpath] = newnode;
                node = newnode;
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

            var oldNodeTask = FetchNode(oldPath);
            Task<AmazonChild> newNodeTask = oldNodeTask;
            if (oldName != newName)
            {
                newNodeTask = amazon.Nodes.Rename(oldNodeTask.Result.id, newName);
            }

            if (oldDir != newDir)
            {
                var oldDirNodeTask = FetchNode(oldDir);
                var newDirNodeTask = FetchNode(newDir);
                Task.WaitAll(oldDirNodeTask, newDirNodeTask, oldNodeTask);
                amazon.Nodes.Move(oldNodeTask.Result.id, oldDirNodeTask.Result.id, newDirNodeTask.Result.id).Wait();
            }

            AmazonChild removed;
            DirItem removedDir;
            pathToDirItems.TryRemove(oldDir, out removedDir);
            pathToDirItems.TryRemove(newDir, out removedDir);
            pathToNode.TryRemove(oldPath, out removed);
            pathToNode[newPath] = newNodeTask.Result;
        }
    }
}
