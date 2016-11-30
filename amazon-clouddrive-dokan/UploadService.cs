namespace Azi.Cloud.DokanNet
{
    using System;
    using System.Collections.Concurrent;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Common;
    using Newtonsoft.Json;
    using Tools;

    public enum FailReason
    {
        ZeroLength,
        NoResultNode,
        NoFolderNode,
        NoOverwriteNode,
        Conflict,
        Unexpected,
        Cancelled,
        FileNotFound
    }

    public class UploadService : IDisposable
    {
        public const string UploadFolder = "Upload";

        private const int ReuploadDelay = 5000;
        private readonly ConcurrentDictionary<string, UploadInfo> allUploads = new ConcurrentDictionary<string, UploadInfo>();
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        private readonly IHttpCloud cloud;
        private readonly BlockingCollection<UploadInfo> leftUploads = new BlockingCollection<UploadInfo>();
        private readonly int uploadLimit;
        private readonly SemaphoreSlim uploadLimitSemaphore;
        private string cachePath;
        private bool disposedValue; // To detect redundant calls
        private Task serviceTask;

        public UploadService(int limit, IHttpCloud cloud)
        {
            uploadLimit = limit;
            uploadLimitSemaphore = new SemaphoreSlim(limit);
            this.cloud = cloud;
        }

        public delegate Task OnUploadFailedDelegate(UploadInfo item, FailReason reason, string message);

        public delegate Task OnUploadFinishedDelegate(UploadInfo item, FSItem.Builder amazonNode);

        public delegate Task OnUploadProgressDelegate(UploadInfo item, long done);

        public string CachePath
        {
            get
            {
                return cachePath;
            }

            set
            {
                var newpath = Path.Combine(value, UploadFolder, cloud.Id);
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

        public Func<UploadInfo, Task> OnUploadAdded { get; set; }

        public OnUploadFailedDelegate OnUploadFailed { get; set; }

        public OnUploadFinishedDelegate OnUploadFinished { get; set; }

        public OnUploadProgressDelegate OnUploadProgress { get; set; }

        public async Task AddOverwrite(FSItem item)
        {
            var info = new UploadInfo(item)
            {
                Overwrite = true
            };

            Directory.CreateDirectory(cachePath);
            var path = Path.Combine(cachePath, item.Id);
            await WriteInfo(path + ".info", info);
            leftUploads.Add(info);
            allUploads.TryAdd(info.Id, info);
            await OnUploadAdded?.Invoke(info);
        }

        public async Task AddUpload(FSItem parent, string file)
        {
            var fileinfo = new FileInfo(file);
            var info = new UploadInfo
            {
                Id = Guid.NewGuid().ToString(),
                Length = fileinfo.Length,
                Path = Path.Combine(parent.Path, Path.GetFileName(file)),
                ParentId = parent.Id
            };

            var path = Path.Combine(cachePath, info.Id);
            SymbolicLink.CreateFile(file, path);

            await WriteInfo(path + ".info", info);
            leftUploads.Add(info);
            allUploads.TryAdd(info.Id, info);
            OnUploadAdded?.Invoke(info);
        }

        public void CancelUpload(string id)
        {
            UploadInfo outitem;
            if (allUploads.TryGetValue(id, out outitem))
            {
                outitem.Cancellation.Cancel();
                OnUploadFailed(outitem, FailReason.Cancelled, "Upload cancelled");
                CleanUpload(outitem.Path);
                allUploads.TryRemove(outitem.Id, out outitem);
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

        public NewFileBlockWriter OpenNew(FSItem item)
        {
            var path = Path.Combine(cachePath, item.Id);
            var result = new NewFileBlockWriter(item, path);
            result.OnClose = async () =>
              {
                  if (!result.Cancelled)
                  {
                      await AddUpload(item);
                  }
              };

            return result;
        }

        public NewFileBlockWriter OpenTruncate(FSItem item)
        {
            var path = Path.Combine(cachePath, item.Id);
            var result = new NewFileBlockWriter(item, path);
            result.SetLength(0);
            result.OnClose = async () =>
            {
                if (!result.Cancelled)
                {
                    await AddOverwrite(item);
                }
            };

            return result;
        }

        public void Start()
        {
            if (serviceTask != null)
            {
                return;
            }

            serviceTask = Task.Factory.StartNew(() => UploadTask(), cancellation.Token, TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach, TaskScheduler.Default);
        }

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

        public async Task WaitForUploadsFinish()
        {
            while (leftUploads.Count > 0)
            {
                await Task.Delay(100);
            }

            for (int i = 0; i < uploadLimit; i++)
            {
                await uploadLimitSemaphore.WaitAsync();
            }

            return;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    Stop();
                    cancellation.Dispose();
                    uploadLimitSemaphore.Dispose();
                    leftUploads.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.
                disposedValue = true;
            }
        }

        private async Task AddUpload(FSItem item)
        {
            var info = new UploadInfo(item);

            var path = Path.Combine(cachePath, item.Id);
            await WriteInfo(path + ".info", info);
            leftUploads.Add(info);
            allUploads.TryAdd(info.Id, info);
            await OnUploadAdded?.Invoke(info);
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
                try
                {
                    var id = Path.GetFileNameWithoutExtension(info.Name);
                    var uploadinfo = JsonConvert.DeserializeObject<UploadInfo>(File.ReadAllText(info.FullName));

                    try
                    {
                        var fileinfo = new FileInfo(Path.Combine(info.DirectoryName, id));
                        var item = FSItem.MakeUploading(uploadinfo.Path, id, uploadinfo.ParentId, fileinfo.Length);
                        leftUploads.Add(uploadinfo);
                        allUploads.TryAdd(id, uploadinfo);
                        OnUploadAdded?.Invoke(uploadinfo);
                    }
                    catch (FileNotFoundException)
                    {
                        Log.Error("Cached upload file not found: " + uploadinfo.Path + " id:" + id);
                        CleanUpload(Path.Combine(cachePath, id));
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex);
                }
            }
        }

        private void CleanUpload(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception)
            {
                Log.Warn("CleanUpload did not find the file, probably successfully moved");
            }

            try
            {
                File.Delete(path + ".info");
            }
            catch (Exception)
            {
                Log.Warn("CleanUpload did not find the info file, probably successfully moved");
            }
        }

        private async Task Upload(UploadInfo item)
        {
            var path = Path.Combine(cachePath, item.Id);
            try
            {
                try
                {
                    if (item.Length == 0)
                    {
                        Log.Trace("Zero Length file: " + item.Path);
                        await OnUploadFailed(item, FailReason.ZeroLength, null);
                        CleanUpload(path);
                        item.Dispose();
                        return;
                    }

                    Log.Trace("Started upload: " + item.Path);
                    FSItem.Builder node;
                    if (!item.Overwrite)
                    {
                        var checkparent = await cloud.Nodes.GetNode(item.ParentId);
                        if (checkparent == null || !checkparent.IsDir)
                        {
                            Log.Error("Folder does not exist to upload file: " + item.Path);
                            await OnUploadFailed(item, FailReason.NoFolderNode, "Parent folder is missing");
                            CleanUpload(path);
                            item.Dispose();
                            return;
                        }

                        var checknode = await cloud.Nodes.GetChild(item.ParentId, Path.GetFileName(item.Path));
                        if (checknode != null)
                        {
                            Log.Warn("File with such name already exists and Upload is New: " + item.Path);
                            await OnUploadFailed(item, FailReason.Conflict, "File already exists");
                            CleanUpload(path);
                            item.Dispose();
                            return;
                        }

                        node = await cloud.Files.UploadNew(
                            item.ParentId,
                            Path.GetFileName(item.Path),
                            () => new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true),
                            (p) => UploadProgress(item, p));
                    }
                    else
                    {
                        var checknode = await cloud.Nodes.GetNode(item.Id);
                        if (checknode == null)
                        {
                            Log.Error("File does not exist to be overwritten: " + item.Path);
                            File.Delete(path + ".info");
                            await OnUploadFailed(item, FailReason.NoOverwriteNode, "No file to overwrite");
                            CleanUpload(path);
                            item.Dispose();

                            return;
                        }

                        node = await cloud.Files.Overwrite(
                            item.Id,
                            () => new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true),
                            (p) => UploadProgress(item, p));
                    }

                    if (node == null)
                    {
                        throw new NullReferenceException("File node is null: " + item.Path);
                    }

                    CleanUpload(path);

                    node.ParentPath = Path.GetDirectoryName(item.Path);

                    Log.Trace("Finished upload: " + item.Path + " id:" + node.Id);
                    await OnUploadFinished(item, node);
                    item.Dispose();
                    return;
                }
                catch (FileNotFoundException ex)
                {
                    Log.Error($"Upload error upload file not found: {item.Path}\r\n{ex}");
                    await OnUploadFailed(item, FailReason.FileNotFound, "Cached upload file is not found");

                    CleanUpload(path);
                    item.Dispose();

                    return;
                }
                catch (OperationCanceledException)
                {
                    if (item.Cancellation.IsCancellationRequested)
                    {
                        Log.Info("Upload canceled");

                        await OnUploadFailed(item, FailReason.Cancelled, "Upload cancelled");
                        CleanUpload(path);
                        item.Dispose();
                    }

                    return;
                }
                catch (CloudException ex)
                {
                    if (ex.Error == System.Net.HttpStatusCode.Conflict)
                    {
                        var node = await cloud.Nodes.GetChild(item.ParentId, Path.GetFileName(item.Path));
                        if (node != null)
                        {
                            Log.Warn($"Upload finished with conflict and file does exist: {item.Path}\r\n{ex}");
                            await OnUploadFinished(item, node);
                            CleanUpload(path);
                            item.Dispose();
                            return;
                        }

                        Log.Error($"Upload conflict but no file: {item.Path}\r\n{ex}");
                        await OnUploadFailed(item, FailReason.Unexpected, "Upload conflict but there is no file in the same place");
                    }
                    else if (ex.Error == System.Net.HttpStatusCode.NotFound)
                    {
                        Log.Error($"Upload error Folder Not Found: {item.Path}\r\n{ex}");
                        await OnUploadFailed(item, FailReason.NoFolderNode, "Folder node for new file is not found");

                        CleanUpload(path);
                        item.Dispose();

                        return;
                    }
                    else if (ex.Error == System.Net.HttpStatusCode.GatewayTimeout)
                    {
                        Log.Warn($"Gateway timeout happened: {item.Path}\r\nWait 30 seconds to check if file was really uploaded");

                        await Task.Delay(30000);
                        var node = await cloud.Nodes.GetChild(item.ParentId, Path.GetFileName(item.Path));
                        if (node != null)
                        {
                            Log.Warn($"Gateway timeout happened: {item.Path}\r\nBut after 30 seconds file did appear");
                            File.Delete(path + ".info");

                            node.ParentPath = Path.GetDirectoryName(item.Path);

                            Log.Trace($"Finished upload: {item.Path} id:{node.Id}");
                            await OnUploadFinished(item, node);
                            item.Dispose();
                            return;
                        }

                        Log.Error($"Gateway timeout happened: {item.Path}\r\nBut after 30 seconds file still did not appear.");
                        await OnUploadFailed(item, FailReason.Unexpected, $"Gateway timeout happened but after 30 seconds file still did not appear");
                    }
                    else
                    {
                        Log.Error($"Cloud exception: {item.Path}");
                        await OnUploadFailed(item, FailReason.Unexpected, $"Unexpected Error. Upload will retry.\r\n{ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Upload failed: {item.Path}\r\n{ex}");
                await OnUploadFailed(item, FailReason.Unexpected, $"Unexpected Error. Upload will retry.\r\n{ex.Message}");
            }
            finally
            {
                uploadLimitSemaphore.Release();
                UploadInfo outItem;
                allUploads.TryRemove(item.Id, out outItem);
            }

            await Task.Delay(ReuploadDelay);
            Log.Warn($"Repeat upload: {item.Path}");
            allUploads.TryAdd(item.Id, item);
            leftUploads.Add(item);
        }

        private void UploadProgress(UploadInfo item, long p)
        {
            OnUploadProgress?.Invoke(item, p);
            cancellation.Token.ThrowIfCancellationRequested();
            item.Cancellation.Token.ThrowIfCancellationRequested();
        }

        private void UploadTask()
        {
            try
            {
                UploadInfo upload;
                while (leftUploads.TryTake(out upload, -1, cancellation.Token))
                {
                    var uploadCopy = upload;
                    if (!uploadLimitSemaphore.Wait(-1, cancellation.Token))
                    {
                        return;
                    }

                    Task.Factory.StartNew(
                        async () =>
                        {
                            try
                            {
                                await Upload(uploadCopy);
                            }
                            catch (Exception e)
                            {
                                Log.Error(e);
                            }
                        },
                        TaskCreationOptions.LongRunning);
                }
            }
            catch (OperationCanceledException)
            {
                Log.Info("Upload service stopped");
            }
        }

        private async Task WriteInfo(string path, UploadInfo info)
        {
            using (var writer = new StreamWriter(new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, true)))
            {
                await writer.WriteAsync(JsonConvert.SerializeObject(info));
            }
        }
    }
}