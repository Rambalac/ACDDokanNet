using Azi.Amazon.CloudDrive;
using Azi.Amazon.CloudDrive.Json;
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
        public readonly IList<FSItem> Items;
        public DirItem(IList<FSItem> items)
        {
            Items = items;
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
            throw new NotImplementedException();
        }

        public bool Exists(string fileName)
        {
            return FetchNode(fileName) != null;
        }

        public void CreateDir(string fileName)
        {
            throw new NotImplementedException();
        }

        public IBlockReader OpenFile(string fileName, FileMode mode, FileAccess fileAccess, FileShare share, FileOptions options)
        {
            if (fileAccess != FileAccess.Read) return null; //TODO
            var node = FetchNode(fileName).Result;
            if (node == null) return null;
            return smallFileCache.OpenRead(node);
        }

        public void CreateFile(string fileName)
        {
            throw new NotImplementedException();
        }

        public void DeleteDir(string fileName)
        {
            throw new NotImplementedException();
        }

        public async Task<IList<FSItem>> GetDirItems(string folderPath)
        {
            DirItem result;
            var found = pathToDirItems.TryGetValue(folderPath, out result);
            if (found && (DateTime.UtcNow - result.FetchTime) < DirItemsExpiration) return result.Items;

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
            AmazonChild node = await amazon.Nodes.GetRoot();
            do
            {
                folders.AddFirst(Path.GetFileName(curpath));
                curpath = Path.GetDirectoryName(curpath);
                if (curpath == "\\" || string.IsNullOrEmpty(curpath)) break;
            } while (!pathToNode.TryGetValue(curpath, out node));
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
            DirItem dir;
            pathToDirItems.TryRemove(oldDir, out dir);
            pathToDirItems.TryRemove(newDir, out dir);
            pathToNode.TryRemove(oldPath, out removed);
            pathToNode[newPath] = newNodeTask.Result;
        }
    }
}
