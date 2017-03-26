namespace Azi.Cloud.DokanNet
{
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using Common;

    public class RootFolderFSProvider : IFSProvider
    {
        private readonly IFSProvider provider;
        private string rootFolder;

        public RootFolderFSProvider(IFSProvider provider)
        {
            this.provider = provider;
        }

        public string CachePath
        {
            get { return provider.CachePath; }
            set { provider.CachePath = value; }
        }

        public bool CheckFileHash
        {
            get { return provider.CheckFileHash; }
            set { provider.CheckFileHash = value; }
        }

        public string FileSystemName => provider.FileSystemName;

        public SmallFilesCache SmallFilesCache => provider.SmallFilesCache;

        public long SmallFilesCacheSize
        {
            get { return provider.SmallFilesCacheSize; }
            set { provider.SmallFilesCacheSize = value; }
        }

        public long SmallFileSizeLimit
        {
            get { return provider.SmallFileSizeLimit; }
            set { provider.SmallFileSizeLimit = value; }
        }

        public string VolumeName
        {
            get { return provider.VolumeName; }
            set { provider.VolumeName = value; }
        }

        public Task BuildItemInfo(FSItem item)
        {
            return provider.BuildItemInfo(item);
        }

        public void CancelUpload(string id)
        {
            provider.CancelUpload(id);
        }

        public Task ClearSmallFilesCache()
        {
            return provider.ClearSmallFilesCache();
        }

        public Task CreateDir(string filePath)
        {
            return provider.CreateDir(FixRoot(filePath));
        }

        public Task DeleteDir(string filePath)
        {
            return provider.DeleteDir(FixRoot(filePath));
        }

        public Task DeleteFile(string filePath)
        {
            return provider.DeleteFile(FixRoot(filePath));
        }

        public void Dispose()
        {
            provider.Dispose();
        }

        public Task<bool> Exists(string filePath)
        {
            return provider.Exists(FixRoot(filePath));
        }

        public Task<FSItem> FetchNode(string itemPath)
        {
            return provider.FetchNode(FixRoot(itemPath));
        }

        public Task<long> GetAvailableFreeSpace()
        {
            return provider.GetAvailableFreeSpace();
        }

        public async Task<IList<FSItem>> GetDirItems(string folderPath)
        {
            var result = await provider.GetDirItems(FixRoot(folderPath));
            foreach (var fsItem in result)
            {
                if (fsItem.Path.StartsWith(rootFolder))
                {
                    fsItem.Path = fsItem.Path.Substring(rootFolder.Length);
                }
            }

            return result;
        }

        public Task<byte[]> GetExtendedInfo(string[] streamNameGroups, FSItem item)
        {
            return provider.GetExtendedInfo(streamNameGroups, item);
        }

        public Task<long> GetTotalFreeSpace() => provider.GetTotalFreeSpace();

        public Task<long> GetTotalSize() => provider.GetTotalSize();

        public Task<long> GetTotalUsedSpace() => provider.GetTotalUsedSpace();

        public Task MoveFile(string oldPath, string newPath, bool replace)
        {
            return provider.MoveFile(FixRoot(oldPath), FixRoot(newPath), replace);
        }

        public Task<IBlockStream> OpenFile(string filePath, FileMode mode, FileAccess fileAccess, FileShare share, FileOptions options)
        {
            return provider.OpenFile(FixRoot(filePath), mode, fileAccess, share, options);
        }

        public ByteArrayBlockWriter OpenUploadHere(FSItem item)
        {
            return provider.OpenUploadHere(item);
        }

        public async Task SetRootFolder(string rootfolder)
        {
            if (string.IsNullOrWhiteSpace(rootfolder))
            {
                rootFolder = string.Empty;
                return;
            }

            rootFolder = "\\" + rootfolder.TrimStart('\\');
            rootFolder = await provider.Exists(rootfolder) ? rootfolder : string.Empty;
        }

        public void StopUpload()
        {
            provider.StopUpload();
        }

        private string FixRoot(string folderPath)
        {
            return Path.Combine(rootFolder, folderPath.TrimStart('\\'));
        }
    }
}