namespace Azi.Cloud.DokanNet
{
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using Common;
    using FileInformation=global::DokanNet.FileInformation;

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

        public async Task BuildItemInfo(FSItem item) => await provider.BuildItemInfo(item).ConfigureAwait(false);

        public void CancelUpload(string id) => provider.CancelUpload(id);

        public async Task ClearSmallFilesCache() => await provider.ClearSmallFilesCache().ConfigureAwait(false);

        public async Task CreateDir(string filePath) => await provider.CreateDir(FixRoot(filePath)).ConfigureAwait(false);

        public async Task DeleteDir(string filePath) => await provider.DeleteDir(FixRoot(filePath)).ConfigureAwait(false);

        public async Task DeleteFile(string filePath) => await provider.DeleteFile(FixRoot(filePath)).ConfigureAwait(false);

        public void Dispose()
        {
            provider.Dispose();
        }

        public async Task<bool> Exists(string filePath) => await provider.Exists(FixRoot(filePath)).ConfigureAwait(false);

        public async Task<FSItem> FetchNode(string itemPath) => await provider.FetchNode(FixRoot(itemPath)).ConfigureAwait(false);

        public async Task<long> GetAvailableFreeSpace() => await provider.GetAvailableFreeSpace().ConfigureAwait(false);

        public async Task<IList<FSItem>> GetDirItems(string folderPath)
        {
            var result = await provider.GetDirItems(FixRoot(folderPath)).ConfigureAwait(false);
            foreach (var fsItem in result)
            {
                if (fsItem.Path.StartsWith(rootFolder))
                {
                    fsItem.Path = fsItem.Path.Substring(rootFolder.Length);
                }
            }

            return result;
        }

        public async Task<byte[]> GetExtendedInfo(string[] streamNameGroups, FSItem item) => await provider.GetExtendedInfo(streamNameGroups, item).ConfigureAwait(false);

        public async Task<long> GetTotalFreeSpace() => await provider.GetTotalFreeSpace().ConfigureAwait(false);

        public async Task<long> GetTotalSize() => await provider.GetTotalSize().ConfigureAwait(false);

        public async Task<long> GetTotalUsedSpace() => await provider.GetTotalUsedSpace().ConfigureAwait(false);

        public async Task MoveFile(string oldPath, string newPath, bool replace) => await provider.MoveFile(FixRoot(oldPath), FixRoot(newPath), replace).ConfigureAwait(false);

        public async Task<IBlockStream> OpenFile(string filePath, FileMode mode, FileAccess fileAccess, FileShare share, FileOptions options)
        {
            return await provider.OpenFile(FixRoot(filePath), mode, fileAccess, share, options).ConfigureAwait(false);
        }

        public async Task<FileInformation?> GetItemInfo(string fileName) => await provider.GetItemInfo(FixRoot(fileName)).ConfigureAwait(false);

        public ByteArrayBlockWriter OpenUploadHere(FSItem item) => provider.OpenUploadHere(item);

        public async Task SetRootFolder(string rootfolder)
        {
            if (string.IsNullOrWhiteSpace(rootfolder))
            {
                rootFolder = string.Empty;
                return;
            }

            rootFolder = "\\" + rootfolder.TrimStart('\\');
            rootFolder = await provider.Exists(rootfolder).ConfigureAwait(false) ? rootfolder : string.Empty;
        }

        public void StopUpload() => provider.StopUpload();

        private string FixRoot(string folderPath) => Path.Combine(rootFolder, folderPath.TrimStart('\\'));
    }
}