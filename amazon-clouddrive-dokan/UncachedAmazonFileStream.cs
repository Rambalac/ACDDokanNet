using Azi.Amazon.CloudDrive;
using Azi.Amazon.CloudDrive.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace amazon_clouddrive_dokan
{
    public class UncachedAmazonFileStream : Stream
    {
        AmazonDrive amazon;
        private AmazonChild node;
        long position = 0;

        public UncachedAmazonFileStream(AmazonChild node, AmazonDrive amazon)
        {
            this.node = node;
            this.amazon = amazon;
        }

        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => true;

        public override long Length => node.contentProperties.size;

        public override long Position
        {
            get
            {
                return position;
            }

            set
            {
                Seek(value, SeekOrigin.Begin);
            }
        }

        public override void Flush()
        {

        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var red = amazon.Files.Download(node.id, buffer, offset, position, count).Result;
            position += red;
            return red;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin:
                    position = offset;
                    break;
                case SeekOrigin.Current:
                    position += offset;
                    break;
                case SeekOrigin.End:
                    position = node.contentProperties.size + offset;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            return position;
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }
    }
}