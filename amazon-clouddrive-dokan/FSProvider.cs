using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azi.Amazon.CloudDrive;
using Azi.Amazon.CloudDrive.JsonObjects;
using Azi.Tools;
using Newtonsoft.Json;
using ShellExtension;

namespace Azi.ACDDokanNet
{
    public class FSProvider : IDisposable
    {
        private static readonly AmazonNodeKind[] FsItemKinds = { AmazonNodeKind.FILE, AmazonNodeKind.FOLDER };

        private readonly AmazonDrive amazon;

        private readonly ItemsTreeCache itemsTreeCache = new ItemsTreeCache();

        private string cachePath;

        private int downloadingCount = 0;

        private int uploadingCount = 0;

        private bool disposedValue = false; // To detect redundant calls

        private StatisticsUpdated onStatisticsUpdated;

        public FSProvider(AmazonDrive amazon)
        {
            this.amazon = amazon;
            SmallFilesCache = new SmallFilesCache(amazon);
            SmallFilesCache.OnDownloadStarted = (id) =>
            {
                Interlocked.Increment(ref downloadingCount);
                OnStatisticsUpdated?.Invoke(downloadingCount, uploadingCount);
            };
            SmallFilesCache.OnDownloaded = (id) =>
            {
                Interlocked.Decrement(ref downloadingCount);
                OnStatisticsUpdated?.Invoke(downloadingCount, uploadingCount);
            };
            SmallFilesCache.OnDownloadFailed = (id) =>
            {
                Interlocked.Decrement(ref downloadingCount);
                OnStatisticsUpdated?.Invoke(downloadingCount, uploadingCount);
            };

            UploadService = new UploadService(2, amazon);
            UploadService.OnUploadFailed = (uploaditem, reason) =>
            {
                Interlocked.Decrement(ref uploadingCount);
                OnStatisticsUpdated?.Invoke(downloadingCount, uploadingCount);

                var olditemPath = Path.Combine(UploadService.CachePath, uploaditem.Id);
                File.Delete(olditemPath);

                if (reason == FailReason.ZeroLength)
                {
                    var item = GetItem(uploaditem.Path);
                    item?.MakeNotUploading();
                    return;
                }

                itemsTreeCache.DeleteFile(uploaditem.Path);
            };
            UploadService.OnUploadFinished = (item, node) =>
            {
                Interlocked.Decrement(ref uploadingCount);
                OnStatisticsUpdated?.Invoke(downloadingCount, uploadingCount);

                var newitem = FSItem.FromNode(item.Path, node);
                var olditemPath = Path.Combine(UploadService.CachePath, item.Id);
                var newitemPath = Path.Combine(SmallFilesCache.CachePath, node.id);

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
            };
            UploadService.OnUploadResumed = item =>
            {
                itemsTreeCache.Add(item);
                Interlocked.Increment(ref uploadingCount);
                OnStatisticsUpdated?.Invoke(downloadingCount, uploadingCount);
            };
            UploadService.Start();
        }

        public delegate void StatisticsUpdated(int downloading, int uploading);

        public long SmallFileSizeLimit { get; set; } = 20 * 1024 * 1024;

        public SmallFilesCache SmallFilesCache { get; private set; }

        public UploadService UploadService { get; private set; }

        public StatisticsUpdated OnStatisticsUpdated
        {
            get
            {
                return onStatisticsUpdated;
            }

            set
            {
                onStatisticsUpdated = value;
                onStatisticsUpdated?.Invoke(downloadingCount, uploadingCount);
            }
        }

