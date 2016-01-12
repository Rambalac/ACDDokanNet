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
        const int reuploadDelay = 5000;
        private string cachePath;


        readonly SemaphoreSlim uploadLimitSemaphore;
        readonly BlockingCollection<UploadInfo> uploads = new BlockingCollection<UploadInfo>();
        readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        readonly AmazonDrive amazon;
        readonly int uploadLimit;

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
                if (cachePath == newpath) return;
                Log.Trace($"Cache path changed from {cachePath} to {newpath}");
                cachePath = newpath;
                Directory.CreateDirectory(cachePath);
                CheckOldUploads();
            }
        }

        private void CheckOldUploads()
        {
            var files = Directory.GetFiles(cachePath, "*.info");
            if (files.Length == 0) return;
            Log.Warn($"{files.Length} not uploaded files found. Resuming.");
            foreach (var file in files)
            {
                var uploadinfo = JsonConvert.DeserializeObject<UploadInfo>(File.ReadAllText(file));
                var fileinfo = new FileInfo(file);
                var item = FSItem.MakeUploading(uploadinfo.path, Path.GetFileNameWithoutExtension(file), uploadinfo.parentId, fileinfo.Length);
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

        private void AddUpload(FSItem item)
        {
            var info = new UploadInfo(item);

            uploads.Add(info);
            var path = Path.Combine(cachePath, item.Id);
            File.WriteAllText(path + ".info", JsonConvert.SerializeObject(info));
        }

        public void AddOverwrite(FSItem item)
        {
            var info = new UploadInfo(item)
            {
                overwrite = true
            };

            uploads.Add(info);
            var path = Path.Combine(cachePath, item.Id);
            File.WriteAllText(path + ".info", JsonConvert.SerializeObject(info));
        }


        private string CalcMD5(string path)
        {
            using (var md5 = MD5.Create())
            {
                using (var stream = File.OpenRead(CachePath))
                {
                    var bytes = md5.ComputeHash(stream);
                    return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
                }
            }
        }



        private async Task Upload(UploadInfo item)
        {
            try
            {
                var path = Path.Combine(cachePath, item.id);
                if (item.length == 0)
                {
                    Log.Warn("Zero Length file: " + item.path);
                    OnUploadFailed(item, FailReason.ZeroLength);
                    return;
                }

                Log.Trace("Started upload: " + item.path);
                AmazonNode node;
                if (!item.overwrite)
                    node = await amazon.Files.UploadNew(item.parentId, Path.GetFileName(item.path),
                        () => new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true));
                else
                    node = await amazon.Files.Overwrite(item.id,
                        () => new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true));
                if (node != null)
                {
                    File.Delete(path + ".info");
                    OnUploadFinished(item, node);
                    Log.Trace("Finished upload: " + item.path + " id:" + node.id);
                    return;
                }
                else
                {
                    OnUploadFailed(item, FailReason.NoNode);
                    throw new NullReferenceException("File node is null: " + item.path);
                }
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
            await Task.Delay(reuploadDelay);
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
            result.OnClose = () =>
            {
                AddOverwrite(item);
            };

            return result;
        }

        Task serviceTask;

        public void Stop()
        {
            if (serviceTask == null) return;
            cancellation.Cancel();
            serviceTask.Wait();
            serviceTask = null;
        }

        public void Start()
        {
            if (serviceTask != null) return;
            serviceTask = Task.Factory.StartNew(() => UploadTask(), cancellation.Token, TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach, TaskScheduler.Default);
        }

        private void UploadTask()
        {
            UploadInfo upload;
            while (uploads.TryTake(out upload, -1, cancellation.Token))
            {
                var uploadCopy = upload;
                if (!uploadLimitSemaphore.Wait(-1, cancellation.Token)) return;
                Task.Run(async () => await Upload(uploadCopy));
            }

        }

        public void WaitForUploadsFnish()
        {
            while (uploads.Count > 0) Thread.Sleep(100);
            for (int i = 0; i < uploadLimit; i++) uploadLimitSemaphore.Wait();
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
