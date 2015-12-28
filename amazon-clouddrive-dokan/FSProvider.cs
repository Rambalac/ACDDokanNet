using Azi.Amazon.CloudDrive;
using Azi.Amazon.CloudDrive.JsonObjects;
using Azi.Tools;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Tools;

namespace Azi.ACDDokanNet
{
    public class FSProvider: IDisposable
    {
        static readonly string[] fsItemKinds = { "FILE", "FOLDER" };

        internal readonly AmazonDrive Amazon;
        readonly NodeTreeCache nodeTreeCache = new NodeTreeCache();
        internal SmallFileCache SmallFileCache { get; private set; }

        public long SmallFileSizeLimit { get; set; } = 20 * 1024 * 1024;

        string cachePath;
        public string CachePath
        {
            get
            {
                return cachePath;
            }
            set
            {
                cachePath = value;
                SmallFileCache.CachePath = value;
                Directory.CreateDirectory(Path.Combine(CachePath, NewFileBlockWriter.UploadFolder));
            }
        }

        public FSProvider(AmazonDrive amazon)
        {
            this.Amazon = amazon;
            SmallFileCache = new SmallFileCache(amazon);
            SmallFileCache.OnDownloadStarted = (id) =>
            {
                Interlocked.Increment(ref downloadingCount);
                OnStatisticsUpdated?.Invoke(downloadingCount, uploadingCount);
            };
            SmallFileCache.OnDownloaded = (id) =>
            {
                Interlocked.Decrement(ref downloadingCount);
                OnStatisticsUpdated?.Invoke(downloadingCount, uploadingCount);
            };
            SmallFileCache.OnDownloadFailed = (id) =>
            {
                Interlocked.Decrement(ref downloadingCount);
                OnStatisticsUpdated?.Invoke(downloadingCount, uploadingCount);
            };

        }

        public delegate void StatisticsUpdated(int downloading, int uploading);
        public StatisticsUpdated OnStatisticsUpdated { get; set; }

        public int downloadingCount = 0;
        public int uploadingCount = 0;
        public long AvailableFreeSpace => Amazon.Account.GetQuota().Result.available;
        public long TotalSize => Amazon.Account.GetQuota().Result.quota;
        public long TotalFreeSpace => Amazon.Account.GetQuota().Result.available;

        public long TotalUsedSpace => Amazon.Account.GetUsage().Result.total.total.bytes;

        public string VolumeName => FileSystemName;

        public string FileSystemName => "Amazon Cloud Drive";

        public long SmallFileCacheSize
        {
            get
            {
                return SmallFileCache.CacheSize;
            }
            set
            {
                SmallFileCache.CacheSize = value;
            }
        }

        public void DeleteFile(string filePath)
        {
            var node = GetItem(filePath);
            if (node != null)
            {
                if (node.IsDir) throw new InvalidOperationException("Not file");
                try { Amazon.Nodes.Delete(node.Id).Wait(); }
                catch (AggregateException ex)
                {
                    var webex = ex.InnerException as HttpWebException;
                    if (webex == null || (webex.StatusCode != HttpStatusCode.NotFound && webex.StatusCode != HttpStatusCode.Conflict)) throw ex.InnerException;
                }

                nodeTreeCache.DeleteFile(filePath);
            }
        }

        public void ClearSmallFilesCache()
        {
            var task = SmallFileCache.Clear();
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
            var node = Amazon.Nodes.CreateFolder(dirNode.Id, name).Result;

            nodeTreeCache.Add(FSItem.FromNode(filePath, node));
        }

        public IBlockStream OpenFile(string filePath, FileMode mode, FileAccess fileAccess, FileShare share, FileOptions options)
        {
            if (fileAccess == FileAccess.ReadWrite) return null;
            var item = GetItem(filePath);
            if (fileAccess == FileAccess.Read)
            {
                if (item == null) return null;

                if (!item.IsFake)
                {
                    if (item.Length < SmallFileSizeLimit)
                        return SmallFileCache.OpenRead(item);

                    Interlocked.Increment(ref downloadingCount);
                    var buffered = new BufferedAmazonBlockReader(item, Amazon);
                    buffered.OnClose = () =>
                      {
                          Interlocked.Decrement(ref downloadingCount);
                          OnStatisticsUpdated?.Invoke(downloadingCount, uploadingCount);
                      };

                    return buffered;
                }

                return new FileBlockReader(Path.Combine(CachePath, NewFileBlockWriter.UploadFolder, item.Id), item.Length);
            }

            if (mode == FileMode.CreateNew || ((mode == FileMode.Create && mode == FileMode.OpenOrCreate) && (item == null || item.Length == 0)))
            {
                var dir = Path.GetDirectoryName(filePath);
                var name = Path.GetFileName(filePath);
                var dirNode = GetItem(dir);
                var uploader = new NewFileBlockWriter(dirNode, item, filePath, this);
                item = uploader.Node;
                nodeTreeCache.Add(item);
                Interlocked.Increment(ref uploadingCount);
                OnStatisticsUpdated?.Invoke(downloadingCount, uploadingCount);

                uploader.OnUpload = (parent, newnode) =>
                  {
                      Interlocked.Decrement(ref uploadingCount);
                      OnStatisticsUpdated?.Invoke(downloadingCount, uploadingCount);

                      var newitemPath = Path.Combine(SmallFileCache.CachePath, newnode.id);
                      if (!File.Exists(newitemPath))
                          File.Move(uploader.CachedPath, newitemPath);

                      var newitem = new FSItem(item);
                      newitem.Id = newnode.id;
                      newitem.Length = newnode.Length;
                      newitem.NotFake();
                      nodeTreeCache.Update(newitem);
                  };
                uploader.OnUploadFailed = (parent, path, id) =>
                  {
                      Interlocked.Decrement(ref uploadingCount);
                      OnStatisticsUpdated?.Invoke(downloadingCount, uploadingCount);

                      nodeTreeCache.DeleteFile(path);
                  };

                return uploader;
            }

            return null;
        }

