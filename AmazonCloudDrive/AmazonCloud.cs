namespace Azi.Cloud.AmazonCloudDrive
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http.Headers;
    using System.Threading;
    using System.Threading.Tasks;
    using Azi.Amazon.CloudDrive;
    using Azi.Amazon.CloudDrive.JsonObjects;
    using Common;
    using Newtonsoft.Json;
    using Tools;

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

        public static string CloudServiceName => "Amazon Cloud Drive";

        public static string CloudServiceIcon => "/Clouds.AmazonCloudDrive;Component/images/cd_icon.png";

        string IHttpCloud.CloudServiceIcon => CloudServiceIcon;

        string IHttpCloud.CloudServiceName => CloudServiceName;

        public string Id { get; set; }

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

        async Task<int> IHttpCloudFiles.Download(string id, byte[] result, int offset, long pos, int left, Progress progress)
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

        async Task<FSItem.Builder> IHttpCloudNodes.GetNode(string id)
        {
            try
            {
                var node = await amazon.Nodes.GetNode(id);
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

        async Task IHttpCloudFiles.Download(string id, Func<Stream, Task<long>> streammer, Progress progress, long? fileOffset, int? length)
        {
            try
            {
                long expectedOffset = fileOffset ?? 0;
                await amazon.Files.Download(id, fileOffset: fileOffset, length: length, streammer: async (response) =>
                {
                    var partial = response.StatusCode == HttpStatusCode.PartialContent;
                    ContentRangeHeaderValue contentRange = null;
                    if (partial)
                    {
                        contentRange = response.Headers.GetContentRange();
                        if (contentRange.From != expectedOffset)
                        {
                            throw new InvalidOperationException("Content range does not match request");
                        }
                    }

                    using (var stream = response.GetResponseStream())
                    {
                        expectedOffset += await streammer(stream);
                    }
                });
            }
            catch (Exception ex)
            {
                throw ProcessException(ex);
            }
        }

        async Task<FSItem.Builder> IHttpCloudFiles.UploadNew(string parentId, string fileName, Func<FileStream> p, Progress progress)
        {
            try
            {
                var upload = new FileUpload
                {
                    StreamOpener = p,
                    FileName = fileName,
                    ParentId = parentId,
                    Progress = (uploaded) =>
                    {
                        progress?.Invoke(uploaded);
                        return uploaded + (1 << 10);
                    },
                    AllowDuplicate = true
                };

                return FromNode(await amazon.Files.UploadNew(upload));
            }
            catch (Exception ex)
            {
                throw ProcessException(ex);
            }
        }

        async Task<FSItem.Builder> IHttpCloudFiles.Overwrite(string id, Func<FileStream> p, Progress progress)
        {
            try
            {
                return FromNode(await amazon.Files.Overwrite(id, p, null, (uploaded) =>
                {
                    progress?.Invoke(uploaded);
                    return uploaded + (1 << 10);
                }));
            }
            catch (Exception ex)
            {
                throw ProcessException(ex);
            }
        }

        async Task<INodeExtendedInfo> IHttpCloudNodes.GetNodeExtended(string id)
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
                if (node.kind == AmazonNodeKind.FOLDER)
                {
                    info.WebLink = "https://www.amazon.com/clouddrive/folder/" + id;
                }

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

        public async Task<bool> AuthenticateNew(CancellationToken cs)
        {
            var dlg = new MountWaitBox();
            var cts = new CancellationTokenSource();
            dlg.Cancellation = cts;
            dlg.Show();

            var result = await amazon.AuthenticationByExternalBrowser(CloudDriveScopes.ReadAll | CloudDriveScopes.Write, TimeSpan.FromMinutes(10), cts.Token);

            dlg.Close();
            cs.ThrowIfCancellationRequested();
            return result;
        }

        public async Task<bool> AuthenticateSaved(CancellationToken cs, string save)
        {
            var authinfo = JsonConvert.DeserializeObject<AuthInfo>(save);

            return await amazon.AuthenticationByTokens(
            authinfo.AuthToken,
            authinfo.AuthRenewToken,
            authinfo.AuthTokenExpiration);
        }

        Task<string> IHttpCloudNodes.ShareNode(string id, NodeShareType type)
        {
            throw new NotSupportedException();
        }

        public async Task SignOut(string save)
        {
            await Task.FromResult(0);
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