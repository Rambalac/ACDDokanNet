namespace Azi.Cloud.AmazonCloudDrive
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Threading;
    using System.Threading.Tasks;
    using Amazon.CloudDrive;
    using Amazon.CloudDrive.JsonObjects;
    using Common;
    using Newtonsoft.Json;
    using Tools;

    public sealed class AmazonCloud : IHttpCloud, IHttpCloudFiles, IHttpCloudNodes
    {
        private static readonly AmazonNodeKind[] FsItemKinds = { AmazonNodeKind.FILE, AmazonNodeKind.FOLDER };

        private readonly AmazonDrive amazon;

        // ReSharper disable once PrivateFieldCanBeConvertedToLocalVariable
        private readonly TokenUpdateListener tokenUpdateListener;

        private Quota lastQuota;
        private DateTime lastQuotaTime;

        public AmazonCloud()
        {
            amazon = new AmazonDrive(AmazonSecret.ClientId, AmazonSecret.ClientSecret);
            tokenUpdateListener = new TokenUpdateListener(this);
            amazon.OnTokenUpdate = tokenUpdateListener;
        }

        public static string CloudServiceIcon => "/Clouds.AmazonCloudDrive;Component/images/cd_icon.png";

        public static string CloudServiceName => "Amazon Cloud Drive";

        string IHttpCloud.CloudServiceIcon => CloudServiceIcon;

        string IHttpCloud.CloudServiceName => CloudServiceName;

        public IHttpCloudFiles Files => this;

        public string Id { get; set; }

        public IHttpCloudNodes Nodes => this;

        public IAuthUpdateListener OnAuthUpdated { get; set; }

        public async Task<bool> AuthenticateNew(CancellationToken cs)
        {
            var dlg = new MountWaitBox();
            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cs))
            {
                dlg.Cancellation = cts;
                dlg.Show();

                try
                {
                    var result = await amazon.AuthenticationByExternalBrowser(CloudDriveScopes.ReadAll | CloudDriveScopes.Write, TimeSpan.FromMinutes(10), cts.Token);
                    cs.ThrowIfCancellationRequested();
                    return result;
                }
                finally
                {
                    dlg.Close();
                }
            }
        }

        public async Task<bool> AuthenticateSaved(CancellationToken cs, string save)
        {
            var authinfo = JsonConvert.DeserializeObject<AuthInfo>(save);

            return await amazon.AuthenticationByTokens(
            authinfo.AuthToken,
            authinfo.AuthRenewToken,
            authinfo.AuthTokenExpiration);
        }

        public Task<string> CalculateLocalStreamContentId(Stream stream)
        {
            using (var md5 = MD5.Create())
            {
                var data = md5.ComputeHash(stream);
                return Task.FromResult(string.Concat(data.Select(b => b.ToString("x2"))));
            }
        }

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

        public void Dispose()
        {
            amazon?.Dispose();
        }

        async Task<Stream> IHttpCloudFiles.Download(string id)
        {
            try
            {
                return await amazon.Files.Download(id);
            }
            catch (Exception ex)
            {
                throw ProcessException(ex);
            }
        }

        public async Task<long> GetAvailableFreeSpace()
        {
            try
            {
                return (await GetQuota()).available;
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

                return node?.status == AmazonNodeStatus.AVAILABLE ? FromNode(node) : null;
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
                return nodes.Where(n => FsItemKinds.Contains(n.kind) && n.status == AmazonNodeStatus.AVAILABLE).Select(FromNode).ToList();
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

                return node?.status == AmazonNodeStatus.AVAILABLE ? FromNode(node) : null;
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

        public async Task<long> GetTotalFreeSpace()
        {
            try
            {
                return (await GetQuota()).available;
            }
            catch (Exception ex)
            {
                throw ProcessException(ex);
            }
        }

        public async Task<long> GetTotalSize()
        {
            try
            {
                return (await GetQuota()).quota;
            }
            catch (Exception ex)
            {
                throw ProcessException(ex);
            }
        }

        public async Task<long> GetTotalUsedSpace()
        {
            try
            {
                return (await amazon.Account.GetUsage()).total.total.bytes;
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

        async Task<FSItem.Builder> IHttpCloudFiles.Overwrite(string id, Func<FileStream> p, Progress progress)
        {
            try
            {
                return FromNode(await amazon.Files.Overwrite(id, p, null, async uploaded =>
                {
                    if (progress != null)
                    {
                        await progress.Invoke(uploaded);
                    }

                    return uploaded + (1 << 15);
                }));
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

        Task<string> IHttpCloudNodes.ShareNode(string id, NodeShareType type)
        {
            throw new NotSupportedException();
        }

        public async Task SignOut(string save)
        {
            await Task.FromResult(0);
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

        async Task<FSItem.Builder> IHttpCloudFiles.UploadNew(string parentId, string fileName, Func<FileStream> p, Progress progress)
        {
            try
            {
                var upload = new FileUpload
                {
                    StreamOpener = p,
                    FileName = fileName,
                    ParentId = parentId,
                    ProgressAsync = async uploaded =>
                    {
                        if (progress != null)
                        {
                            await progress.Invoke(uploaded);
                        }

                        return uploaded + (1 << 15);
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
                ContentId = node.contentProperties?.md5,
                ParentIds = new ConcurrentBag<string>(node.parents),
                Name = node.name
            };
        }

        private async Task<Quota> GetQuota()
        {
            if (lastQuota == null || DateTime.UtcNow - lastQuotaTime > TimeSpan.FromMinutes(1))
            {
                lastQuota = await amazon.Account.GetQuota();
                lastQuotaTime = DateTime.UtcNow;
            }

            return lastQuota;
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

            public void OnTokenUpdated(string accessToken, string refreshToken, DateTime expiresIn)
            {
                var authinfo = new AuthInfo
                {
                    AuthToken = accessToken,
                    AuthRenewToken = refreshToken,
                    AuthTokenExpiration = expiresIn
                };
                var str = JsonConvert.SerializeObject(authinfo);
                ac.OnAuthUpdated?.OnAuthUpdated(ac, str);
            }
        }
    }
}