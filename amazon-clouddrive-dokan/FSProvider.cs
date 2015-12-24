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
    public class FSProvider
    {
        static readonly string[] fsItemKinds = { "FILE", "FOLDER" };

        readonly AmazonDrive amazon;
        readonly NodeTreeCache nodeTreeCache = new NodeTreeCache();
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
        public static IList<char> GetFreeDriveLettes()
        {
            return Enumerable.Range('C', 'Z' - 'C' + 1).Select(c => (char)c).Except(Environment.GetLogicalDrives().Select(s => s[0])).ToList();
        }

        public void DeleteFile(string filePath)
        {
            var node = GetItem(filePath);
            if (node != null)
            {
                if (node.IsDir) throw new InvalidOperationException("Not file");
                amazon.Nodes.Delete(node.Id).Wait();
                nodeTreeCache.DeleteFile(filePath);
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

            nodeTreeCache.Add(FSItem.FromNode(filePath, node));
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
                node = uploader.Node;
                nodeTreeCache.Add(node);

                uploader.OnUpload = (parent, newnode) =>
                  {
                      File.Move(Path.Combine(SmallFileCache.CachePath, node.Id), Path.Combine(SmallFileCache.CachePath, newnode.id));
                      node.Id = newnode.id;
                      node.Length = newnode.Length;
                      node.NotFake();
                  };
                uploader.OnUploadFailed = (parent, path, id) =>
                  {
                      nodeTreeCache.DeleteFile(path);
                  };

                return uploader;
            }

            return null;
        }

        public void DeleteDir(string filePath)
        {
            var node = GetItem(filePath);
            if (node != null)
            {
                if (!node.IsDir) throw new InvalidOperationException("Not dir");
                amazon.Nodes.Delete(node.Id).Wait();
                nodeTreeCache.DeleteDir(filePath);
            }
        }

        public async Task<IList<FSItem>> GetDirItems(string folderPath)
        {
            var cached = nodeTreeCache.GetDir(folderPath);
            if (cached != null) return cached.ToList();

            var folderNode = GetItem(folderPath);
            var nodes = await amazon.Nodes.GetChildren(folderNode?.Id);
            var items = new List<FSItem>(nodes.Count);
            var curdir = folderPath;
            if (curdir == "\\") curdir = "";
            foreach (var node in nodes.Where(n => fsItemKinds.Contains(n.kind)))
            {
                var path = curdir + "\\" + node.name;
                items.Add(FSItem.FromNode(path, node));
            }

            nodeTreeCache.AddDirItems(folderPath, items);
            return items;
        }

        public FSItem GetItem(string itemPath)
        {
            return FetchNode(itemPath).Result;
        }

        private async Task<FSItem> FetchNode(string itemPath)
        {
            if (itemPath == "\\" || itemPath == "") return FSItem.FromRoot(await amazon.Nodes.GetRoot());
            var cached = nodeTreeCache.GetNode(itemPath);
            if (cached != null) return cached;

            var folders = new LinkedList<string>();
            var curpath = itemPath;
            FSItem item = null;
            do
            {
                folders.AddFirst(Path.GetFileName(curpath));
                curpath = Path.GetDirectoryName(curpath);
                if (curpath == "\\" || string.IsNullOrEmpty(curpath)) break;
                item = nodeTreeCache.GetNode(curpath);
            } while (item == null);
            if (item == null) item = FSItem.FromRoot(await amazon.Nodes.GetRoot());
            foreach (var name in folders)
            {
                var newnode = await amazon.Nodes.GetChild(item.Id, name);
                if (newnode == null) return null;

                if (curpath == "\\") curpath = "";
                curpath = curpath + "\\" + name;
                item = FSItem.FromNode(curpath, newnode);
                nodeTreeCache.Add(item);
            }
            return item;
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
                node = FSItem.FromNode(Path.Combine(oldDir, newName), amazon.Nodes.Rename(node.Id, newName).Result);
                if (node == null) throw new InvalidOperationException("Can not rename");
            }

            if (oldDir != newDir)
            {
                var oldDirNodeTask = FetchNode(oldDir);
                var newDirNodeTask = FetchNode(newDir);
                Task.WaitAll(oldDirNodeTask, newDirNodeTask);
                node = FSItem.FromNode(newPath, amazon.Nodes.Move(node.Id, oldDirNodeTask.Result.Id, newDirNodeTask.Result.Id).Result);
                if (node == null) throw new InvalidOperationException("Can not move");
            }

            if (node.IsDir)
                nodeTreeCache.MoveDir(oldPath, node);
            else
                nodeTreeCache.MoveFile(oldPath, node);
        }
    }
}
