using Azi.Amazon.CloudDrive;
using Azi.Amazon.CloudDrive.JsonObjects;
using Azi.Tools;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Azi.ACDDokanNet
{
    public class BufferedAmazonBlockReader : AbstractBlockStream
    {
        class Block
        {
            public readonly long N;
            public DateTime access = DateTime.UtcNow;
            public readonly byte[] Data;
            public Block(long n, byte[] d)
            {
                N = n;
                Data = d;
            }
        }
        AmazonDrive amazon;
        private FSItem item;
        const int blockSize = 4 * 1024 * 1024;
        const int keepLastBlocks = 5;
        ConcurrentDictionary<long, Block> blocks = new ConcurrentDictionary<long, Block>(5, keepLastBlocks * 5);

        public BufferedAmazonBlockReader(FSItem item, AmazonDrive amazon)
        {
            this.item = item;
            this.amazon = amazon;
        }

        public override void Flush()
        {
        }

        private byte[][] GetBlocks(long v1, long v2)
        {
            var result = new byte[v2 - v1 + 1][];
            var tasks = new List<Task>();
            for (long block = v1; block <= v2; block++)
            {
                long blockcopy = block;
                tasks.Add(Task.Run(() =>
                {
                    var b = blocks.GetOrAdd(blockcopy, DownloadBlock);
                    b.access = DateTime.UtcNow;

                    while (blocks.Count > keepLastBlocks)
                    {
                        var del = blocks.Values.Aggregate((curMin, x) => (curMin == null || (x.access < curMin.access)) ? x : curMin);
                        Block remove;
                        blocks.TryRemove(del.N, out remove);
                    }

                    result[blockcopy - v1] = b.Data;
                }));
            }
            Task.WhenAll(tasks).Wait();
            return result;
        }

        private Block DownloadBlock(long block)
        {
            var pos = block * blockSize;
            var count = pos + blockSize <= item.Length ? blockSize : (int)(item.Length - pos);
            if (count == 0) return new Block(block, new byte[0]);
            var result = new byte[count];
            int offset = 0;
            int left = count;
            while (left > 0)
            {
                int red = amazon.Files.Download(item.Id, result, offset, pos, left).Result;
                if (red == 0)
                {
                    Log.Error("Download 0");
                    throw new InvalidOperationException("Download 0");
                }
                offset += red;
                left -= red;
            }
            return new Block(block, result);
        }

        public override int Read(long position, byte[] buffer, int offset, int count, int timeout = 1000)
        {
            if (position >= item.Length) return 0;
            if (position + count > item.Length) count = (int)(item.Length - position);

            var bs = position / blockSize;
            var be = (position + count - 1) / blockSize;
            var blocks = GetBlocks(bs, be);
            var bspos = bs * blockSize;
            var bepos = (be + 1) * blockSize - 1;
            if (bs == be)
            {
                Array.Copy(blocks[0], position - bspos, buffer, offset, count);
                return count;
            }

            var inblock = (int)(position - bspos);
            var inblockcount = blocks[0].Length - inblock;
            Array.Copy(blocks[0], inblock, buffer, offset, inblockcount);
            offset += inblockcount;
            var left = count - inblockcount;

            for (int i = 1; i < blocks.Length - 1; i++)
            {
                Array.Copy(blocks[i], 0, buffer, offset, blocks[i].Length);
                offset += blocks[i].Length;
                left -= blocks[i].Length;
            }

            Array.Copy(blocks[blocks.Length - 1], 0, buffer, offset, left);

            return count;
        }

        public override void SetLength(long len)
        {
            throw new NotSupportedException();
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