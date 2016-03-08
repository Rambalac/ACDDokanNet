using Azi.Amazon.CloudDrive;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azi.Amazon.CloudDrive.JsonObjects;
using System.Collections.Concurrent;
using System.Net;
using ShellExtension;

namespace Azi.ACDDokanNet
{
    public sealed class AmazonCloud : IHttpCloud, IHttpCloudFiles, IHttpCloudNodes
    {
        private static readonly AmazonNodeKind[] FsItemKinds = { AmazonNodeKind.FILE, AmazonNodeKind.FOLDER };

        private readonly AmazonDrive amazon;

        public AmazonCloud(AmazonDrive amazon)
        {
            this.amazon = amazon;
        }

        public long AvailableFreeSpace => amazon.Account.GetQuota().Result.available;

        public IHttpCloudFiles Files => this;

        public IHttpCloudNodes Nodes => this;

        public long TotalFreeSpace => amazon.Account.GetQuota().Result.available;

        public long TotalSize => amazon.Account.GetQuota().Result.quota;

        public long TotalUsedSpace => amazon.Account.GetUsage().Result.total.total.bytes;

        async Task<FSItem.Builder> IHttpCloudNodes.CreateFolder(string parentid, string name)
        {
            var node = await amazon.Nodes.CreateFolder(parentid, name);
            return FromNode(node);
        }

        async Task<int> IHttpCloudFiles.Download(string id, byte[] result, int offset, long pos, int left)
        {
            return await amazon.Files.Download(id, result, offset, pos, left);
        }

        async Task<IList<FSItem.Builder>> IHttpCloudNodes.GetChildren(string id)
        {
            var nodes = await amazon.Nodes.GetChildren(id);

            return nodes.Where(n => FsItemKinds.Contains(n.kind) && n.status == AmazonNodeStatus.AVAILABLE).Select(n => FromNode(n)).ToList();
        }

        async Task<FSItem.Builder> IHttpCloudNodes.Move(string itemId, string oldParentId, string newParentId)
        {
            return FromNode(await amazon.Nodes.Move(itemId, oldParentId, newParentId));
        }

        async Task<FSItem.Builder> IHttpCloudNodes.Rename(string id, string newName)
        {
            return FromNode(await amazon.Nodes.Rename(id, newName));
        }

        async Task<FSItem.Builder> IHttpCloudNodes.GetRoot()
        {
            return FromNode(await amazon.Nodes.GetRoot());
        }

        async Task IHttpCloudNodes.Trash(string id)
        {
            await amazon.Nodes.Trash(id);
        }

        async Task IHttpCloudNodes.Remove(string parentId, string itemId)
        {
            await amazon.Nodes.Remove(parentId, itemId);
        }

        async Task<FSItem.Builder> IHttpCloudNodes.GetChild(string id, string name)
        {
            var node = await amazon.Nodes.GetChild(id, name);
            return (node.status == AmazonNodeStatus.AVAILABLE) ? FromNode(node) : null;
        }

        async Task IHttpCloudFiles.Download(string id, Func<HttpWebResponse, Task> streammer, long? fileOffset, int? length)
        {
            await amazon.Files.Download(id, streammer, fileOffset, length);
        }

        async Task<FSItem.Builder> IHttpCloudFiles.UploadNew(string parentId, string fileName, Func<FileStream> p)
        {
            return FromNode(await amazon.Files.UploadNew(parentId, fileName, p));
        }

        async Task<FSItem.Builder> IHttpCloudFiles.Overwrite(string id, Func<FileStream> p)
        {
            return FromNode(await amazon.Files.Overwrite(id, p));
        }

        async Task<object> IHttpCloudNodes.GetNodeExtended(string id)
        {
            var node = await amazon.Nodes.GetNodeExtended(id);
            var info = new ACDDokanNetItemInfo
            {
                Id = id,
                TempLink = node.tempLink,
                Assets = node.assets?.Select(i => new ACDDokanNetAssetInfo { Id = i.id, TempLink = i.tempLink }).ToList()
            };

            if (node.video != null)
            {
                info.Video = new ACDDokanNetAssetInfoImage { Width = node.video.width, Height = node.video.height };
            }

            if (node.image != null)
            {
                info.Image = new ACDDokanNetAssetInfoImage { Width = node.image.width, Height = node.image.height };
            }

            return info;
        }

        /// <summary>
        /// Construct FSItem using information from AmazonNode
        /// </summary>
        /// <param name="node">Amazon Node info</param>
        /// <returns>New constructed item</returns>
        private static FSItem.Builder FromNode(AmazonNode node)
        {
            return new FSItem.Builder
            {
                Length = node.contentProperties?.size ?? 0,
                Id = node.id,
                IsDir = node.kind == AmazonNodeKind.FOLDER,
                CreationTime = node.createdDate,
                LastAccessTime = node.modifiedDate,
                LastWriteTime = node.modifiedDate,
                ParentIds = new ConcurrentBag<string>(node.parents),
                Name = node.name
            };
        }
    }
}
