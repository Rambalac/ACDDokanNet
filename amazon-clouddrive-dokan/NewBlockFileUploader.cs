using System;
using System.Threading.Tasks;
using Azi.Amazon.CloudDrive.JsonObjects;
using System.IO;
using Azi.Amazon.CloudDrive;
using Azi.Tools;

namespace Azi.ACDDokanNet
{
    public class Interval
    {
        public readonly long Start; //included
        public readonly long End; //included
        public Interval(long a, long b)
        {
            Start = a;
            End = b;
        }
    }
    internal class NewBlockFileUploader : IBlockStream
    {
        private AmazonDrive amazon;
        private AmazonChild dirNode;
        private string name;
        readonly FileStream writer;
        public readonly string CachedName;
        Task uploader;
        public Action<AmazonChild, AmazonChild> OnUpload;

        public NewBlockFileUploader(AmazonChild dirNode, string name, AmazonDrive amazon)
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
            lock (writer)
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
                    var node = await amazon.Files.UploadNew(dirNode.id, name, reader);
                    reader.Close();
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
            lock (writer)
            {
                writer.Position = position;
                writer.Write(buffer, offset, count);
            }
        }

        public void Flush()
        {
            writer.Flush();
        }
    }
}