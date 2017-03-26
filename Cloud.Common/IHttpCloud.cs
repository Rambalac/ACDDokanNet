namespace Azi.Cloud.Common
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    public delegate Task Progress(long progress);

    public interface IHttpCloud : IDisposable
    {
        string CloudServiceIcon { get; }

        string CloudServiceName { get; }

        IHttpCloudFiles Files { get; }

        string Id { get; set; }

        IHttpCloudNodes Nodes { get; }

        IAuthUpdateListener OnAuthUpdated { get; set; }

        Task<bool> AuthenticateNew(CancellationToken cs);

        Task<bool> AuthenticateSaved(CancellationToken cs, string save);

        Task<long> GetAvailableFreeSpace();

        Task<long> GetTotalFreeSpace();

        Task<long> GetTotalSize();

        Task<long> GetTotalUsedSpace();

        Task SignOut(string save);

        Task<string> CalculateLocalStreamContentId(Stream stream);
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

        Task<FSItem.Builder> GetNode(string id);

        Task<INodeExtendedInfo> GetNodeExtended(string id);

        Task<FSItem.Builder> GetRoot();

        Task<FSItem.Builder> Move(string itemId, string oldParentId, string newParentId);

        Task Remove(string itemId, string parentId);

        Task<FSItem.Builder> Rename(string id, string newName);

        Task<string> ShareNode(string id, NodeShareType type);

        Task Trash(string id);
    }
}