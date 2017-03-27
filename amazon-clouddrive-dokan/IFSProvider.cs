namespace Azi.Cloud.DokanNet
{
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using Common;
    using FileInformation=global::DokanNet.FileInformation;

    public interface IFSProvider
    {
        string CachePath { get; set; }

        bool CheckFileHash { get; set; }

        string FileSystemName { get; }

        SmallFilesCache SmallFilesCache { get; }

        long SmallFilesCacheSize { get; set; }

        long SmallFileSizeLimit { get; set; }

        string VolumeName { get; set; }

        Task BuildItemInfo(FSItem item);

        void CancelUpload(string id);

        Task ClearSmallFilesCache();

        Task CreateDir(string filePath);

        Task DeleteDir(string filePath);

        Task DeleteFile(string filePath);

        void Dispose();

        Task<bool> Exists(string filePath);

        Task<FSItem> FetchNode(string itemPath);

        Task<long> GetAvailableFreeSpace();

        Task<IList<FSItem>> GetDirItems(string folderPath);

        Task<byte[]> GetExtendedInfo(string[] streamNameGroups, FSItem item);

        Task<long> GetTotalFreeSpace();

        Task<long> GetTotalSize();

        Task<long> GetTotalUsedSpace();

        Task MoveFile(string oldPath, string newPath, bool replace);

        Task<IBlockStream> OpenFile(string filePath, FileMode mode, FileAccess fileAccess, FileShare share, FileOptions options);

        ByteArrayBlockWriter OpenUploadHere(FSItem item);

        void StopUpload();

        Task<FileInformation?> GetItemInfo(string fileName);
    }
}