        public void DeleteDir(string filePath)
        {
            var node = GetItem(filePath);
            if (node != null)
            {
                if (!node.IsDir) throw new InvalidOperationException("Not dir");
                Amazon.Nodes.Delete(node.Id).Wait();
                nodeTreeCache.DeleteDir(filePath);
            }
        }

        public async Task<IList<FSItem>> GetDirItems(string folderPath)
        {
            var cached = nodeTreeCache.GetDir(folderPath);
            if (cached != null) return await Task.WhenAll(cached.Select(i => FetchNode(i)));

            var folderNode = GetItem(folderPath);
            var nodes = await Amazon.Nodes.GetChildren(folderNode?.Id);
            var items = new List<FSItem>(nodes.Count);
            var curdir = folderPath;
            if (curdir == "\\") curdir = "";
            foreach (var node in nodes.Where(n => fsItemKinds.Contains(n.kind)))
            {
                var path = curdir + "\\" + node.name;
                items.Add(FSItem.FromNode(path, node));
            }

            nodeTreeCache.AddDirItems(folderPath, items);
            return items;
        }

        public FSItem GetItem(string itemPath)
        {
            return FetchNode(itemPath).Result;
        }

        private async Task<FSItem> FetchNode(string itemPath)
        {
            if (itemPath == "\\" || itemPath == "") return FSItem.FromRoot(await Amazon.Nodes.GetRoot());
            var cached = nodeTreeCache.GetNode(itemPath);
            if (cached != null) return cached;

            var folders = new LinkedList<string>();
            var curpath = itemPath;
            FSItem item = null;
            do
            {
                folders.AddFirst(Path.GetFileName(curpath));
                curpath = Path.GetDirectoryName(curpath);
                if (curpath == "\\" || string.IsNullOrEmpty(curpath)) break;
                item = nodeTreeCache.GetNode(curpath);
            } while (item == null);
            if (item == null) item = FSItem.FromRoot(await Amazon.Nodes.GetRoot());
            foreach (var name in folders)
            {
                var newnode = await Amazon.Nodes.GetChild(item.Id, name);
                if (newnode == null || newnode.status != AmazonNodeStatus.AVAILABLE) return null;

                if (curpath == "\\") curpath = "";
                curpath = curpath + "\\" + name;
                item = FSItem.FromNode(curpath, newnode);
                nodeTreeCache.Add(item);
            }
            return item;
        }

        public void MoveFile(string oldPath, string newPath, bool replace)
        {
            if (oldPath == newPath) return;

            var oldDir = Path.GetDirectoryName(oldPath);
            var oldName = Path.GetFileName(oldPath);
            var newDir = Path.GetDirectoryName(newPath);
            var newName = Path.GetFileName(newPath);

            var node = GetItem(oldPath);
            if (oldName != newName)
            {
                node = FSItem.FromNode(Path.Combine(oldDir, newName), Amazon.Nodes.Rename(node.Id, newName).Result);
                if (node == null) throw new InvalidOperationException("Can not rename");
            }

            if (oldDir != newDir)
            {
                var oldDirNodeTask = FetchNode(oldDir);
                var newDirNodeTask = FetchNode(newDir);
                Task.WaitAll(oldDirNodeTask, newDirNodeTask);
                node = FSItem.FromNode(newPath, Amazon.Nodes.Move(node.Id, oldDirNodeTask.Result.Id, newDirNodeTask.Result.Id).Result);
                if (node == null) throw new InvalidOperationException("Can not move");
            }

            if (node.IsDir)
                nodeTreeCache.MoveDir(oldPath, node);
            else
                nodeTreeCache.MoveFile(oldPath, node);
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    nodeTreeCache.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
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
        #endregion


    }
}
