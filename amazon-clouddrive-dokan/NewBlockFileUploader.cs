using System;
using System.Threading.Tasks;
using Azi.Amazon.CloudDrive.JsonObjects;
using System.IO;
using Azi.Amazon.CloudDrive;
using Azi.Tools;

namespace Azi.ACDDokanNet
{
    public class NewBlockFileUploader : IBlockStream
    {
        public delegate void OnUploadDelegate(FSItem node, AmazonNode amazonNode);
        public delegate void OnUploadFailedDelegate(FSItem node, string filePath, string localId);

        private AmazonDrive amazon;
        private FSItem dirNode;
        public readonly FSItem Node;
        private string filePath;
        private readonly FileStream writer;
        private object fileLock = new object();
        private Task uploader;
        public OnUploadDelegate OnUpload;
        public OnUploadFailedDelegate OnUploadFailed;

        public NewBlockFileUploader(FSItem dirNode, FSItem node, string filePath, AmazonDrive amazon)
        {
            this.dirNode = dirNode;
            this.Node = node;
            this.filePath = filePath;
            this.amazon = amazon;

            if (node == null)
            {
                Node = FSItem.FromFake(filePath, Guid.NewGuid().ToString());
            }

            var path = Path.Combine(SmallFileCache.CachePath, Node.Id);
            Log.Trace("Created file: " + filePath);
            writer = File.OpenWrite(path);
        }

        public void Close()
        {
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
        }

        private async Task Upload()
        {
            try
            {
                Log.Trace("Started upload: " + filePath);
                using (var reader = new FileStream(Path.Combine(SmallFileCache.CachePath, Node.Id), FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true))
                {
                    var node = await amazon.Files.UploadNew(dirNode.Id, Path.GetFileName(filePath), reader);
                    if (node != null)
                    {
                        OnUpload(dirNode, node);
                        Log.Trace("Finished upload: " + filePath + " id:" + node.id);
                    }
                    else
                    {
                        OnUploadFailed(dirNode, filePath, Node.Id);
                        throw new NullReferenceException("File node is null: " + filePath);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Upload failed: {filePath}\r\n{ex}");
            }
        }

        public int Read(long position, byte[] buffer, int offset, int count, int timeout = 1000)
        {
            throw new NotSupportedException();
        }

        public void Write(long position, byte[] buffer, int offset, int count, int timeout = 1000)
        {
            lock (fileLock)
            {
                writer.Position = position;
                writer.Write(buffer, offset, count);
            }
            Node.Length = writer.Length;
        }

        public void Flush()
        {
            writer.Flush();
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
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

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
        }
        #endregion
    }
}