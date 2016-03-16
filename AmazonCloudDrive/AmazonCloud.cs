using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Net;
using Azi.Cloud.Common;
using System.Threading;
using Newtonsoft.Json;
using Azi.Amazon.CloudDrive.JsonObjects;
using Azi.Amazon.CloudDrive;
using Azi.Tools;

namespace Azi.Cloud.DokanNet.AmazonCloudDrive
{
    public sealed class AmazonCloud : IHttpCloud, IHttpCloudFiles, IHttpCloudNodes
    {
        private static readonly AmazonNodeKind[] FsItemKinds = { AmazonNodeKind.FILE, AmazonNodeKind.FOLDER };

        private AmazonDrive amazon;

        private TokenUpdateListener tokenUpdateListener;

        public AmazonCloud()
        {
            amazon = new AmazonDrive(AmazonSecret.ClientId, AmazonSecret.ClientSecret);
            tokenUpdateListener = new TokenUpdateListener(this);
            amazon.OnTokenUpdate = tokenUpdateListener;
        }

        public IHttpCloudFiles Files => this;

        public IHttpCloudNodes Nodes => this;

        public long AvailableFreeSpace
        {
            get
            {
                try
                {
                    return amazon.Account.GetQuota().Result.available;
                }
                catch (Exception ex)
                {
                    throw ProcessException(ex);
                }
            }
        }

        public long TotalFreeSpace
        {
            get
            {
                try
                {
                    return amazon.Account.GetQuota().Result.available;
                }
                catch (Exception ex)
                {
                    throw ProcessException(ex);
                }
            }
        }

        public long TotalSize
        {
            get
            {
                try
                {
                    return amazon.Account.GetQuota().Result.quota;
                }
                catch (Exception ex)
                {
                    throw ProcessException(ex);
                }
            }
        }

        public long TotalUsedSpace
        {
            get
            {
                try
                {
                    return amazon.Account.GetUsage().Result.total.total.bytes;
                }
                catch (Exception ex)
                {
                    throw ProcessException(ex);
                }
            }
        }

        public IAuthUpdateListener OnAuthUpdated { get; set; }

        async Task<FSItem.Builder> IHttpCloudNodes.CreateFolder(string parentid, string name)
        {
            try
            {
                var node = await amazon.Nodes.CreateFolder(parentid, name);
                return FromNode(node);
            }
            catch (Exception ex)
            {
                throw ProcessException(ex);
            }
        }

        async Task<int> IHttpCloudFiles.Download(string id, byte[] result, int offset, long pos, int left)
        {
            try
            {
                return await amazon.Files.Download(id, result, offset, pos, left);
            }
            catch (Exception ex)
            {
                throw ProcessException(ex);
            }
        }

        async Task<IList<FSItem.Builder>> IHttpCloudNodes.GetChildren(string id)
        {
            try
            {
                var nodes = await amazon.Nodes.GetChildren(id);
                return nodes.Where(n => FsItemKinds.Contains(n.kind) && n.status == AmazonNodeStatus.AVAILABLE).Select(n => FromNode(n)).ToList();
            }
            catch (Exception ex)
            {
                throw ProcessException(ex);
            }
        }

        async Task<FSItem.Builder> IHttpCloudNodes.Move(string itemId, string oldParentId, string newParentId)
        {
            try
            {
                return FromNode(await amazon.Nodes.Move(itemId, oldParentId, newParentId));
            }
            catch (Exception ex)
            {
                throw ProcessException(ex);
            }
        }

        async Task<FSItem.Builder> IHttpCloudNodes.Rename(string id, string newName)
        {
            try
            {
                return FromNode(await amazon.Nodes.Rename(id, newName));
            }
            catch (Exception ex)
            {
                throw ProcessException(ex);
            }
        }

        async Task<FSItem.Builder> IHttpCloudNodes.GetRoot()
        {
            try
            {
                return FromNode(await amazon.Nodes.GetRoot());
            }
            catch (Exception ex)
            {
                throw ProcessException(ex);
            }
        }

        async Task IHttpCloudNodes.Trash(string id)
        {
            try
            {
                await amazon.Nodes.Trash(id);
            }
            catch (Exception ex)
            {
                throw ProcessException(ex);
            }
        }

