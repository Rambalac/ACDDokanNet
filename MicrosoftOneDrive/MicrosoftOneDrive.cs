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

        private IOneDriveClient oneDriveClient;

        public MicrosoftOneDrive()
        {
            oneDriveClient = OneDriveClient.GetMicrosoftAccountClient(
                MicrosoftSecret.ClientId,
                "http://localhost:45674/authredirect",
                Scopes,
                webAuthenticationUi: new FormsWebAuthenticationUi());
        }

        public static string CloudServiceName => "Microsoft OneDrive";

        public static string CloudServiceIcon => "/Clouds.MicrosoftOneDrive;Component/images/cd_icon.png";

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
            return await Authenticate();
        }

        public async Task<bool> AuthenticateSaved(CancellationToken cs, string save)
        {
            return await Authenticate();
        }

        public async Task SignOut(string save)
        {
            await oneDriveClient.SignOutAsync();
        }

        public async Task<FSItem.Builder> CreateFolder(string parentid, string name)
        {
            var item = await GetItem(parentid).Request().CreateAsync(new Item { Name = name });
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
            var items = await GetItem(id).Children.Request().GetAsync();
            var item = items.Where(i => i.Name == name).SingleOrDefault();
            if (item == null)
            {
                return null;
            }

            return FromNode(item);
        }

        public async Task<IList<FSItem.Builder>> GetChildren(string id)
        {
            var nodes = await GetItem(id).Children.Request().GetAsync();
            return nodes.Select(n => FromNode(n)).ToList();
        }

        public async Task<object> GetNodeExtended(string id)
        {
            throw new NotImplementedException();
        }

        public async Task<FSItem.Builder> GetRoot()
        {
            var rootItem = await oneDriveClient
                             .Drive
                             .Root
                             .Request()
                             .GetAsync();
            return FromNode(rootItem);
        }

        public async Task<FSItem.Builder> Move(string itemId, string oldParentId, string newParentId)
        {
            var item = await GetItem(itemId).Request().GetAsync();
            item.ParentReference = new ItemReference { Id = newParentId };
            var newitem = await GetItem(itemId).Request().UpdateAsync(item);
            return FromNode(newitem);

        }

        public async Task<FSItem.Builder> Overwrite(string id, Func<FileStream> p)
        {
            throw new NotImplementedException();
        }

        public Task Remove(string id1, string id2)
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

        public async Task<FSItem.Builder> UploadNew(string parentId, string fileName, Func<FileStream> p)
        {
            throw new NotImplementedException();
        }

        private static FSItem.Builder FromNode(Item node)
        {
            return new FSItem.Builder
            {
                Length = node.Size ?? 0,
                Id = node.Id,
                IsDir = node.Folder != null,
                CreationTime = node.CreatedDateTime?.LocalDateTime ?? DateTime.UtcNow,
                LastAccessTime = node.LastModifiedDateTime?.LocalDateTime ?? DateTime.UtcNow,
                LastWriteTime = node.LastModifiedDateTime?.LocalDateTime ?? DateTime.UtcNow,
                ParentIds = new ConcurrentBag<string>(new[] { node.ParentReference.Id }),
                Name = node.Name
            };
        }

        private Drive GetDrive()
        {
            return oneDriveClient.Drive.Request().GetAsync().Result;
        }

        private IItemRequestBuilder GetItem(string id)
        {
            return GetItem(id);
        }

        private async Task<bool> Authenticate()
        {
            if (!oneDriveClient.IsAuthenticated)
            {
                await oneDriveClient.AuthenticateAsync();
            }

            return oneDriveClient.IsAuthenticated;
        }
    }
}
