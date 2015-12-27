using Azi.Amazon.CloudDrive;
using Azi.Amazon.CloudDrive.JsonObjects;
using System;
using System.IO;

namespace Azi.ACDDokanNet
{
    public class BufferedAmazonBlockReader : AbstractBlockStream
    {
        AmazonDrive amazon;
        private FSItem item;
        const int cacheSize = 500000;
        int blockSize = 0;
        long blockStart = 0;
        byte[] block = new byte[cacheSize];

        public BufferedAmazonBlockReader(FSItem item, AmazonDrive amazon)
        {
            this.item = item;
            this.amazon = amazon;
        }

        public override void Flush()
        {
        }

        public override int Read(long position, byte[] buffer, int offset, int count, int timeout = 1000)
        {
            if (position >= item.Length) return 0;
            if (position + count > item.Length) count = (int)(item.Length - position);
            if (position < blockStart || position + count > blockStart + blockSize)
            {
                int red;
                if (count > cacheSize)
                {
                    red = amazon.Files.Download(item.Id, buffer, offset, position, count).Result;
                    if (red == count)
                    {
                        Array.Copy(buffer, offset + count - cacheSize, block, 0, cacheSize);
                        blockSize = cacheSize;
                        blockStart = position + count - cacheSize;
                    }
                    return red;
                }

                int realCount = cacheSize;
                if (position + realCount > item.Length) realCount = (int)(item.Length - position);
                red = amazon.Files.Download(item.Id, block, 0, position, realCount).Result;
                blockStart = position;
                blockSize = red;
            }

            Array.Copy(block, position - blockStart, buffer, offset, count);

            return count;
        }

        public override void Write(long position, byte[] buffer, int offset, int count, int timeout = 1000)
        {
            throw new NotSupportedException();
        }

        protected override void Dispose(bool disposing)
        {

        }
    }
}