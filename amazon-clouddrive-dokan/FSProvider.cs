using Azi.Amazon.CloudDrive;
using Azi.Amazon.CloudDrive.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace amazon_clouddrive_dokan
{
    public class FSProvider
    {
        AmazonDrive amazon;

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
            throw new NotImplementedException();
        }

        public void CreateDir(string fileName)
        {
            throw new NotImplementedException();
        }

        public Stream OpenFile(string fileName, FileMode mode, FileAccess fileAccess, FileShare share, FileOptions options)
        {
            throw new NotImplementedException();
        }

        public void CreateFile(string fileName)
        {
            throw new NotImplementedException();
        }

        public void DeleteDir(string fileName)
        {
            throw new NotImplementedException();
        }

        Dictionary<string, AmazonChild> pathToNode = new Dictionary<string, AmazonChild>();

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
                result.Add(new FSItem(path, IsDir(node)));
            }

            return result;
        }

        bool IsDir(AmazonChild node) => node.kind == folderKind;

        public FSItem GetItem(string itemPath)
        {
            if (itemPath == "\\")
                return new FSItem(itemPath, true);
            var node = FetchNode(itemPath).Result;

            return (node != null) ? new FSItem(itemPath, IsDir(node)) : null;
        }

        private async Task<AmazonChild> FetchNode(string itemPath)
        {
            if (itemPath == "\\" || itemPath == "") return null;
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
            } while (pathToNode.TryGetValue(curpath, out node));
            foreach (var name in folders)
            {
                var newnode = await amazon.Nodes.GetChild(node?.id, name);
                curpath = curpath + "\\" + name;
                pathToNode[curpath] = newnode;
                node = newnode;
            }
            return node;
        }

        public void MoveFile(string oldName, string newName, bool replace)
        {
            throw new NotImplementedException();
        }
    }
}
