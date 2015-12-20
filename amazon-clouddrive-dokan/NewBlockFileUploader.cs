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
        private AmazonDrive amazon;
        private FSItem dirNode;
        private string name;
        private readonly FileStream writer;
        private object fileLock=new object();
        public readonly string CachedName;
        private Task uploader;
        public Action<FSItem, AmazonChild> OnUpload;

        public NewBlockFileUploader(FSItem dirNode, string name, AmazonDrive amazon)
        {
            this.dirNode = dirNode;
            this.name = name;
            this.amazon = amazon;
            CachedName = Guid.NewGuid().ToString();
            var path = Path.Combine(SmallFileCache.CachePath, CachedName);
            Log.Trace("Created file: " + name);
            writer = File.OpenWrite(path);
        }

        public void Close()
        {
            lock (fileLock)
            {
                writer.Close();
            }
            Log.Trace("Closed file: " + name);
            uploader = Task.Run(Upload);
        }

        private async Task Upload()
        {
            try
            {
                Log.Trace("Started upload: " + name);
                using (var reader = new FileStream(Path.Combine(SmallFileCache.CachePath, CachedName), FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true))
                {
                    var node = await amazon.Files.UploadNew(dirNode.Id, name, reader);
                    reader.Close();
                    if (node == null) throw new NullReferenceException("File node is null: " + name);
                    OnUpload(dirNode, node);
                    Log.Trace("Finished upload: " + name + " id:" + node.id);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Upload failed: {name}\r\n{ex}");
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