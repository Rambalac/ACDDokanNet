using Azi.Amazon.CloudDrive;
using Azi.Tools;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Diagnostics;
using System.Net.Http.Headers;

namespace Azi.ACDDokanNet
{
    public class SmallFilesCache
    {
        private class CacheEntry : IDisposable
        {
            public string Id;

            private readonly ReaderWriterLockSlim lk = new ReaderWriterLockSlim();
            private DateTime accessTime;

            public DateTime AccessTime
            {
                get
                {
                    lk.EnterReadLock();
                    try
                    {
                        return accessTime;
                    }
                    finally
                    {
                        lk.ExitReadLock();
                    }
                }

                set
                {
                    lk.EnterWriteLock();
                    try
                    {
                        accessTime = value;
                    }
                    finally
                    {
                        lk.ExitWriteLock();
                    }
                }
            }

            public long Length;

            public void Dispose()
            {
                ((IDisposable)lk).Dispose();
            }
        }

        private ConcurrentDictionary<string, CacheEntry> access = new ConcurrentDictionary<string, CacheEntry>(10, 1000);

        private static ConcurrentDictionary<AmazonDrive, SmallFilesCache> Instances = new ConcurrentDictionary<AmazonDrive, SmallFilesCache>(10, 3);
        private static ConcurrentDictionary<string, Downloader> Downloaders = new ConcurrentDictionary<string, Downloader>(10, 3);

        private readonly AmazonDrive Amazon;
        private static string cachePath = null;

        public string CachePath
        {
            get
            {
                return cachePath;
            }

            set
            {
                if (cachePath == value)
                {
                    return;
                }

                bool wasNull = cachePath == null;
                try
                {
                    if (cachePath != null)
                    {
                        Directory.Delete(cachePath, true);
                    }
                }
                catch (Exception)
                {
                    Log.Warn("Can not delete old cache: " + cachePath);
                }

                cachePath = Path.Combine(value, "SmallFiles");
                Directory.CreateDirectory(cachePath);
                if (wasNull)
                {
                    Task.Run(() => RecalculateTotalSize());
                }
            }
        }

        public long CacheSize { get; internal set; }

        private Downloader StartDownload(FSItem item, string path)
        {
            try
            {
                var downloader = new Downloader(item, path);
                if (!Downloaders.TryAdd(item.Path, downloader))
                {
                    return Downloaders[item.Path];
                }

                var writer = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
                if (writer.Length == item.Length)
                {
                    writer.Close();
                    Downloader removed;
                    Downloaders.TryRemove(item.Path, out removed);
                    return Downloader.CreateCompleted(item, path, item.Length);
                }

                if (writer.Length > 0)
                {
                    Log.Warn($"File was not totally downloaded before. Should be {item.Length} but was {writer.Length}: {path}");
                }

                downloader.Downloaded = writer.Length;
                downloader.Task = Task.Run(async () => await Download(item, writer, downloader));
                return downloader;
            }
            catch (IOException e)
            {
                Log.Trace("File is already downloading: " + item.Id + "\r\n" + e);
                return Downloaders[item.Path];
            }
        }

        public Action<string> OnDownloadStarted;
        public Action<string> OnDownloaded;
        public Action<string> OnDownloadFailed;

        private async Task Download(FSItem item, Stream writer, Downloader downloader)
        {
            Log.Trace("Started download: " + item.Id);
            var start = Stopwatch.StartNew();
            var buf = new byte[4096];
            using (writer)
                try
                {
                    OnDownloadStarted?.Invoke(item.Id);
                    while (writer.Length < item.Length)
                    {
                        await Amazon.Files.Download(item.Id, fileOffset: writer.Length, streammer: async (response) =>
                        {
                            var partial = response.StatusCode == System.Net.HttpStatusCode.PartialContent;
                            ContentRangeHeaderValue contentRange = null;
                            if (partial)
                            {
                                contentRange = response.Headers.GetContentRange();
                                if (contentRange.From != writer.Length)
                                {
                                    throw new InvalidOperationException("Content range does not match request");
                                }
                            }
                            using (var stream = response.GetResponseStream())
                            {
                                int red = 0;
                                do
                                {
                                    red = await stream.ReadAsync(buf, 0, buf.Length);
                                    if (writer.Length == 0)
                                    {
                                        Log.Trace("Got first part: " + item.Id + " in " + start.ElapsedMilliseconds);
                                    }

                                    writer.Write(buf, 0, red);
                                    downloader.Downloaded = writer.Length;
                                }
                                while (red > 0);
                            }
                        });
                        if (writer.Length < item.Length)
                        {
                            await Task.Delay(500);
                        }
                    }

                    Log.Trace("Finished download: " + item.Id);
                    OnDownloaded?.Invoke(item.Id);

                    access.TryAdd(item.Id, new CacheEntry { Id = item.Id, AccessTime = DateTime.UtcNow, Length = item.Length });
                    TotalSize += item.Length;
                    if (TotalSize > CacheSize)
                    {
                        StartClear(TotalSize - CacheSize);
                    }
                }
                catch (Exception ex)
                {
                    OnDownloadFailed?.Invoke(item.Id);
                    Log.Error($"Download failed: {item.Id}\r\n{ex}");
                }
                finally
                {
                    Downloader remove;
                    Downloaders.TryRemove(item.Path, out remove);
                }
        }

