namespace Azi.Cloud.Common
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    public delegate void Progress(long progress);

    public interface IHttpCloud
    {
        string Id { get; set; }

        long AvailableFreeSpace { get; }

        string CloudServiceIcon { get; }

        string CloudServiceName { get; }

        IHttpCloudFiles Files { get; }

        IHttpCloudNodes Nodes { get; }

        IAuthUpdateListener OnAuthUpdated { get; set; }

        long TotalFreeSpace { get; }

        long TotalSize { get; }

        long TotalUsedSpace { get; }

        Task<bool> AuthenticateNew(CancellationToken cs);

        Task<bool> AuthenticateSaved(CancellationToken cs, string save);

        Task SignOut(string save);
    }

    public interface IHttpCloudFiles
    {
        Task<Stream> Download(string id);

        Task<FSItem.Builder> Overwrite(string id, Func<FileStream> p, Progress progress);

        Task<FSItem.Builder> UploadNew(string parentId, string fileName, Func<FileStream> p, Progress progress);
    }

    public interface IHttpCloudNodes
    {
        Task<FSItem.Builder> CreateFolder(string parentid, string name);

        Task<FSItem.Builder> GetChild(string id, string name);

        Task<IList<FSItem.Builder>> GetChildren(string id);

        Task<INodeExtendedInfo> GetNodeExtended(string id);

        Task<FSItem.Builder> GetRoot();

        Task<FSItem.Builder> Move(string itemId, string oldParentId, string newParentId);

        Task Remove(string itemId, string parentId);

        Task<FSItem.Builder> Rename(string id, string newName);

        Task Trash(string id);

        Task<FSItem.Builder> GetNode(string id);

        Task<string> ShareNode(string id, NodeShareType type);
    }
}