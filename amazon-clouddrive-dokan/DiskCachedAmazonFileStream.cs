using AmazonCloudDriveApi;
using Azi.Amazon.CloudDrive;
using Azi.Amazon.CloudDrive.Json;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;

namespace amazon_clouddrive_dokan
{
    public class DiskCachedAmazonFileStream : Stream
    {
        AmazonDrive amazon;
        AmazonChild node;
        string cachedFilePath;
        FileStream cachedFile;
        readonly static string cachePath = Path.Combine(Path.GetTempPath(), "CloudDriveTestCache");
        static ConcurrentDictionary<string, object> fileDownloadBlockers = new ConcurrentDictionary<string, object>(10, 20);


        static DiskCachedAmazonFileStream()
        {
            Directory.CreateDirectory(cachePath);
        }

        public DiskCachedAmazonFileStream(AmazonChild node, AmazonDrive amazon)
        {
            this.node = node;
            this.amazon = amazon;
        }

        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => false;

        public override long Length => node.contentProperties.size;

        public override long Position
        {
            get
            {
                if (cachedFile == null) OpenFile();
                return cachedFile.Position;
            }

            set
            {
                if (cachedFile == null) OpenFile();

                cachedFile.Position = value;
            }
        }

        public override void Flush()
        {

        }

        bool DownloadFile()
        {
            try
            {
                var blocker = new object();
                lock (blocker)
                {
                    if (!fileDownloadBlockers.TryAdd(node.id, blocker)) return true;
                    using (var file = File.Open(cachedFilePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        try
                        {
                            amazon.Files.Download(node.id, file).Wait();
                            file.Close();
                            return true;
                        }
                        catch (IOException e)
                        {
                            throw new InvalidOperationException("Download failed", e);
                        }
                    }
                }
            }
            catch (IOException)
            {
                return false;
            }

        }

        void OpenFile()
        {
            cachedFilePath = Path.Combine(cachePath, node.id);
            if (!File.Exists(cachedFilePath)) DownloadFile();
            object blocker;
            if (fileDownloadBlockers.TryGetValue(node.id, out blocker))
            {
                //Wait til file finish downloading
                lock (blocker) { }
            }
            cachedFile = File.OpenRead(cachedFilePath);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (cachedFile == null) OpenFile();

            return cachedFile.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            if (cachedFile == null) OpenFile();

            return cachedFile.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override void Close()
        {
            base.Close();
            if (cachedFile != null)
                cachedFile.Close();
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected override void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    if (cachedFile != null)
                    {
                        cachedFile.Dispose();
                        cachedFile = null;
                    }
                }

                disposedValue = true;
            }
        }
        #endregion
    }
}