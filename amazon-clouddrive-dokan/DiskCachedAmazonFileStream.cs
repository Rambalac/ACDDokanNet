using Azi.Amazon.CloudDrive;
using Azi.Amazon.CloudDrive.Json;
using Azi.Tools;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Diagnostics;

namespace Azi.ACDDokanNet
{
    public interface IBlockReader
    {
        int Read(long position, byte[] buffer, int offset, int count, int timeout = 1000);
        void Close();
    }

    public class FileBlockReader : IBlockReader
    {
        readonly Stream file;
        readonly string path;
        public FileBlockReader(string path)
        {
            this.path = path;
            file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        }

        public void Close()
        {
            Log.Trace(Path.GetFileName(path));

            file.Close();
        }

        public int Read(long position, byte[] buffer, int offset, int count, int timeout)
        {
            lock (file)
            {
                file.Position = position;
                return file.Read(buffer, offset, count);
            }
        }

    }

    public class Downloader
    {
        public class Reader : IBlockReader
        {
            Downloader downloader;

            internal Reader(Downloader downloader)
            {
                this.downloader = downloader;
            }

            public void Close()
            {
                downloader.DecRefCount();
            }

            public int Read(long position, byte[] buffer, int offset, int count, int timeout = 1000)
            {
                return downloader.Read(position, buffer, offset, count, timeout);
            }
        }

        private void DecRefCount()
        {
            if (Interlocked.Decrement(ref fileRefCount) == 0) Close();
        }

        readonly private AmazonDrive amazon;
        readonly public AmazonChild Node;
        public Action<Downloader> OnDone;
        FileStream file;
        SortedList<long, ManualResetEventSlim> events = new SortedList<long, ManualResetEventSlim>();
        int fileRefCount = 1;

        public static Downloader Start(AmazonDrive amazon, AmazonChild node, string path)
        {
            try
            {
                return new Downloader(amazon, node, path);
            }
            catch (IOException)
            {
                Log.Warn("File exists: " + node.id);
                //File exists
                return null;
            }
        }

        private async void Download()
        {
            var start = Stopwatch.StartNew();
            var buf = new byte[4096];
            try
            {
                while (file.Length < Node.contentProperties.size)
                {
                    await amazon.Files.Download(Node.id, fileOffset: file.Length, streammer: async (stream) =>
                    {
                        int red = 0;
                        long downloaded = file.Length;
                        do
                        {
                            red = await stream.ReadAsync(buf, 0, buf.Length);
                            lock (file)
                            {
                                file.Seek(0, SeekOrigin.End);
                                file.Write(buf, 0, red);
                            }
                            if (downloaded == 0) Log.Trace("Got first part: " + Node.id + " in " + start.ElapsedMilliseconds);
                            downloaded += red;
                            lock (events)
                            {
                                var toRemove = events.Keys.Where(p => p < downloaded).ToList();
                                foreach (var key in toRemove)
                                {
                                    events[key].Set();
                                    events.Remove(key);
                                }
                            }
                        } while (red > 0);
                    });
                }

                lock (events)
                {
                    foreach (var evnt in events.Values) evnt.Set();
                }

                file.Flush();
                if (Interlocked.Decrement(ref fileRefCount) == 0) Close();
            }
            catch (Exception e)
            {
                Log.Error($"Download failed: {Node.id}\r\n{e}");
                lock (file)
                {
                    file.Close();
                    File.Delete(path);
                }
                throw new InvalidOperationException("Download failed", e);
            }
        }

        private bool WaitFor(long position, int timeout)
        {
            if (position >= file.Length)
            {
                var evnt = new ManualResetEventSlim(false);
                lock (events)
                {
                    ManualResetEventSlim exevnt;
                    if (events.TryGetValue(position, out exevnt))
                    {
                        evnt.Dispose();
                        evnt = exevnt;
                    }
                    else
                        events.Add(position, evnt);
                }
                if (!evnt.Wait(timeout)) return false;
                evnt.Dispose();
            }
            return true;
        }

        public Reader Open()
        {
            Interlocked.Increment(ref fileRefCount);
            return new Reader(this);
        }
        private int Read(long position, byte[] buffer, int offset, int count, int timeout = 1000)
        {
            long needpos = position + count - 1;
            if (!WaitFor(needpos, timeout))
            {
                if (position >= file.Length) throw new TimeoutException();
                count = (int)Math.Min(count, (file.Length - position));
            }

            lock (file)
            {
                if (file.Length <= position) throw new InvalidOperationException("Not downloaded");
                file.Position = position;
                return file.Read(buffer, offset, count);
            }
        }

        private void Close()
        {
            Log.Trace("Close downloader: " + Node.id);

            file.Close();
            var newName = Path.Combine(Path.GetDirectoryName(path), Node.id);
            File.Move(path, newName);

            Done = true;
            OnDone?.Invoke(this);
        }

        private Downloader(AmazonDrive amazon, AmazonChild node, string path)
        {
            this.amazon = amazon;
            Node = node;
            this.path = path;
            file = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read, 4096, FileOptions.Asynchronous | FileOptions.RandomAccess);
            Task = Task.Run(() => Download());
        }

        public bool Done = false;
        private readonly string path;

        public Task Task { get; private set; }
    }
    public class SmallFileCache
    {
        static ConcurrentDictionary<AmazonDrive, SmallFileCache> Instances = new ConcurrentDictionary<AmazonDrive, SmallFileCache>(10, 3);
        public static SmallFileCache GetInstance(AmazonDrive amazon) => Instances.GetOrAdd(amazon, (a) => new SmallFileCache(a));

        readonly AmazonDrive amazon;
        const int blockSize = 64 * 1024;
        readonly static string cachePath = Path.Combine(Path.GetTempPath(), "CloudDriveTestCache");

        readonly static ConcurrentDictionary<string, Downloader> fileDownloadBlockers = new ConcurrentDictionary<string, Downloader>(10, 20);

        private SmallFileCache(AmazonDrive a)
        {
            amazon = a;
            Directory.CreateDirectory(cachePath);
        }

        private Downloader CreateDownloader(AmazonChild node)
        {
            var path = Path.Combine(cachePath, node.id);
            var res = Downloader.Start(amazon, node, path + ".d");
            if (res == null) throw new InvalidOperationException("Duplicated file downloader: " + node.id);
            res.OnDone = (d) =>
            {
                Downloader r;
                fileDownloadBlockers.TryRemove(node.id, out r);
                Log.Trace("Downloader removed: " + node.id);
            };
            return res;
        }

        public IBlockReader OpenRead(AmazonChild node)
        {
            var path = Path.Combine(cachePath, node.id);

            Downloader downloader;
            if (fileDownloadBlockers.TryGetValue(node.id, out downloader))
            {
                Log.Trace("Reopened downloader: " + node.id);
                return downloader.Open();
            }
            if (File.Exists(path))
            {
                Log.Trace("Opened cached: " + node.id);
                return new FileBlockReader(path);
            }

            Log.Trace("Opened downloader: " + node.id);
            return fileDownloadBlockers.GetOrAdd(node.id, (id) => CreateDownloader(node)).Open();
        }

    }
}