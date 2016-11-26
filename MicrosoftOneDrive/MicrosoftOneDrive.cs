namespace Azi.Cloud.MicrosoftOneDrive
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Common;
    using Microsoft.OneDrive.Sdk;
    using Microsoft.OneDrive.Sdk.Authentication;
    using Newtonsoft.Json;

    public class MicrosoftOneDrive : IHttpCloud, IHttpCloudFiles, IHttpCloudNodes
    {
        private static readonly string[] Scopes = { "onedrive.readwrite", "wl.offline_access", "wl.signin" };

        private Quota lastQuota;
        private DateTime lastQuotaTime;
        private MsaAuthenticationProvider msaAuthenticationProvider;
        private IOneDriveClient oneDriveClient;
        private Item rootItem;

        public static string CloudServiceIcon => "/Clouds.MicrosoftOneDrive;Component/images/cd_icon.png";

        public static string CloudServiceName => "Microsoft OneDrive";

        public IHttpCloudFiles Files => this;

        public string Id { get; set; }

        string IHttpCloud.CloudServiceIcon => CloudServiceIcon;

        string IHttpCloud.CloudServiceName => CloudServiceName;

        public IHttpCloudNodes Nodes => this;

        public IAuthUpdateListener OnAuthUpdated { get; set; }

        public async Task<bool> AuthenticateNew(CancellationToken cs)
        {
            msaAuthenticationProvider = new MsaAuthenticationProvider(MicrosoftSecret.ClientId, MicrosoftSecret.ClientSecret, "http://localhost:45674/authredirect", Scopes, null, new CredentialsVault(this, null));
            await msaAuthenticationProvider.AuthenticateUserAsync().ConfigureAwait(false);

            oneDriveClient = new OneDriveClient("https://api.onedrive.com/v1.0", msaAuthenticationProvider);

            return msaAuthenticationProvider.IsAuthenticated;
        }

        public async Task<bool> AuthenticateSaved(CancellationToken cs, string save)
        {
            msaAuthenticationProvider = new MsaAuthenticationProvider(MicrosoftSecret.ClientId, MicrosoftSecret.ClientSecret, "http://localhost:45674/authredirect", Scopes, null, new CredentialsVault(this, save));
            await msaAuthenticationProvider.RestoreMostRecentFromCacheOrAuthenticateUserAsync().ConfigureAwait(false);

            oneDriveClient = new OneDriveClient("https://api.onedrive.com/v1.0", msaAuthenticationProvider);

            return msaAuthenticationProvider.IsAuthenticated;
        }

        public async Task<FSItem.Builder> CreateFolder(string parentid, string name)
        {
            var item = await GetItem(parentid).Children.Request().AddAsync(new Item { Name = name, Folder = new Folder() }).ConfigureAwait(false);
            return FromNode(item);
        }

        public async Task<Stream> Download(string id)
        {
            return await GetItem(id).Content.Request().GetAsync().ConfigureAwait(false);
        }

        public async Task<long> GetAvailableFreeSpace()
        {
            try
            {
                return (await GetQuota()).Remaining ?? 0;
            }
            catch (Exception ex)
            {
                throw ProcessException(ex);
            }
        }

        public async Task<FSItem.Builder> GetChild(string id, string name)
        {
            var items = await GetAllChildren(id);
            var item = items.SingleOrDefault(i => i.Name == name);
            if (item == null)
            {
                return null;
            }

            return FromNode(item);
        }

        public async Task<IList<FSItem.Builder>> GetChildren(string id)
        {
            var nodes = await GetAllChildren(id);
            return nodes.Select(n => FromNode(n)).ToList();
        }

        public async Task<FSItem.Builder> GetNode(string id)
        {
            var item = await GetItem(id).Request().GetAsync().ConfigureAwait(false);
            return FromNode(item);
        }

        public async Task<INodeExtendedInfo> GetNodeExtended(string id)
        {
            var item = await GetItem(id).Request().GetAsync().ConfigureAwait(false);
            var info = new CloudDokanNetItemInfo
            {
                WebLink = item.WebUrl,
                CanShareReadOnly = true,
                CanShareReadWrite = true,
                Id = item.Id
            };

            return info;
        }

        public async Task<FSItem.Builder> GetRoot()
        {
            return FromNode(await GetRootItem());
        }

        public async Task<long> GetTotalFreeSpace()
        {
            try
            {
                return (await GetQuota()).Remaining ?? 0;
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
                return (await GetQuota()).Total ?? 0;
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
                return (await GetQuota()).Used ?? 0;
            }
            catch (Exception ex)
            {
                throw ProcessException(ex);
            }
        }

        public async Task<FSItem.Builder> Move(string itemId, string oldParentId, string newParentId)
        {
            var newitem = await GetItem(itemId).Request().UpdateAsync(new Item { ParentReference = new ItemReference { Id = newParentId } });
            return FromNode(newitem);
        }

        public async Task<FSItem.Builder> Overwrite(string id, Func<FileStream> streammer, Progress progress)
        {
            using (var stream = streammer())
            {
                var newitem = await GetItem(id).Content.Request().PutAsync<Item>(stream);
                return FromNode(newitem);
            }
        }

        public Task Remove(string itemId, string parentId)
        {
            throw new NotSupportedException();
        }

        public async Task<FSItem.Builder> Rename(string id, string newName)
        {
            var newitem = await GetItem(id).Request().UpdateAsync(new Item { Name = newName });
            return FromNode(newitem);
        }

        public async Task<string> ShareNode(string id, NodeShareType type)
        {
            string t;
            switch (type)
            {
                case NodeShareType.ReadWrite:
                    t = "edit";
                    break;

                default:
                    t = "view";
                    break;
            }

            var perm = await GetItem(id).CreateLink(t).Request().PostAsync();
            return perm.Link.WebUrl;
        }

        public async Task SignOut(string save)
        {
            if (oneDriveClient != null)
            {
                await msaAuthenticationProvider.SignOutAsync();
                msaAuthenticationProvider = null;
                oneDriveClient = null;
            }
        }

        public async Task Trash(string id)
        {
            await GetItem(id).Request().DeleteAsync();
        }

        public async Task<FSItem.Builder> UploadNew(string parentId, string fileName, Func<FileStream> streammer, Progress progress)
        {
            using (var stream = streammer())
            {
                var newitem = await GetItem(parentId).ItemWithPath(fileName).Content.Request().PutAsync<Item>(stream);
                return FromNode(newitem);
            }
        }

        private static FSItem.Builder FromNode(Item node)
        {
            return new FSItem.Builder
            {
                Length = node.Size ?? 0,
                Id = node.Id,
                IsDir = node.Folder != null,
                ParentIds = (node.ParentReference != null) ? new ConcurrentBag<string>(new[] { node.ParentReference.Id }) : new ConcurrentBag<string>(),
                CreationTime = node.CreatedDateTime?.LocalDateTime ?? DateTime.UtcNow,
                LastAccessTime = node.LastModifiedDateTime?.LocalDateTime ?? DateTime.UtcNow,
                LastWriteTime = node.LastModifiedDateTime?.LocalDateTime ?? DateTime.UtcNow,
                Name = node.Name
            };
        }

        private async Task<IList<Item>> GetAllChildren(string id)
        {
            var result = new List<Item>();
            var request = GetItem(id).Children.Request();

            do
            {
                var nodes = await request.GetAsync().ConfigureAwait(false);
                result.AddRange(nodes.CurrentPage);
                request = nodes.NextPageRequest;
            }
            while (request != null);

            return result;
        }

        private async Task<Drive> GetDrive()
        {
            return await oneDriveClient.Drive.Request().GetAsync();
        }

        private IItemRequestBuilder GetItem(string id)
        {
            return oneDriveClient.Drive.Items[id];
        }

        private async Task<Quota> GetQuota()
        {
            if (lastQuota == null || DateTime.UtcNow - lastQuotaTime > TimeSpan.FromMinutes(1))
            {
                lastQuota = (await GetDrive()).Quota;
                lastQuotaTime = DateTime.UtcNow;
            }

            return lastQuota;
        }

        private async Task<Item> GetRootItem()
        {
            if (rootItem == null)
            {
                rootItem = await oneDriveClient
                             .Drive
                             .Root
                             .Request()
                             .GetAsync().ConfigureAwait(false);
            }

            return rootItem;
        }

        private Exception ProcessException(Exception ex)
        {
            if (ex is AggregateException)
            {
                return ex.InnerException;
            }

            return ex;
        }

        private class CredentialsVault : ICredentialVault
        {
            private readonly MicrosoftOneDrive od;
            private byte[] data;

            public CredentialsVault(MicrosoftOneDrive od, string data)
            {
                this.od = od;
                if (data != null)
                {
                    this.data = JsonConvert.DeserializeObject<byte[]>(data);
                }
            }

            public void AddCredentialCacheToVault(CredentialCache credentialCache)
            {
                data = credentialCache.GetCacheBlob();
                var str = JsonConvert.SerializeObject(data);

                od.OnAuthUpdated?.OnAuthUpdated(od, str);
            }

            public bool DeleteStoredCredentialCache()
            {
                return true;
            }

            public bool RetrieveCredentialCache(CredentialCache credentialCache)
            {
                if (data == null)
                {
                    return false;
                }

                credentialCache.InitializeCacheFromBlob(data);
                return true;
            }
        }
    }
}