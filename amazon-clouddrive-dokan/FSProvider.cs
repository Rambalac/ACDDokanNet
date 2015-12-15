using Azi.Amazon.CloudDrive;
using Azi.Amazon.CloudDrive.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;


namespace Azi.ACDDokanNet
{

    public class FSProvider
    {
        readonly AmazonDrive amazon;

        public FSProvider(AmazonDrive amazon)
        {
            this.amazon = amazon;
        }

        public long AvailableFreeSpace => amazon.Account.GetQuota().Result.available;
        public long TotalSize => amazon.Account.GetQuota().Result.quota;
        public long TotalFreeSpace => amazon.Account.GetQuota().Result.available;

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

        const int fileMemoryBufferSize = 1 << 10;

        public Stream OpenFile(string fileName, FileMode mode, FileAccess fileAccess, FileShare share, FileOptions options)
        {
            if (fileAccess != FileAccess.Read) return null; //TODO

            var node = FetchNode(fileName).Result;
            if (node == null) return null;
            return new BufferedStream(new DiskCachedAmazonFileStream(node, amazon), fileMemoryBufferSize);
        }

        public void CreateFile(string fileName)
        {
            throw new NotImplementedException();
        }

        public void DeleteDir(string fileName)
        {
            throw new NotImplementedException();
        }

        readonly Dictionary<string, AmazonChild> pathToNode = new Dictionary<string, AmazonChild>();

        string[] fsItemKinds = { "FILE", "FOLDER" };
        string folderKind = "FOLDER";
        public async Task<IList<FSItem>> GetDirItems(string folderPath)
        {
            var folderNode = FetchNode(folderPath).Result;
            var nodes = await amazon.Nodes.GetChildren(folderNode?.id);
            var result = new List<FSItem>(nodes.Count);
            if (folderPath == "\\") folderPath = "";
            foreach (var node in nodes.Where(n => fsItemKinds.Contains(n.kind)))
            {
                var path = folderPath + "\\" + node.name;
                pathToNode[path] = node;
                result.Add(FromNode(path, node));
            }

            return result;
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

            pathToNode.Remove(oldPath);
            pathToNode.Add(newPath, newNodeTask.Result);
        }
    }
}
