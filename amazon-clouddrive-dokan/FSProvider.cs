namespace Azi.Cloud.DokanNet
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Common;
    using Newtonsoft.Json;
    using Tools;

    public delegate void StatisticUpdateDelegate(IHttpCloud cloud, StatisticUpdateReason reason, AStatisticFileInfo info);

    public enum StatisticUpdateReason
    {
        UploadAdded,
        UploadFinished,
        DownloadAdded,
        DownloadFinished,
        DownloadFailed,
        UploadFailed,
        Progress,
        UploadAborted
    }

    public class FSProvider : IDisposable
    {
        private readonly IHttpCloud cloud;

        private readonly ItemsTreeCache itemsTreeCache = new ItemsTreeCache();

        private string cachePath;

        private bool disposedValue; // To detect redundant calls

        private StatisticUpdateDelegate onStatisticsUpdated;

        public FSProvider(IHttpCloud cloud, StatisticUpdateDelegate statisticUpdate)
        {
            onStatisticsUpdated = statisticUpdate;

            this.cloud = cloud;
            SmallFilesCache = new SmallFilesCache(cloud);
            SmallFilesCache.OnDownloadStarted = (info) =>
            {
                onStatisticsUpdated(cloud, StatisticUpdateReason.DownloadAdded, new DownloadStatisticInfo(info));
            };
            SmallFilesCache.OnDownloaded = (info) =>
            {
                onStatisticsUpdated(cloud, StatisticUpdateReason.DownloadFinished, new DownloadStatisticInfo(info));
            };
            SmallFilesCache.OnDownloadFailed = (info) =>
            {
                onStatisticsUpdated(cloud, StatisticUpdateReason.DownloadFailed, new DownloadStatisticInfo(info));
            };

            UploadService = new UploadService(2, cloud);

            UploadService.OnUploadFailed = UploadFailed;

            UploadService.OnUploadFinished = UploadFinished;

            UploadService.OnUploadProgress = (item, done) =>
            {
                onStatisticsUpdated(cloud, StatisticUpdateReason.Progress, new UploadStatisticInfo(item) { Done = done });
            };

            UploadService.OnUploadAdded = item =>
            {
                itemsTreeCache.Add(item.ToFSItem());
                onStatisticsUpdated(cloud, StatisticUpdateReason.UploadAdded, new UploadStatisticInfo(item));
            };
            UploadService.Start();
        }

        public long AvailableFreeSpace => cloud.AvailableFreeSpace;

        public string CachePath
        {
            get
            {
                return cachePath;
            }

            set
            {
                var val = Path.GetFullPath(value);
                if (cachePath == val)
                {
                    return;
                }

                if (cachePath != null)
                {
                    SmallFilesCache.Clear().Wait();
                }

                cachePath = val;
                SmallFilesCache.CachePath = val;
                UploadService.CachePath = val;
            }
        }

        public string FileSystemName => "Cloud Drive";

        public SmallFilesCache SmallFilesCache { get; private set; }

        public long SmallFilesCacheSize
        {
            get
            {
                return SmallFilesCache.CacheSize;
            }

            set
            {
                SmallFilesCache.CacheSize = value;
            }
        }

        public long SmallFileSizeLimit { get; set; } = 20 * 1024 * 1024;

        public long TotalFreeSpace => cloud.TotalFreeSpace;

        public long TotalSize => cloud.TotalSize;

        public long TotalUsedSpace => cloud.TotalUsedSpace;

        public UploadService UploadService { get; private set; }

        public string VolumeName { get; set; }

        public async Task BuildItemInfo(FSItem item)
        {
            var info = await cloud.Nodes.GetNodeExtended(item.Id);

            var str = JsonConvert.SerializeObject(info);
            item.Info = Encoding.UTF8.GetBytes(str);
        }

        public void CancelUpload(string id)
        {
            UploadService.CancelUpload(id);
        }

        public async Task ClearSmallFilesCache()
        {
            await SmallFilesCache.Clear();
        }

        public void CreateDir(string filePath)
        {
            var dir = Path.GetDirectoryName(filePath);
            var dirNode = GetItem(dir);

            var name = Path.GetFileName(filePath);
            var node = cloud.Nodes.CreateFolder(dirNode.Id, name).Result;

            itemsTreeCache.Add(node.SetParentPath(Path.GetDirectoryName(filePath)).Build());
        }

        public void DeleteDir(string filePath)
        {
            var item = GetItem(filePath);
            if (item != null)
            {
                if (!item.IsDir)
                {
                    throw new InvalidOperationException("Not dir");
                }

                DeleteItem(filePath, item);
                itemsTreeCache.DeleteDir(filePath);
            }
        }

        public void DeleteFile(string filePath)
        {
            var item = GetItem(filePath);
            if (item != null)
            {
                if (item.IsDir)
                {
                    throw new InvalidOperationException("Not file");
                }

                DeleteItem(filePath, item);
                itemsTreeCache.DeleteFile(filePath);
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);

            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }

        public bool Exists(string filePath)
        {
            return GetItem(filePath) != null;
        }

        public async Task<IList<FSItem>> GetDirItems(string folderPath)
        {
            var cached = itemsTreeCache.GetDir(folderPath);
            if (cached != null)
            {
                // Log.Warn("Got cached dir:\r\n  " + string.Join("\r\n  ", cached));
                return (await Task.WhenAll(cached.Select(i => FetchNode(i)))).Where(i => i != null).ToList();
            }

            var folderNode = GetItem(folderPath);
            var nodes = await cloud.Nodes.GetChildren(folderNode?.Id);
            var items = new List<FSItem>(nodes.Count);
            var curdir = folderPath;
            if (curdir == "\\")
            {
                curdir = string.Empty;
            }

            foreach (var node in nodes)
            {
                items.Add(node.SetParentPath(curdir).Build());
            }

            // Log.Warn("Got real dir:\r\n  " + string.Join("\r\n  ", items.Select(i => i.Path)));
            itemsTreeCache.AddDirItems(folderPath, items);
            return items;
        }

        public byte[] GetExtendedInfo(List<string> streamNameGroups, FSItem item)
        {
            switch (streamNameGroups[1])
            {
                case CloudDokanNetAssetInfo.StreamNameShareReadOnly:
                    return Encoding.UTF8.GetBytes(cloud.Nodes.ShareNode(item.Id, NodeShareType.ReadOnly).Result);

                case CloudDokanNetAssetInfo.StreamNameShareReadWrite:
                    return Encoding.UTF8.GetBytes(cloud.Nodes.ShareNode(item.Id, NodeShareType.ReadWrite).Result);

                default:
                    return new byte[0];
            }
        }

        public FSItem GetItem(string itemPath)
        {
            return FetchNode(itemPath).Result;
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~FSProvider() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }
        public void MoveFile(string oldPath, string newPath, bool replace)
        {
            if (oldPath == newPath)
            {
                return;
            }

            Log.Trace($"Move: {oldPath} to {newPath} replace:{replace}");

            var oldDir = Path.GetDirectoryName(oldPath);
            var oldName = Path.GetFileName(oldPath);
            var newDir = Path.GetDirectoryName(newPath);
            var newName = Path.GetFileName(newPath);

            var item = GetItem(oldPath);
            item = WaitForReal(item, 25000);
            if (oldName != newName)
            {
                if (item.Length > 0 || item.IsDir)
                {
                    item = cloud.Nodes.Rename(item.Id, newName).Result.SetParentPath(oldDir).Build();
                }
                else
                {
                    item = new FSItem(item)
                    {
                        Path = Path.Combine(oldDir, newName)
                    };
                }

                if (item == null)
                {
                    throw new InvalidOperationException("Can not rename");
                }
            }

            if (oldDir != newDir)
            {
                var oldDirNodeTask = FetchNode(oldDir);
                var newDirNodeTask = FetchNode(newDir);
                Task.WaitAll(oldDirNodeTask, newDirNodeTask);
                if (item.Length > 0 || item.IsDir)
                {
                    item = cloud.Nodes.Move(item.Id, oldDirNodeTask.Result.Id, newDirNodeTask.Result.Id).Result.SetParentPath(newDir).Build();
                    if (item == null)
                    {
                        throw new InvalidOperationException("Can not move");
                    }
                }
                else
                {
                    item = new FSItem(item)
                    {
                        Path = newPath
                    };
                }
            }

            if (item.IsDir)
            {
                itemsTreeCache.MoveDir(oldPath, item);
            }
            else
            {
                itemsTreeCache.MoveFile(oldPath, item);
            }
        }

#pragma warning disable RECS0154 // Parameter is never used
        public IBlockStream OpenFile(string filePath, FileMode mode, FileAccess fileAccess, FileShare share, FileOptions options)
#pragma warning restore RECS0154 // Parameter is never used
        {
            var item = GetItem(filePath);
            if (fileAccess == FileAccess.Read)
            {
                if (item == null)
                {
                    return null;
                }

                Log.Trace($"Opening {filePath} for Read");

                var result = SmallFilesCache.OpenReadCachedOnly(item);
                if (result != null)
                {
                    return result;
                }

                item = WaitForReal(item, 25000);

                if (item.Length < SmallFileSizeLimit)
                {
                    return SmallFilesCache.OpenReadWithDownload(item);
                }

                onStatisticsUpdated(cloud, StatisticUpdateReason.DownloadAdded, new DownloadStatisticInfo(item));
                var buffered = new BufferedHttpCloudBlockReader(item, cloud);
                buffered.OnClose = () =>
                  {
                      onStatisticsUpdated(cloud, StatisticUpdateReason.DownloadFinished, new DownloadStatisticInfo(item));
                  };

                return buffered;
            }

            if (item == null || item.Length == 0)
            {
                Log.Trace($"Creating {filePath} as New because mode:{mode} and {((item == null) ? "item is null" : "length is 0")}");

                var dir = Path.GetDirectoryName(filePath);
                var name = Path.GetFileName(filePath);
                var dirItem = GetItem(dir);

                item = FSItem.MakeUploading(filePath, Guid.NewGuid().ToString(), dirItem.Id, 0);

                var file = UploadService.OpenNew(item);

                itemsTreeCache.Add(item);

                return file;
            }

            if (item == null)
            {
                return null;
            }

            item = WaitForReal(item, 25000);

            if ((mode == FileMode.Create || mode == FileMode.Truncate) && item.Length > 0)
            {
                Log.Trace($"Opening {filePath} as Truncate because mode:{mode} and length {item.Length}");
                item.Length = 0;
                SmallFilesCache.Delete(item);
                item.MakeUploading();
                var file = UploadService.OpenTruncate(item);

                return file;
            }

            if (mode == FileMode.Open || mode == FileMode.Append || mode == FileMode.OpenOrCreate)
            {
                Log.Trace($"Opening {filePath} as ReadWrite because mode:{mode} and length {item.Length}");
                if (item.Length < SmallFileSizeLimit)
                {
                    var file = SmallFilesCache.OpenReadWrite(item);
                    file.OnChangedAndClosed = (it, path) =>
                    {
                        it.LastWriteTime = DateTime.UtcNow;

                        if (!it.IsUploading)
                        {
                            it.MakeUploading();
                            var olditemPath = Path.Combine(SmallFilesCache.CachePath, item.Id);
                            var newitemPath = Path.Combine(UploadService.CachePath, item.Id);

                            if (File.Exists(newitemPath))
                            {
                                File.Delete(newitemPath);
                            }

                            SymbolicLink.CreateFile(GetRelativePath(olditemPath, Path.GetDirectoryName(newitemPath)), newitemPath);
                            SmallFilesCache.AddExisting(it);
                        }

                        UploadService.AddOverwrite(it);
                    };

                    return file;
                }

                Log.Warn("File is too big for ReadWrite: " + filePath);
            }

            return null;
        }

        public void Stop()
        {
            UploadService.Stop();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    itemsTreeCache.Dispose();
                    UploadService.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.
                disposedValue = true;
            }
        }

        private void DeleteItem(string filePath, FSItem item)
        {
            try
            {
                if (item.ParentIds.Count == 1)
                {
                    if (item.IsUploading)
                    {
                        UploadService.CancelUpload(item.Id);
                    }
                    else
                    {
                        cloud.Nodes.Trash(item.Id).Wait();
                    }
                }
                else
                {
                    var dir = Path.GetDirectoryName(filePath);
                    var dirItem = GetItem(dir);
                    cloud.Nodes.Remove(dirItem.Id, item.Id).Wait();
                }
            }
            catch (AggregateException ex)
            {
                throw ex.Flatten();
            }
            catch (CloudException ex) when (ex.Error == HttpStatusCode.NotFound || ex.Error == HttpStatusCode.Conflict)
            {
                Log.Warn(ex.Error.ToString());
            }
        }

        private async Task<FSItem> FetchNode(string itemPath)
        {
            if (itemPath == "\\" || itemPath == string.Empty)
            {
                return (await cloud.Nodes.GetRoot()).BuildRoot();
            }

            var cached = itemsTreeCache.GetItem(itemPath);
            if (cached != null)
            {
                if (cached.NotExistingDummy)
                {
                    // Log.Warn("NonExisting path from cache: " + itemPath);
                    return null;
                }

                return cached;
            }

            var folders = new LinkedList<string>();
            var curpath = itemPath;
            FSItem item = null;
            do
            {
                folders.AddFirst(Path.GetFileName(curpath));
                curpath = Path.GetDirectoryName(curpath);
                if (curpath == "\\" || string.IsNullOrEmpty(curpath))
                {
                    break;
                }

                item = itemsTreeCache.GetItem(curpath);
            }
            while (item == null);
            if (item == null)
            {
                item = (await cloud.Nodes.GetRoot()).BuildRoot();
            }

            foreach (var name in folders)
            {
                if (curpath == "\\")
                {
                    curpath = string.Empty;
                }

                var newpath = curpath + "\\" + name;

                var newnode = await cloud.Nodes.GetChild(item.Id, name);
                if (newnode == null)
                {
                    itemsTreeCache.AddItemOnly(FSItem.MakeNotExistingDummy(newpath));

                    // Log.Error("NonExisting path from server: " + itemPath);
                    return null;
                }

                item = newnode.SetParentPath(curpath).Build();
                itemsTreeCache.Add(item);
                curpath = newpath;
            }

            return item;
        }

        private string GetRelativePath(string filepath, string relativeto)
        {
            var pathUri = new Uri(filepath);
            if (!relativeto.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.InvariantCulture))
            {
                relativeto += Path.DirectorySeparatorChar;
            }

            var relativetoUri = new Uri(relativeto);
            return Uri.UnescapeDataString(relativetoUri.MakeRelativeUri(pathUri).ToString().Replace('/', Path.DirectorySeparatorChar));
        }

        private void UploadFailed(UploadInfo uploaditem, FailReason reason, string message)
        {
            var olditemPath = Path.Combine(UploadService.CachePath, uploaditem.Id);
            File.Delete(olditemPath);

            switch (reason)
            {
                case FailReason.ZeroLength:
                    var item = GetItem(uploaditem.Path);
                    item?.MakeNotUploading();
                    onStatisticsUpdated(cloud, StatisticUpdateReason.UploadFinished, new UploadStatisticInfo(uploaditem));
                    return;

                case FailReason.FileNotFound:
                case FailReason.Conflict:
                case FailReason.NoFolderNode:
                    onStatisticsUpdated(cloud, StatisticUpdateReason.UploadAborted, new UploadStatisticInfo(uploaditem, message));
                    return;

                case FailReason.Cancelled:
                    if (!uploaditem.Overwrite)
                    {
                        itemsTreeCache.DeleteFile(uploaditem.Path);
                    }

                    onStatisticsUpdated(cloud, StatisticUpdateReason.UploadFinished, new UploadStatisticInfo(uploaditem));
                    return;
            }

            onStatisticsUpdated(cloud, StatisticUpdateReason.UploadFailed, new UploadStatisticInfo(uploaditem, message));
            itemsTreeCache.DeleteFile(uploaditem.Path);
        }

        private void UploadFinished(UploadInfo item, FSItem.Builder node)
        {
            onStatisticsUpdated(cloud, StatisticUpdateReason.UploadFinished, new UploadStatisticInfo(item));

            var newitem = node.SetParentPath(Path.GetDirectoryName(item.Path)).Build();
            var olditemPath = Path.Combine(UploadService.CachePath, item.Id);
            var newitemPath = Path.Combine(SmallFilesCache.CachePath, node.Id);

            if (!File.Exists(newitemPath))
            {
                File.Move(olditemPath, newitemPath);
            }
            else
            {
                File.Delete(olditemPath);
            }

            SmallFilesCache.AddExisting(newitem);
            itemsTreeCache.Update(newitem);
        }

        private FSItem WaitForReal(FSItem item, int timeout)
        {
            var timeouttime = DateTime.UtcNow.AddMilliseconds(timeout);
            while (item.IsUploading)
            {
                if (DateTime.UtcNow > timeouttime)
                {
                    throw new TimeoutException();
                }

                Thread.Sleep(100);
                item = GetItem(item.Path);
            }

            return item;
        }
    }
}