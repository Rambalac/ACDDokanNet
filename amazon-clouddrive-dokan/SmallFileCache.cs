using Azi.Amazon.CloudDrive;
using Azi.Amazon.CloudDrive.JsonObjects;
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
        static ConcurrentDictionary<AmazonDrive, SmallFileCache> Instances = new ConcurrentDictionary<AmazonDrive, SmallFileCache>(10, 3);

        readonly AmazonDrive Amazon;
        private static string cachePath = null;
        public string CachePath
        {
            get { return cachePath; }
            set
            {
                if (cachePath == value) return;
                try
                {
                    if (cachePath!=null)
                    Directory.Delete(cachePath, true);
                }
                catch (Exception)
                {
                    Log.Warn("Can not delete old cache: " + cachePath);
                }
                cachePath = Path.Combine(value, "SmallFiles");
                Directory.CreateDirectory(cachePath);
            }
        }

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
                }
                catch (Exception ex)
                {
                    OnDownloadFailed?.Invoke(node.Id);
                    Log.Error($"Download failed: {node.Id}\r\n{ex}");
                }
        }

        public SmallFileCache(AmazonDrive a)
        {
            Amazon = a;
        }

        public IBlockStream OpenRead(FSItem node)
        {
            var path = Path.Combine(cachePath, node.Id);
            StartDownload(node, path);

            Log.Trace("Opened cached: " + node.Id);
            return new FileBlockReader(path, node.Length);
        }
    }
}