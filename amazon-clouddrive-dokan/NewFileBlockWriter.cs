using System;
using System.Threading.Tasks;
using Azi.Amazon.CloudDrive.JsonObjects;
using System.IO;
using Azi.Amazon.CloudDrive;
using Azi.Tools;
using System.Threading;

namespace Azi.ACDDokanNet
{
    public class NewFileBlockWriter : AbstractBlockStream
    {
        public const string UploadFolder = "Upload";

        public delegate void OnUploadDelegate(FSItem node, AmazonNode amazonNode);
        public delegate void OnUploadFailedDelegate(FSItem node, string filePath, string localId);

        private FSProvider provider;
        private FSItem dirNode;
        public readonly FSItem Node;
        private string filePath;
        private readonly FileStream writer;
        private object fileLock = new object();
        public readonly string CachedPath;
        private Task uploader;
        public OnUploadDelegate OnUpload;
        public Action OnUploadStarted;
        public OnUploadFailedDelegate OnUploadFailed;

        public NewFileBlockWriter(FSItem dirNode, FSItem node, string filePath, FSProvider provider)
        {
            this.dirNode = dirNode;
            this.Node = node;
            this.filePath = filePath;
            this.provider = provider;

            if (node == null)
            {
                Node = FSItem.FromFake(filePath, Guid.NewGuid().ToString());
            }

            var dir = Path.Combine(provider.CachePath, UploadFolder);
            Directory.CreateDirectory(dir);
            CachedPath = Path.Combine(dir, Node.Id);
            Log.Trace("Created file: " + filePath);
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
            Node.Length = len;
            Log.Trace("Closed file: " + filePath);
            if (len == 0)
                Log.Warn("Zero Length file: " + filePath);
            else
                uploader = Task.Run(Upload);
            base.Close();
        }

        private async Task Upload()
        {
            try
            {
                Log.Trace("Started upload: " + filePath);
                OnUploadStarted?.Invoke();
                var node = await provider.Amazon.Files.UploadNew(dirNode.Id, Path.GetFileName(filePath),
                    () => new FileStream(CachedPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true));
                if (node != null)
                {
                    OnUpload?.Invoke(dirNode, node);
                    Log.Trace("Finished upload: " + filePath + " id:" + node.id);
                }
                else
                {
                    OnUploadFailed?.Invoke(dirNode, filePath, Node.Id);
                    throw new NullReferenceException("File node is null: " + filePath);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Upload failed: {filePath}\r\n{ex}");
                OnUploadFailed?.Invoke(dirNode, filePath, Node.Id);
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
            Node.Length = writer.Length;
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