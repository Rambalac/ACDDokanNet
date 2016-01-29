using Azi.Amazon.CloudDrive;
using Azi.Amazon.CloudDrive.JsonObjects;
using Azi.Tools;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Azi.ACDDokanNet
{
    public enum FailReason
    {
        ZeroLength,
        NoNode
    }

    public class UploadService : IDisposable
    {
        public class UploadInfo
        {
            public long length;
            public string id;
            public string path;
            public string parentId;
            public bool overwrite = false;

            public UploadInfo()
            {
            }

            public UploadInfo(FSItem item)
            {
                id = item.Id;
                path = item.Path;
                parentId = item.ParentIds.First();
                length = item.Length;
            }
        }

        public const string UploadFolder = "Upload";
        private const int ReuploadDelay = 5000;
        private string cachePath;

        private readonly SemaphoreSlim uploadLimitSemaphore;
        private readonly BlockingCollection<UploadInfo> uploads = new BlockingCollection<UploadInfo>();
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        private readonly AmazonDrive amazon;
        private readonly int uploadLimit;

        public delegate void OnUploadFinishedDelegate(UploadInfo item, AmazonNode amazonNode);

        public delegate void OnUploadFailedDelegate(UploadInfo item, FailReason reason);

        public OnUploadFinishedDelegate OnUploadFinished;
        public OnUploadFailedDelegate OnUploadFailed;
        public Action<FSItem> OnUploadResumed;

        public string CachePath
        {
            get
            {
                return cachePath;
            }

            set
            {
                var newpath = Path.Combine(value, UploadFolder);
                if (cachePath == newpath)
                {
                    return;
                }

                Log.Trace($"Cache path changed from {cachePath} to {newpath}");
                cachePath = newpath;
                Directory.CreateDirectory(cachePath);
                CheckOldUploads();
            }
        }

        private void CheckOldUploads()
        {
            var files = Directory.GetFiles(cachePath, "*.info");
            if (files.Length == 0)
            {
                return;
            }

            Log.Warn($"{files.Length} not uploaded files found. Resuming.");
            foreach (var info in files.Select(f => new FileInfo(f)).OrderBy(f => f.CreationTime))
            {
                var uploadinfo = JsonConvert.DeserializeObject<UploadInfo>(File.ReadAllText(info.FullName));
                var fileinfo = new FileInfo(Path.Combine(info.DirectoryName, Path.GetFileNameWithoutExtension(info.Name)));
                var item = FSItem.MakeUploading(uploadinfo.path, fileinfo.Name, uploadinfo.parentId, fileinfo.Length);
                OnUploadResumed(item);
                AddUpload(item);
            }
        }

        public UploadService(int limit, AmazonDrive amazon)
        {
            uploadLimit = limit;
            uploadLimitSemaphore = new SemaphoreSlim(limit);
            this.amazon = amazon;
        }

        private void WriteInfo(string path, UploadInfo info)
        {
            using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
            using (var writer = new StreamWriter(file))
            {
                writer.Write(JsonConvert.SerializeObject(info));
            }
        }

        private void AddUpload(FSItem item)
        {
            var info = new UploadInfo(item);

            var path = Path.Combine(cachePath, item.Id);
            WriteInfo(path + ".info", info);
            uploads.Add(info);
        }

        public void AddOverwrite(FSItem item)
        {
            var info = new UploadInfo(item)
            {
                overwrite = true
            };

            var path = Path.Combine(cachePath, item.Id);
            WriteInfo(path + ".info", info);
            uploads.Add(info);
        }

        private async Task Upload(UploadInfo item)
        {
            var path = Path.Combine(cachePath, item.id);
            try
            {
                if (item.length == 0)
                {
                    Log.Trace("Zero Length file: " + item.path);
                    File.Delete(path + ".info");
                    OnUploadFailed(item, FailReason.ZeroLength);
                    return;
                }

                Log.Trace("Started upload: " + item.path);
                AmazonNode node;
                if (!item.overwrite)
                    node = await amazon.Files.UploadNew(item.parentId, Path.GetFileName(item.path),
                        () => new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, true));
                else
                    node = await amazon.Files.Overwrite(item.id,
                        () => new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, true));
                File.Delete(path + ".info");
                if (node == null)
                {
                    OnUploadFailed(item, FailReason.NoNode);
                    throw new NullReferenceException("File node is null: " + item.path);
                }

                OnUploadFinished(item, node);
                Log.Trace("Finished upload: " + item.path + " id:" + node.id);
                return;
            }
            catch (HttpWebException ex)
            {
                if (ex.StatusCode == System.Net.HttpStatusCode.Conflict)
                {
                    Log.Error($"Upload conflict: {item.path}\r\n{ex}");
                    return;
                }

                throw;
            }
            catch (Exception ex)
            {
                Log.Error($"Upload failed: {item.path}\r\n{ex}");
            }
            finally
            {
                uploadLimitSemaphore.Release();
            }

            await Task.Delay(ReuploadDelay);
            uploads.Add(item);
        }

        public NewFileBlockWriter OpenNew(FSItem item)
        {
            var path = Path.Combine(cachePath, item.Id);
            var result = new NewFileBlockWriter(item, path);
            result.OnClose = () =>
              {
                  AddUpload(item);
              };

            return result;
        }

        public NewFileBlockWriter OpenTruncate(FSItem item)
        {
            var path = Path.Combine(cachePath, item.Id);
            var result = new NewFileBlockWriter(item, path);
            result.SetLength(0);
            result.OnClose = () =>
            {
                AddOverwrite(item);
            };

            return result;
        }

        private Task serviceTask;

        public void Stop()
        {
            if (serviceTask == null)
            {
                return;
            }

            cancellation.Cancel();
            try
            {
                serviceTask.Wait();
            }
            catch (AggregateException e)
            {
                e.Handle(ce => ce is TaskCanceledException);
            }

            serviceTask = null;
        }

        public void Start()
        {
            if (serviceTask != null)
            {
                return;
            }

            serviceTask = Task.Factory.StartNew(() => UploadTask(), cancellation.Token, TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach, TaskScheduler.Default);
        }

        private void UploadTask()
        {
            UploadInfo upload;
            while (uploads.TryTake(out upload, -1, cancellation.Token))
            {
                var uploadCopy = upload;
                if (!uploadLimitSemaphore.Wait(-1, cancellation.Token))
                {
                    return;
                }

                Task.Run(async () => await Upload(uploadCopy));
            }
        }

        public void WaitForUploadsFnish()
        {
            while (uploads.Count > 0)
            {
                Thread.Sleep(100);
            }

            for (int i = 0; i < uploadLimit; i++)
            {
                uploadLimitSemaphore.Wait();
            }

            return;
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    Stop();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~UploadService() {
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
