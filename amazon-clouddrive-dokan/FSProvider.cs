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

        public long TotalUsedSpace => amazon.Account.GetUsage().Result.total.total.bytes;

        public string VolumeName => "Cloud Drive";

        public void DeleteFile(string filePath)
        {
            var node = GetItem(filePath);
            var dir = Path.GetDirectoryName(filePath);
            var dirNode = GetItem(dir);
            if (node != null)
            {
                amazon.Nodes.Delete(node.Id).Wait();
                DirItem remove;
                pathToDirItems.TryRemove(dir, out remove);
                FSItem rem;
                pathToNode.TryRemove(filePath, out rem);
                if (node.IsDir) pathToDirItems.TryRemove(filePath, out remove);
            }

        }

        public bool Exists(string filePath)
        {
            return GetItem(filePath) != null;
        }

        public void CreateDir(string filePath)
        {
            var dir = Path.GetDirectoryName(filePath);
            var dirNode = GetItem(dir);

            var name = Path.GetFileName(filePath);
            var node = amazon.Nodes.CreateFolder(dirNode.Id, name).Result;

            DirItem result;
            if (pathToDirItems.TryGetValue(dir, out result))
                result.Items.Add(FSItem.FromNode(filePath, node));

        }

        public IBlockStream OpenFile(string filePath, FileMode mode, FileAccess fileAccess, FileShare share, FileOptions options)
        {
            if (fileAccess == FileAccess.ReadWrite) return null;
            var node = GetItem(filePath);
            if (fileAccess == FileAccess.Read)
            {
                if (node == null) return null;
                return smallFileCache.OpenRead(node);
            }

            if (mode == FileMode.CreateNew || (mode == FileMode.Create && (node == null || node.Length == 0)))
            {
                var dir = Path.GetDirectoryName(filePath);
                var name = Path.GetFileName(filePath);
                var dirNode = GetItem(dir);
                var uploader = new NewBlockFileUploader(dirNode, node, filePath, amazon);
                if (node == null)
                {
                    node = uploader.Node;

                    pathToNode[filePath] = node;
                    DirItem result;

                    if (pathToDirItems.TryGetValue(dir, out result)) result.Items.Add(node);
                }

                uploader.OnUpload = (parent, newnode) =>
                  {
                      File.Move(Path.Combine(SmallFileCache.CachePath, node.Id), Path.Combine(SmallFileCache.CachePath, newnode.id));
                      node.Id = newnode.id;
                      node.Length = newnode.Length;
                      node.NotFake();
                  };
                uploader.OnUploadFailed = (parent, path, id) =>
                  {
                      FSItem removeitem;
                      DirItem removedir;
                      pathToNode.TryRemove(path, out removeitem);
                      pathToDirItems.TryRemove(parent.Path, out removedir);
                  };

                return uploader;
            }

            return null;
        }

        public void DeleteDir(string filePath)
        {
            DeleteFile(filePath);
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
                pathToNode[path] = FSItem.FromNode(path, node);
                items.Add(FSItem.FromNode(path, node));
            }

            pathToDirItems[folderPath] = new DirItem(items);
            return items;
        }

        public FSItem GetItem(string itemPath)
        {
            return FetchNode(itemPath).Result;
        }

        private async Task<FSItem> FetchNode(string itemPath)
        {
            if (itemPath == "\\" || itemPath == "") return FSItem.FromNode(itemPath, await amazon.Nodes.GetRoot());
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
            if (node == null) node = FSItem.FromNode("\\", await amazon.Nodes.GetRoot());
            foreach (var name in folders)
            {
                var newnode = await amazon.Nodes.GetChild(node.Id, name);
                if (newnode == null) return null;

                if (curpath == "\\") curpath = "";
                curpath = curpath + "\\" + name;
                node = FSItem.FromNode(curpath, newnode);
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
                node = FSItem.FromNode(newPath, amazon.Nodes.Rename(node.Id, newName).Result);
            }

            if (oldDir != newDir)
            {
                var oldDirNodeTask = FetchNode(oldDir);
                var newDirNodeTask = FetchNode(newDir);
                Task.WaitAll(oldDirNodeTask, newDirNodeTask);
                node = FSItem.FromNode(newPath, amazon.Nodes.Move(node.Id, oldDirNodeTask.Result.Id, newDirNodeTask.Result.Id).Result);
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
