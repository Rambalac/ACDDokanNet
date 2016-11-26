namespace Azi.Cloud.DokanNet
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading.Tasks;
    using Common;
    using Newtonsoft.Json;
    using Tools;

    public delegate Task StatisticUpdateDelegate(IHttpCloud cloud, StatisticUpdateReason reason, AStatisticFileInfo info);

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

            UploadService.OnUploadProgress = async (item, done) =>
            {
                await onStatisticsUpdated(cloud, StatisticUpdateReason.Progress, new UploadStatisticInfo(item) { Done = done });
            };

            UploadService.OnUploadAdded = async (item) =>
            {
                itemsTreeCache.Add(item.ToFSItem());
                await onStatisticsUpdated(cloud, StatisticUpdateReason.UploadAdded, new UploadStatisticInfo(item));
            };
            UploadService.Start();
        }

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
                    SmallFilesCache.ClearAllInBackground().Wait();
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

        public UploadService UploadService { get; private set; }

        public string VolumeName { get; set; }

        public async Task<long> GetTotalFreeSpace() => await cloud.GetTotalFreeSpace();

        public async Task<long> GetTotalSize() => await cloud.GetTotalSize();

        public async Task<long> GetTotalUsedSpace() => await cloud.GetTotalUsedSpace();

        public async Task<long> GetAvailableFreeSpace() => await cloud.GetAvailableFreeSpace();

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
            await SmallFilesCache.ClearAllInBackground();
        }

        public async Task CreateDir(string filePath)
        {
            var dir = Path.GetDirectoryName(filePath);
            var dirNode = await FetchNode(dir);

            var name = Path.GetFileName(filePath);
            var node = await cloud.Nodes.CreateFolder(dirNode.Id, name);

            itemsTreeCache.Add(node.SetParentPath(Path.GetDirectoryName(filePath)).Build());
        }

        public async Task DeleteDir(string filePath)
        {
            var item = await FetchNode(filePath);
            if (item != null)
            {
                if (!item.IsDir)
                {
                    throw new InvalidOperationException("Not dir");
                }

                await DeleteItem(filePath, item);
                itemsTreeCache.DeleteDir(filePath);
            }
            else
            {
                throw new FileNotFoundException();
            }
        }

        public async Task DeleteFile(string filePath)
        {
            var item = await FetchNode(filePath);
            if (item != null)
            {
                if (item.IsDir)
                {
                    throw new InvalidOperationException("Not file");
                }

                await DeleteItem(filePath, item);
                itemsTreeCache.DeleteFile(filePath);
            }
            else
            {
                throw new FileNotFoundException();
            }

            try
            {
                SmallFilesCache.Delete(item);
            }
            catch (FileNotFoundException)
            {
                Log.Trace("Skip, File is not in SmallFilesCache");
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

        public async Task<bool> Exists(string filePath)
        {
            return await FetchNode(filePath) != null;
        }

        public async Task<FSItem> FetchNode(string itemPath)
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

        public async Task<IList<FSItem>> GetDirItems(string folderPath)
        {
            var cached = itemsTreeCache.GetDir(folderPath);
            if (cached != null)
            {
                // Log.Warn("Got cached dir:\r\n  " + string.Join("\r\n  ", cached));
                return (await Task.WhenAll(cached.Select(i => FetchNode(i)))).Where(i => i != null).ToList();
            }

            var folderNode = await FetchNode(folderPath);
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

        public async Task<byte[]> GetExtendedInfo(string[] streamNameGroups, FSItem item)
        {
            switch (streamNameGroups[1])
            {
                case CloudDokanNetAssetInfo.StreamNameShareReadOnly:
                    return Encoding.UTF8.GetBytes(await cloud.Nodes.ShareNode(item.Id, NodeShareType.ReadOnly));

                case CloudDokanNetAssetInfo.StreamNameShareReadWrite:
                    return Encoding.UTF8.GetBytes(await cloud.Nodes.ShareNode(item.Id, NodeShareType.ReadWrite));

                default:
                    return new byte[0];
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~FSProvider() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }
        public async Task MoveFile(string oldPath, string newPath, bool replace)
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

            var item = await FetchNode(oldPath);
            await WaitForReal(item, 25000);
            if (oldName != newName)
            {
                if (item.Length > 0 || item.IsDir)
                {
                    item = (await cloud.Nodes.Rename(item.Id, newName)).SetParentPath(oldDir).Build();
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
                    item = cloud.Nodes.Move(item.Id, (await oldDirNodeTask).Id, (await newDirNodeTask).Id).Result.SetParentPath(newDir).Build();
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
        public async Task<IBlockStream> OpenFile(string filePath, FileMode mode, FileAccess fileAccess, FileShare share, FileOptions options)
#pragma warning restore RECS0154 // Parameter is never used
        {
            var item = await FetchNode(filePath);
            if (fileAccess == FileAccess.Read)
            {
                if (item == null)
                {
                    return null;
                }

                Log.Trace($"Opening {filePath} for Read");

                if (!item.IsUploading && item.Length < SmallFileSizeLimit)
                {
                    return SmallFilesCache.OpenReadWithDownload(item);
                }

                var result = SmallFilesCache.OpenReadCachedOnly(item);
                if (result != null)
                {
                    return result;
                }

                await WaitForReal(item, 25000);

                await onStatisticsUpdated(cloud, StatisticUpdateReason.DownloadAdded, new DownloadStatisticInfo(item));
                var buffered = new BufferedHttpCloudBlockReader(item, cloud);
                buffered.OnClose = async () =>
                  {
                      await onStatisticsUpdated(cloud, StatisticUpdateReason.DownloadFinished, new DownloadStatisticInfo(item));
                  };

                return buffered;
            }

            if (item == null || item.Length == 0)
            {
                Log.Trace($"Creating {filePath} as New because mode:{mode} and {((item == null) ? "item is null" : "length is 0")}");

                var dir = Path.GetDirectoryName(filePath);
                var name = Path.GetFileName(filePath);
                var dirItem = await FetchNode(dir);

                item = FSItem.MakeUploading(filePath, Guid.NewGuid().ToString(), dirItem.Id, 0);

                var file = UploadService.OpenNew(item);
                SmallFilesCache.AddAsLink(item, file.UploadCachePath);

                itemsTreeCache.Add(item);

                return file;
            }

            if (item == null)
            {
                return null;
            }

            await WaitForReal(item, 25000);

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
                    file.OnChangedAndClosed = async (it, path) =>
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

                            HardLink.Create(olditemPath, newitemPath);
                            SmallFilesCache.AddExisting(it);
                        }

                        await UploadService.AddOverwrite(it);
                    };

                    return file;
                }

                Log.Warn("File is too big for ReadWrite: " + filePath);
            }

            return null;
        }

        public ByteArrayBlockWriter OpenUploadHere(FSItem item)
        {
            Log.Trace($"Upload from Shell");
            var result = new ByteArrayBlockWriter();
            result.OnClose = async () =>
              {
                  var bytes = result.Content.ToArray();
                  var str = Encoding.UTF8.GetString(bytes);
                  var list = JsonConvert.DeserializeObject<CloudDokanNetUploadHereInfo>(str);
                  try
                  {
                      await MakeUploads(item, list.Files);
                  }
                  catch (Exception ex)
                  {
                      Log.Error($"UploadHere error: {ex}");
                      throw;
                  }
              };
            return result;
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

        private async Task<FSItem> CheckCreateFolder(FSItem parent, string name)
        {
            var node = await cloud.Nodes.GetChild(parent.Id, name);
            if (node != null)
            {
                return node.SetParentPath(parent.Path).Build();
            }

            var result = (await cloud.Nodes.CreateFolder(parent.Id, name)).SetParentPath(parent.Path).Build();
            itemsTreeCache.Add(result);
            return result;
        }

        private async Task DeleteItem(string filePath, FSItem item)
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
                        await cloud.Nodes.Trash(item.Id);
                    }
                }
                else
                {
                    var dir = Path.GetDirectoryName(filePath);
                    var dirItem = await FetchNode(dir);
                    await cloud.Nodes.Remove(dirItem.Id, item.Id);
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

        private async Task MakeUploads(FSItem dest, List<string> files)
        {
            var currentfiles = new Queue<UploadTaskItem>(files.Select(f => new UploadTaskItem { Parent = dest, File = f }));

            while (currentfiles.Count > 0)
            {
                var item = currentfiles.Dequeue();
                if (Directory.Exists(item.File))
                {
                    var created = await CheckCreateFolder(item.Parent, Path.GetFileName(item.File));
                    if (created != null)
                    {
                        foreach (var file in Directory.EnumerateFileSystemEntries(item.File))
                        {
                            currentfiles.Enqueue(new UploadTaskItem { Parent = created, File = file });
                        }
                    }
                }
                else
                {
                    await UploadService.AddUpload(item.Parent, item.File);
                }
            }
        }

        private async Task UploadFailed(UploadInfo uploaditem, FailReason reason, string message)
        {
            switch (reason)
            {
                case FailReason.ZeroLength:
                    var item = await FetchNode(uploaditem.Path);
                    item?.MakeNotUploading();
                    await onStatisticsUpdated(cloud, StatisticUpdateReason.UploadFinished, new UploadStatisticInfo(uploaditem));
                    return;

                case FailReason.FileNotFound:
                case FailReason.Conflict:
                case FailReason.NoFolderNode:
                    await onStatisticsUpdated(cloud, StatisticUpdateReason.UploadAborted, new UploadStatisticInfo(uploaditem, message));
                    return;

                case FailReason.Cancelled:
                    if (!uploaditem.Overwrite)
                    {
                        itemsTreeCache.DeleteFile(uploaditem.Path);
                    }

                    await onStatisticsUpdated(cloud, StatisticUpdateReason.UploadFinished, new UploadStatisticInfo(uploaditem));
                    return;
            }

            await onStatisticsUpdated(cloud, StatisticUpdateReason.UploadFailed, new UploadStatisticInfo(uploaditem, message));
            itemsTreeCache.DeleteFile(uploaditem.Path);
        }

        private Task UploadFinished(UploadInfo item, FSItem.Builder node)
        {
            onStatisticsUpdated(cloud, StatisticUpdateReason.UploadFinished, new UploadStatisticInfo(item));

            var newitem = node.SetParentPath(Path.GetDirectoryName(item.Path)).Build();

            itemsTreeCache.Update(newitem);

            return Task.FromResult(0);
        }

        private async Task WaitForReal(FSItem item, int timeout)
        {
            var timeouttime = DateTime.UtcNow.AddMilliseconds(timeout);
            while (item.IsUploading)
            {
                if (DateTime.UtcNow > timeouttime)
                {
                    throw new TimeoutException();
                }

                await Task.Delay(1000);
                item = await FetchNode(item.Path);
            }
        }

        private class UploadTaskItem
        {
            public string File { get; set; }

            public FSItem Parent { get; set; }
        }
    }
}