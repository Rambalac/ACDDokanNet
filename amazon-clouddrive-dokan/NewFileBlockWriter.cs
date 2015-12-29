using System;
using System.Threading.Tasks;
using Azi.Amazon.CloudDrive.JsonObjects;
using System.IO;
using Azi.Amazon.CloudDrive;
using Azi.Tools;
using System.Threading;
using System.Security.Cryptography;

namespace Azi.ACDDokanNet
{
    public class NewFileBlockWriter : AbstractBlockStream
    {
        public const string UploadFolder = "Upload";

        public delegate void OnUploadDelegate(FSItem item, AmazonNode amazonNode);
        public delegate void OnUploadFailedDelegate(FSItem item, string filePath, string localId);

        private FSProvider provider;
        private FSItem dirItem;
        public readonly FSItem Item;
        private readonly FileStream writer;
        private object fileLock = new object();
        public readonly string CachedPath;
        private Task uploader;
        public OnUploadDelegate OnUpload;
        public Action OnUploadStarted;
        public OnUploadFailedDelegate OnUploadFailed;
        public Func<string, bool> OnIsDuplicate;

        public NewFileBlockWriter(FSItem dirItem, FSItem item, string filePath, FSProvider provider)
        {
            this.dirItem = dirItem;
            this.Item = item;
            this.provider = provider;

            if (item == null)
            {
                Item = FSItem.FromFake(filePath, Guid.NewGuid().ToString(), dirItem.Id);
            }

            var dir = Path.Combine(provider.CachePath, UploadFolder);
            Directory.CreateDirectory(dir);
            CachedPath = Path.Combine(dir, Item.Id);
            Log.Trace("Created file: " + Item.Path);
            writer = File.OpenWrite(CachedPath);
        }

        int closed = 0;
        public override void Close()
        {
            if (Interlocked.CompareExchange(ref closed, 1, 0) == 1) return;

            long len = writer.Length;
            lock (fileLock)
            {
                writer.Close();
            }
            Item.Length = len;
            Log.Trace("Closed file: " + Item.Path);
            if (len == 0)
                Log.Warn("Zero Length file: " + Item.Path);
            else
            {
                if (!OnIsDuplicate(CalcMD5()))
                    uploader = Task.Run(Upload);
            }
            base.Close();
        }

        private string CalcMD5()
        {
            using (var md5 = MD5.Create())
            {
                using (var stream = File.OpenRead(CachedPath))
                {
                    var bytes = md5.ComputeHash(stream);
                    return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
                }
            }
        }

        private async Task Upload()
        {
            try
            {
                Log.Trace("Started upload: " + Item.Path);
                OnUploadStarted?.Invoke();
                var node = await provider.Amazon.Files.UploadNew(dirItem.Id, Path.GetFileName(Item.Path),
                    () => new FileStream(CachedPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true));
                if (node != null)
                {
                    OnUpload?.Invoke(dirItem, node);
                    Log.Trace("Finished upload: " + Item.Path + " id:" + node.id);
                }
                else
                {
                    OnUploadFailed?.Invoke(dirItem, Item.Path, Item.Id);
                    throw new NullReferenceException("File node is null: " + Item.Path);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Upload failed: {Item.Path}\r\n{ex}");
                OnUploadFailed?.Invoke(dirItem, Item.Path, Item.Id);
            }
        }

        public override int Read(long position, byte[] buffer, int offset, int count, int timeout = 1000)
        {
            throw new NotSupportedException();
        }

        public override void Write(long position, byte[] buffer, int offset, int count, int timeout = 1000)
        {
            lock (fileLock)
            {
                writer.Position = position;
                writer.Write(buffer, offset, count);
            }
            Item.Length = writer.Length;
            Log.Trace("Write byte: " + count);
        }

        public override void Flush()
        {
            writer.Flush();
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected override void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    writer.Dispose();
                }

                disposedValue = true;
            }
        }

        #endregion
    }
}