        private long totalSize = 0;

        public long TotalSize
        {
            get
            {
                return Interlocked.Read(ref totalSize);
            }

            set
            {
                Interlocked.Exchange(ref totalSize, value);
            }
        }

        private bool cleaning = false;

        internal void StartClear(long size)
        {
            if (cleaning)
            {
                return;
            }

            cleaning = true;
            Task.Run(() =>
            {
                try
                {
                    long deleted = 0;
                    foreach (var file in access.Values.OrderBy(f => f.AccessTime).TakeWhile(f =>
                    {
                        size -= f.Length;
                        return size > 0;
                    }).ToList())
                    {
                        try
                        {
                            var path = Path.Combine(cachePath, file.Id);
                            var info = new FileInfo(path);
                            File.Delete(path);
                            deleted += info.Length;
                            CacheEntry remove;
                            access.TryRemove(file.Id, out remove);
                        }
                        catch (IOException)
                        {
                            // Skip if failed
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex);
                        }
                    }

                    TotalSize -= deleted;
                }
                finally
                {
                    cleaning = false;
                }
            });
        }

        public Task Clear()
        {
            var oldcachePath = cachePath;
            return Task.Run(() =>
            {
                int failed = 0;
                try
                {
                    foreach (var file in Directory.GetFiles(oldcachePath).ToList())
                    {
                        try
                        {
                            File.Delete(file);
                        }
                        catch (IOException)
                        {
                            failed++;
                        }
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    Log.Error("Cannot access folder: " + oldcachePath);
                    return;
                }
                RecalculateTotalSize();

                if (failed > 0)
                {
                    throw new InvalidOperationException("Can not delete all cached files. Some files are still in use.");
                }
            });
        }

        private void RecalculateTotalSize()
        {
            if (cachePath == null)
            {
                return;
            }

            long t = 0;
            var newaccess = new ConcurrentDictionary<string, CacheEntry>(10, 1000);
            try
            {
                foreach (var file in Directory.GetFiles(cachePath))
                {
                    try
                    {
                        var fi = new FileInfo(file);
                        t += fi.Length;
                        var id = Path.GetFileName(file);
                        newaccess.TryAdd(id, new CacheEntry { Id = id, AccessTime = fi.LastAccessTimeUtc });
                    }
                    catch (Exception ex)
                    {
                        Log.Error("Cannot access folder: " + cachePath);
                        Log.Error(ex);
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                return;
            }

            access = newaccess;
            TotalSize = t;
        }

        internal void Delete(FSItem item)
        {
            var path = Path.Combine(cachePath, item.Id);
            File.Delete(path);
        }

        internal SmallFileBlockReaderWriter OpenReadWrite(FSItem item)
        {
            var path = Path.Combine(cachePath, item.Id);
            var downloader = StartDownload(item, path);

            CacheEntry entry;
            if (access.TryGetValue(item.Id, out entry))
            {
                entry.AccessTime = DateTime.UtcNow;
            }

            Log.Trace("Opened ReadWrite cached: " + item.Id);
            return new SmallFileBlockReaderWriter(downloader);
        }

        internal void AddExisting(FSItem item)
        {
            access.TryAdd(item.Id, new CacheEntry { Id = item.Id, AccessTime = DateTime.UtcNow });
        }

        public SmallFilesCache(AmazonDrive a)
        {
            Amazon = a;
        }

        public IBlockStream OpenReadWithDownload(FSItem item)
        {
            var path = Path.Combine(cachePath, item.Id);
            StartDownload(item, path);

            return OpenReadCachedOnly(item);
        }

        public FileBlockReader OpenReadCachedOnly(FSItem item)
        {
            var path = Path.Combine(cachePath, item.Id);
            if (!File.Exists(path))
            {
                return null;
            }

            CacheEntry entry;
            if (access.TryGetValue(item.Id, out entry))
            {
                entry.AccessTime = DateTime.UtcNow;
            }

            Log.Trace("Opened cached: " + item.Id);
            return FileBlockReader.Open(path, item.Length);
        }
    }
}