        public long AvailableFreeSpace => amazon.Account.GetQuota().Result.available;

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
                OnStatisticsUpdated?.Invoke(downloadingCount, uploadingCount);
            }
        }

        public long TotalSize => amazon.Account.GetQuota().Result.quota;

        public long TotalFreeSpace => amazon.Account.GetQuota().Result.available;

        public long TotalUsedSpace => amazon.Account.GetUsage().Result.total.total.bytes;

        public string VolumeName => FileSystemName;

        public string FileSystemName => "Amazon Cloud Drive";

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

        public void ClearSmallFilesCache()
        {
            var task = SmallFilesCache.Clear();
        }

        public bool Exists(string filePath)
        {
            return GetItem(filePath) != null;
        }

        public void CreateDir(string filePath)
        {
            var dir = Path.GetDirectoryName(filePath);
            var dirNode = GetItem(dir);

            var name = Path.GetFileName(filePath);
            var node = amazon.Nodes.CreateFolder(dirNode.Id, name).Result;

            itemsTreeCache.Add(FSItem.FromNode(filePath, node));
        }

        public IBlockStream OpenFile(string filePath, FileMode mode, FileAccess fileAccess, FileShare share, FileOptions options)
        {
            var item = GetItem(filePath);
            if (fileAccess == FileAccess.Read)
            {
                if (item == null)
                {
                    return null;
                }

                item = WaitForReal(item, 25000);

                Log.Trace($"Opening {filePath} for Read");

                if (item.Length < SmallFileSizeLimit)
                {
                    return SmallFilesCache.OpenReadWithDownload(item);
                }

                var result = SmallFilesCache.OpenReadCachedOnly(item);
                if (result != null)
                {
                    return result;
                }

                Interlocked.Increment(ref downloadingCount);
                OnStatisticsUpdated?.Invoke(downloadingCount, uploadingCount);
                var buffered = new BufferedAmazonBlockReader(item, amazon);
                buffered.OnClose = () =>
                  {
                      Interlocked.Decrement(ref downloadingCount);
                      OnStatisticsUpdated?.Invoke(downloadingCount, uploadingCount);
                  };

                return buffered;
            }

            if (item == null || item.Length == 0)
            {
#if TRACE
                Log.Trace($"Creating {filePath} as New because mode:{mode} and {((item == null) ? "item is null" : "length is 0")}");
#endif

                var dir = Path.GetDirectoryName(filePath);
                var name = Path.GetFileName(filePath);
                var dirItem = GetItem(dir);

                item = FSItem.MakeUploading(filePath, Guid.NewGuid().ToString(), dirItem.Id, 0);

                var file = UploadService.OpenNew(item);
                Interlocked.Increment(ref uploadingCount);
                OnStatisticsUpdated?.Invoke(downloadingCount, uploadingCount);

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
#if TRACE
                Log.Trace($"Opening {filePath} as Truncate because mode:{mode} and length {item.Length}");
#endif
                item.Length = 0;
                SmallFilesCache.Delete(item);
                item.MakeUploading();
                var file = UploadService.OpenTruncate(item);
                Interlocked.Increment(ref uploadingCount);
                OnStatisticsUpdated?.Invoke(downloadingCount, uploadingCount);

                return file;
            }

            if (mode == FileMode.Open || mode == FileMode.Append || mode == FileMode.OpenOrCreate)
            {
#if TRACE
                Log.Trace($"Opening {filePath} as ReadWrite because mode:{mode} and length {item.Length}");
#endif
                if (item.Length < SmallFileSizeLimit)
                {
                    var file = SmallFilesCache.OpenReadWrite(item);
                    file.OnChangedAndClosed = (it, path) =>
                    {
                        it.LastWriteTime = DateTime.UtcNow;
                        Interlocked.Increment(ref uploadingCount);
                        OnStatisticsUpdated?.Invoke(downloadingCount, uploadingCount);

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

        public async Task<IList<FSItem>> GetDirItems(string folderPath)
        {
            var cached = itemsTreeCache.GetDir(folderPath);
            if (cached != null)
            {
                // Log.Warn("Got cached dir:\r\n  " + string.Join("\r\n  ", cached));
                return (await Task.WhenAll(cached.Select(i => FetchNode(i)))).Where(i => i != null).ToList();
            }

            var folderNode = GetItem(folderPath);
            var nodes = await amazon.Nodes.GetChildren(folderNode?.Id);
            var items = new List<FSItem>(nodes.Count);
            var curdir = folderPath;
            if (curdir == "\\")
            {
                curdir = string.Empty;
            }

            foreach (var node in nodes.Where(n => FsItemKinds.Contains(n.kind)))
            {
                if (node.status != AmazonNodeStatus.AVAILABLE)
                {
                    continue;
                }

                var path = curdir + "\\" + node.name;
                items.Add(FSItem.FromNode(path, node));
            }

            // Log.Warn("Got real dir:\r\n  " + string.Join("\r\n  ", items.Select(i => i.Path)));
            itemsTreeCache.AddDirItems(folderPath, items);
            return items;
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

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);

            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }

        public async Task BuildItemInfo(FSItem item)
        {
            var node = await amazon.Nodes.GetNodeExtended(item.Id);
            var info = new ACDDokanNetItemInfo
            {
                Id = item.Id,
                TempLink = node.tempLink,
                Assets = node.assets?.Select(i => new ACDDokanNetAssetInfo { Id = i.id, TempLink = i.tempLink }).ToList()
            };

            if (node.video != null)
            {
                info.Video = new ACDDokanNetAssetInfoImage { Width = node.video.width, Height = node.video.height };
            }

            if (node.image != null)
            {
                info.Image = new ACDDokanNetAssetInfoImage { Width = node.image.width, Height = node.image.height };
            }

            string str = JsonConvert.SerializeObject(info);
            item.Info = Encoding.UTF8.GetBytes(str);
        }

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
                    item = FSItem.FromNode(Path.Combine(oldDir, newName), amazon.Nodes.Rename(item.Id, newName).Result);
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
                    item = FSItem.FromNode(newPath, amazon.Nodes.Move(item.Id, oldDirNodeTask.Result.Id, newDirNodeTask.Result.Id).Result);
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

        private void DeleteItem(string filePath, FSItem item)
        {
            try
            {
                if (item.ParentIds.Count == 1)
                {
                    amazon.Nodes.Trash(item.Id).Wait();
                }
                else
                {
                    var dir = Path.GetDirectoryName(filePath);
                    var dirItem = GetItem(dir);
                    amazon.Nodes.Remove(dirItem.Id, item.Id).Wait();
                }
            }
            catch (AggregateException ex)
            {
                var webex = ex.InnerException as HttpWebException;
                if (webex == null || (webex.StatusCode != HttpStatusCode.NotFound && webex.StatusCode != HttpStatusCode.Conflict))
                {
                    throw ex.InnerException;
                }
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

        private async Task<FSItem> FetchNode(string itemPath)
        {
            if (itemPath == "\\" || itemPath == string.Empty)
            {
                return FSItem.FromRoot(await amazon.Nodes.GetRoot());
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
                item = FSItem.FromRoot(await amazon.Nodes.GetRoot());
            }

            foreach (var name in folders)
            {
                if (curpath == "\\")
                {
                    curpath = string.Empty;
                }

                curpath = curpath + "\\" + name;

                var newnode = await amazon.Nodes.GetChild(item.Id, name);
                if (newnode == null || newnode.status != AmazonNodeStatus.AVAILABLE)
                {
                    itemsTreeCache.AddItemOnly(FSItem.MakeNotExistingDummy(curpath));

                    // Log.Error("NonExisting path from server: " + itemPath);
                    return null;
                }

                item = FSItem.FromNode(curpath, newnode);
                itemsTreeCache.Add(item);
            }

            return item;
        }
    }
}
