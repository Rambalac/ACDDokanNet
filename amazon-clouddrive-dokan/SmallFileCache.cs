using Azi.Amazon.CloudDrive;
using Azi.Tools;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Diagnostics;
using System.Net.Http.Headers;

namespace Azi.ACDDokanNet
{


    public class SmallFileCache
    {
        class CacheEntry : IDisposable
        {
            public string Id;

            readonly ReaderWriterLockSlim lk = new ReaderWriterLockSlim();
            DateTime accessTime;

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

        ConcurrentDictionary<string, CacheEntry> access = new ConcurrentDictionary<string, CacheEntry>(10, 1000);

        static ConcurrentDictionary<AmazonDrive, SmallFileCache> Instances = new ConcurrentDictionary<AmazonDrive, SmallFileCache>(10, 3);

        readonly AmazonDrive Amazon;
        private static string cachePath = null;
        public string CachePath
        {
            get { return cachePath; }
            set
            {
                if (cachePath == value) return;
                bool wasNull = (cachePath == null);
                try
                {
                    if (cachePath != null)
                        Directory.Delete(cachePath, true);
                }
                catch (Exception)
                {
                    Log.Warn("Can not delete old cache: " + cachePath);
                }
                cachePath = Path.Combine(value, "SmallFiles");
                if (wasNull) Task.Run(() => RecalculateTotalSize());

                Directory.CreateDirectory(cachePath);
            }
        }

        public long CacheSize { get; internal set; }

        public void StartDownload(FSItem node, string path)
        {
            try
            {
                var writer = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
                if (writer.Length == node.Length)
                {
                    writer.Close();
                    return;
                }
                if (writer.Length>0)
                {
                    Log.Warn($"File was not totally downloaded. Should be {node.Length} but was {writer.Length}: {path}");
                }
                Task.Run(async () => await Download(node, writer));
            }
            catch (IOException e)
            {
                Log.Trace("File is already downloading: " + node.Id + "\r\n" + e);
            }
        }

        public Action<string> OnDownloadStarted;
        public Action<string> OnDownloaded;
        public Action<string> OnDownloadFailed;

        private async Task Download(FSItem node, Stream writer)
        {
            Log.Trace("Started download: " + node.Id);
            var start = Stopwatch.StartNew();
            var buf = new byte[4096];
            using (writer)
                try
                {
                    OnDownloadStarted?.Invoke(node.Id);
                    while (writer.Length < node.Length)
                    {
                        await Amazon.Files.Download(node.Id, fileOffset: writer.Length, streammer: async (response) =>
                        {
                            var partial = response.StatusCode == System.Net.HttpStatusCode.PartialContent;
                            ContentRangeHeaderValue contentRange = null;
                            if (partial)
                            {
                                contentRange = response.Headers.GetContentRange();
                                if (contentRange.From != writer.Length) throw new InvalidOperationException("Content range does not match request");
                            }
                            using (var stream = response.GetResponseStream())
                            {
                                int red = 0;
                                do
                                {
                                    red = await stream.ReadAsync(buf, 0, buf.Length);
                                    if (writer.Length == 0) Log.Trace("Got first part: " + node.Id + " in " + start.ElapsedMilliseconds);
                                    writer.Write(buf, 0, red);
                                } while (red > 0);
                            }
                        });
                        if (writer.Length < node.Length) await Task.Delay(500);
                    }
                    Log.Trace("Finished download: " + node.Id);
                    OnDownloaded?.Invoke(node.Id);

                    access.TryAdd(node.Id, new CacheEntry { Id = node.Id, AccessTime = DateTime.UtcNow, Length = node.Length });
                    TotalSize += node.Length;
                    if (TotalSize > CacheSize) StartClear(TotalSize - CacheSize);
                }
                catch (Exception ex)
                {
                    OnDownloadFailed?.Invoke(node.Id);
                    Log.Error($"Download failed: {node.Id}\r\n{ex}");
                }
        }

        long totalSize = 0;
        public long TotalSize
        {
            get { return Interlocked.Read(ref totalSize); }
            set
            {
                Interlocked.Exchange(ref totalSize, value);
            }
        }


        bool cleaning = false;
        internal void StartClear(long size)
        {
            if (cleaning) return;
            cleaning = true;
            Task.Run(() =>
            {
                try
                {
                    long deleted = 0;
                    foreach (var file in access.Values.OrderBy(f => f.AccessTime).TakeWhile(f => { size -= f.Length; return size > 0; }).ToList())
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
                            //Skip if failed
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
                RecalculateTotalSize();

                if (failed > 0) throw new InvalidOperationException("Can not delete all cached files. Some files are still in use.");
            });
        }

        private void RecalculateTotalSize()
        {
            if (cachePath == null) return;
            long t = 0;
            var newaccess = new ConcurrentDictionary<string, CacheEntry>(10, 1000);
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
                    Log.Error(ex);
                }
            }
            access = newaccess;
            TotalSize = t;
        }

        internal void AddExisting(FSItem item)
        {
            access.TryAdd(item.Id, new CacheEntry { Id = item.Id, AccessTime = DateTime.UtcNow });
        }

        public SmallFileCache(AmazonDrive a)
        {
            Amazon = a;
        }

        public IBlockStream OpenReadWithDownload(FSItem node)
        {
            var path = Path.Combine(cachePath, node.Id);
            StartDownload(node, path);

            return OpenReadCachedOnly(node);
        }

        public IBlockStream OpenReadCachedOnly(FSItem node)
        {
            var path = Path.Combine(cachePath, node.Id);

            CacheEntry entry;
            if (access.TryGetValue(node.Id, out entry)) entry.AccessTime = DateTime.UtcNow;

            Log.Trace("Opened cached: " + node.Id);
            return FileBlockReader.Open(path, node.Length);
        }
    }
}