        async Task IHttpCloudNodes.Remove(string parentId, string itemId)
        {
            try
            {
                await amazon.Nodes.Remove(parentId, itemId);
            }
            catch (Exception ex)
            {
                throw ProcessException(ex);
            }
        }

        async Task<FSItem.Builder> IHttpCloudNodes.GetChild(string id, string name)
        {
            try
            {
                var node = await amazon.Nodes.GetChild(id, name);
                if (node == null)
                {
                    return null;
                }

                return (node.status == AmazonNodeStatus.AVAILABLE) ? FromNode(node) : null;
            }
            catch (Exception ex)
            {
                throw ProcessException(ex);
            }
        }

        async Task IHttpCloudFiles.Download(string id, Func<HttpWebResponse, Task> streammer, long? fileOffset, int? length)
        {
            try
            {
                await amazon.Files.Download(id, streammer, fileOffset, length);
            }
            catch (Exception ex)
            {
                throw ProcessException(ex);
            }
        }

        async Task<FSItem.Builder> IHttpCloudFiles.UploadNew(string parentId, string fileName, Func<FileStream> p)
        {
            try
            {
                return FromNode(await amazon.Files.UploadNew(parentId, fileName, p));
            }
            catch (Exception ex)
            {
                throw ProcessException(ex);
            }
        }

        async Task<FSItem.Builder> IHttpCloudFiles.Overwrite(string id, Func<FileStream> p)
        {
            try
            {
                return FromNode(await amazon.Files.Overwrite(id, p));
            }
            catch (Exception ex)
            {
                throw ProcessException(ex);
            }
        }

        async Task<object> IHttpCloudNodes.GetNodeExtended(string id)
        {
            try
            {
                var node = await amazon.Nodes.GetNodeExtended(id);
                var info = new CloudDokanNetItemInfo
                {
                    Id = id,
                    TempLink = node.tempLink,
                    Assets = node.assets?.Select(i => new CloudDokanNetAssetInfo { Id = i.id, TempLink = i.tempLink }).ToList()
                };

                if (node.video != null)
                {
                    info.Video = new CloudDokanNetAssetInfoImage { Width = node.video.width, Height = node.video.height };
                }

                if (node.image != null)
                {
                    info.Image = new CloudDokanNetAssetInfoImage { Width = node.image.width, Height = node.image.height };
                }

                return info;
            }
            catch (Exception ex)
            {
                throw ProcessException(ex);
            }
        }

        public Task<bool> AuthenticateNew(CancellationToken cs)
        {
            return Task.Run(async () =>
            {
                if (!await amazon.AuthenticationByExternalBrowser(CloudDriveScope.ReadAll | CloudDriveScope.Write, TimeSpan.FromMinutes(10), cs))
                {
                    return false;
                }

                cs.ThrowIfCancellationRequested();
                return true;
            });
        }

        public Task<bool> AuthenticateSaved(CancellationToken cs, string save)
        {
            try
            {
                var authinfo = JsonConvert.DeserializeObject<AuthInfo>(save);

                return Task.Run(async () =>
                {
                    if (!await amazon.AuthenticationByTokens(
                    authinfo.AuthToken,
                    authinfo.AuthRenewToken,
                    authinfo.AuthTokenExpiration))
                    {
                        return false;
                    }

                    cs.ThrowIfCancellationRequested();
                    return true;
                });
            }
            catch (JsonReaderException)
            {
                return Task.FromResult(false);
            }
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

        private Exception ProcessException(Exception ex)
        {
            var webex = (ex as HttpWebException) ?? ex.InnerException as HttpWebException;
            if (webex == null)
            {
                if (ex is AggregateException)
                {
                    return ex.InnerException;
                }

                return ex;
            }

            return new CloudException(webex.StatusCode, ex);
        }

        private class TokenUpdateListener : ITokenUpdateListener
        {
            private readonly AmazonCloud ac;

            public TokenUpdateListener(AmazonCloud ac)
            {
                this.ac = ac;
            }

            public void OnTokenUpdated(string access_token, string refresh_token, DateTime expires_in)
            {
                var authinfo = new AuthInfo
                {
                    AuthToken = access_token,
                    AuthRenewToken = refresh_token,
                    AuthTokenExpiration = expires_in
                };
                var str = JsonConvert.SerializeObject(authinfo);
                ac.OnAuthUpdated?.OnAuthUpdated(ac, str);
            }
        }
    }
}
