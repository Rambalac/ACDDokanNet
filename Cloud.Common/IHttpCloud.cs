using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Azi.Cloud.Common
{
    public interface IHttpCloud
    {
        IAuthUpdateListener OnAuthUpdated { get; set; }

        long AvailableFreeSpace { get; }

        long TotalFreeSpace { get; }

        long TotalSize { get; }

        long TotalUsedSpace { get; }

        IHttpCloudFiles Files { get; }

        IHttpCloudNodes Nodes { get; }

        Task<bool> AuthenticateNew(CancellationToken cs);

        Task<bool> AuthenticateSaved(CancellationToken cs, string save);
    }

    public interface IHttpCloudFiles
    {
        Task<int> Download(string id, byte[] result, int offset, long pos, int left);

        Task Download(string id, Func<HttpWebResponse, Task> streammer, long? fileOffset = null, int? length = null);

        Task<FSItem.Builder> UploadNew(string parentId, string fileName, Func<FileStream> p);

        Task<FSItem.Builder> Overwrite(string id, Func<FileStream> p);
    }

    public interface IHttpCloudNodes
    {
        Task<FSItem.Builder> CreateFolder(string parentid, string name);

        Task<IList<FSItem.Builder>> GetChildren(string id);

        Task<FSItem.Builder> Move(string itemId, string oldParentId, string newParentId);

        Task<FSItem.Builder> Rename(string id, string newName);

        Task<FSItem.Builder> GetRoot();

        Task Trash(string id);

        Task Remove(string id1, string id2);

        Task<FSItem.Builder> GetChild(string id, string name);

        Task<object> GetNodeExtended(string id);
    }
}