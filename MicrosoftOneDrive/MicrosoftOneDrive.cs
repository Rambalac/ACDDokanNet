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
        private IOneDriveClient oneDriveClient;

        private static readonly string[] Scopes = { "onedrive.readwrite" };

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

        public MicrosoftOneDrive()
        {

        }

        public async Task<bool> AuthenticateNew(CancellationToken cs)
        {
            return await Authenticate();
        }

        private async Task<bool> Authenticate()
        {
            if (oneDriveClient == null)
            {
                oneDriveClient = OneDriveClient.GetMicrosoftAccountClient(
                MicrosoftSecret.ClientId,
                "http://localhost:45674/authredirect",
                Scopes,
                webAuthenticationUi: new FormsWebAuthenticationUi());
            }

            if (!oneDriveClient.IsAuthenticated)
            {
                await oneDriveClient.AuthenticateAsync();
            }
            return oneDriveClient.IsAuthenticated;
        }

        public async Task<bool> AuthenticateSaved(CancellationToken cs, string save)
        {
            return await Authenticate();
        }

        public async Task<FSItem.Builder> CreateFolder(string parentid, string name)
        {
            throw new NotImplementedException();
        }

        public async Task Download(string id, Func<HttpWebResponse, Task> streammer, long? fileOffset = default(long?), int? length = default(int?))
        {
            throw new NotImplementedException();
        }

        public async Task<int> Download(string id, byte[] result, int offset, long pos, int left)
        {
            throw new NotImplementedException();
        }

        public async Task<FSItem.Builder> GetChild(string id, string name)
        {
            var items = await oneDriveClient.Drive.Items["1234"].Children.Request().GetAsync();
            var item = items.Where(i => i.Name == name).SingleOrDefault();
            if (item == null) return null;
            return FromNode(item);
        }

        public async Task<IList<FSItem.Builder>> GetChildren(string id)
        {
            var nodes = await oneDriveClient.Drive.Items[id].Children.Request().GetAsync();
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
            throw new NotImplementedException();
        }

        public async Task<FSItem.Builder> Overwrite(string id, Func<FileStream> p)
        {
            throw new NotImplementedException();
        }

        public async Task Remove(string id1, string id2)
        {
            throw new NotImplementedException();
        }

        public async Task<FSItem.Builder> Rename(string id, string newName)
        {
            throw new NotImplementedException();
        }

        public async Task Trash(string id)
        {
            throw new NotImplementedException();
        }

        public async Task<FSItem.Builder> UploadNew(string parentId, string fileName, Func<FileStream> p)
        {
            throw new NotImplementedException();
        }

        private Drive GetDrive()
        {
            return oneDriveClient.Drive.Request().GetAsync().Result;
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
    }
}
