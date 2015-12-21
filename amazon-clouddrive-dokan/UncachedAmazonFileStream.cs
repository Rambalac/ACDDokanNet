using Azi.Amazon.CloudDrive;
using Azi.Amazon.CloudDrive.JsonObjects;
using System;
using System.IO;

namespace Azi.ACDDokanNet
{
    public class UncachedAmazonFileStream : Stream
    {
        AmazonDrive amazon;
        private AmazonNode node;
        long position = 0;

        public UncachedAmazonFileStream(AmazonNode node, AmazonDrive amazon)
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
                if (position != value)
                    Seek(value, SeekOrigin.Begin);
            }
        }

        public override void Flush()
        {

        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (position >= node.contentProperties.size) return 0;
            if (position + count > node.contentProperties.size) count = (int)(node.contentProperties.size - position);
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
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }
    }
}