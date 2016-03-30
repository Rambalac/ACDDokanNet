using Azi.Cloud.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Net;
using System.Threading;
using Microsoft.OneDrive.Sdk;
using Microsoft.OneDrive.Sdk.WindowsForms;
using System.Collections.Concurrent;

namespace Azi.Cloud.MicrosoftOneDrive
{
    public class MicrosoftOneDrive : IHttpCloud, IHttpCloudFiles, IHttpCloudNodes
    {
        private static readonly string[] Scopes = { "onedrive.readwrite", "wl.signin" };

        private Item rootItem;

        private IOneDriveClient oneDriveClient;

        public static string CloudServiceName => "Microsoft OneDrive";

        public static string CloudServiceIcon => "/Clouds.MicrosoftOneDrive;Component/images/cd_icon.png";

        public string Id { get; set; }

        public long AvailableFreeSpace => 0;

        string IHttpCloud.CloudServiceIcon => CloudServiceIcon;

        string IHttpCloud.CloudServiceName => CloudServiceName;

        public IHttpCloudFiles Files => this;

        public IHttpCloudNodes Nodes => this;

        public IAuthUpdateListener OnAuthUpdated { get; set; }

        public long TotalFreeSpace => GetDrive().Quota.Remaining ?? 0;

        public long TotalSize => GetDrive().Quota.Total ?? 0;

        public long TotalUsedSpace => GetDrive().Quota.Used ?? 0;

        public async Task<bool> AuthenticateNew(CancellationToken cs)
        {
            if (oneDriveClient == null)
            {
                var form = new FormsWebAuthenticationUi();
                oneDriveClient = await OneDriveClient.GetAuthenticatedMicrosoftAccountClient(MicrosoftSecret.ClientId, "http://localhost:45674/authredirect", Scopes, MicrosoftSecret.ClientSecret, form);
            }

            return oneDriveClient.IsAuthenticated;
        }

        public async Task<bool> AuthenticateSaved(CancellationToken cs, string save)
        {
            if (oneDriveClient == null)
            {
                var form = new FormsWebAuthenticationUi();
                oneDriveClient = await OneDriveClient.GetAuthenticatedMicrosoftAccountClient(MicrosoftSecret.ClientId, "http://localhost:45674/authredirect", Scopes, MicrosoftSecret.ClientSecret, form);
            }

            return oneDriveClient.IsAuthenticated;
        }

        public async Task SignOut(string save)
        {
            await oneDriveClient.SignOutAsync();
        }

        public async Task<FSItem.Builder> CreateFolder(string parentid, string name)
        {
            var item = await GetItem(parentid).Request().CreateAsync(new Item { Name = name, Folder = new Folder() });
            return FromNode(item);
        }

        public async Task Download(string id, Func<Stream, Task<long>> streammer, long? fileOffset = default(long?), int? length = default(int?))
        {
            using (var stream = await GetItem(id).Content.Request().GetAsync())
            {
                if (fileOffset != null)
                {
                    stream.Position = fileOffset.Value;
                    await streammer(stream);
                }
            }
        }

        public async Task<FSItem.Builder> GetNode(string id)
        {
            var item = await GetItem(id).Request().GetAsync();
            return FromNode(item);
        }


        public async Task<int> Download(string id, byte[] result, int offset, long pos, int left)
        {
            using (var stream = await GetItem(id).Content.Request().GetAsync())
            {
                stream.Position = pos;
                return await stream.ReadAsync(result, offset, left);
            }
        }

        public async Task<FSItem.Builder> GetChild(string id, string name)
        {
            var items = await GetAllChildren(id);
            var item = items.Where(i => i.Name == name).SingleOrDefault();
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

        public async Task<object> GetNodeExtended(string id)
        {
            var item = await GetItem(id).Request().GetAsync();
            var info = new CloudDokanNetItemInfo
            {
                WebLink = item.WebUrl,
            };

            return info;
        }

        public async Task<FSItem.Builder> GetRoot()
        {
            return FromNode(await GetRootItem());
        }

        public async Task<FSItem.Builder> Move(string itemId, string oldParentId, string newParentId)
        {
            var item = await GetItem(itemId).Request().GetAsync();
            item.ParentReference = new ItemReference { Id = newParentId };
            var newitem = await GetItem(itemId).Request().UpdateAsync(item);
            return FromNode(newitem);
        }

        public async Task<FSItem.Builder> Overwrite(string id, Func<FileStream> streammer)
        {
            using (var stream = streammer())
            {
                var newitem = await GetItem(id).Content.Request().PutAsync<Item>(stream);
                return FromNode(newitem);
            }
        }

        public async Task<FSItem.Builder> UploadNew(string parentId, string fileName, Func<FileStream> streammer)
        {
            using (var stream = streammer())
            {
                var newitem = await GetItem(parentId).ItemWithPath(fileName).Content.Request().PutAsync<Item>(stream);
                return FromNode(newitem);
            }
        }

        public Task Remove(string itemId, string parentId)
        {
            throw new NotSupportedException();
        }

        public async Task<FSItem.Builder> Rename(string id, string newName)
        {
            var item = await GetItem(id).Request().GetAsync();
            item.Name = newName;
            var newitem = await GetItem(id).Request().UpdateAsync(item);
            return FromNode(newitem);
        }

        public async Task Trash(string id)
        {
            await GetItem(id).Request().DeleteAsync();
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

        private async Task<Item> GetRootItem()
        {
            if (rootItem == null)
            {
                rootItem = await oneDriveClient
                             .Drive
                             .Root
                             .Request()
                             .GetAsync();
            }

            return rootItem;
        }

        private async Task<IList<Item>> GetAllChildren(string id)
        {
            var result = new List<Item>();
            var request = GetItem(id).Children.Request();

            do
            {
                var nodes = await request.GetAsync();
                result.AddRange(nodes.CurrentPage);
                request = nodes.NextPageRequest;
            }
            while (request != null);

            return result;
        }

        private Drive GetDrive()
        {
            return oneDriveClient.Drive.Request().GetAsync().Result;
        }

        private IItemRequestBuilder GetItem(string id)
        {
            return oneDriveClient.Drive.Items[id];
        }
    }
}
