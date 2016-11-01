namespace Azi.Cloud.DokanNet
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Common;
    using Tools;

    public class BufferedHttpCloudBlockReader : AbstractBlockStream
    {
        private const int BlockSize = 1 * 1024 * 1024;
        private const int KeepLastBlocks = 5;

        private readonly ConcurrentDictionary<long, Block> blocks = new ConcurrentDictionary<long, Block>(5, KeepLastBlocks * 5);
        private IHttpCloud cloud;
        private FSItem item;
        private long lastBlock;

        public BufferedHttpCloudBlockReader(FSItem item, IHttpCloud cloud)
        {
            this.item = item;
            this.cloud = cloud;
        }

        public override void Flush()
        {
        }

        public override int Read(long position, byte[] buffer, int offset, int count, int timeout = 1000)
        {
            if (position >= item.Length)
            {
                return 0;
            }

            if (position + count > item.Length)
            {
                count = (int)(item.Length - position);
            }

            Log.Trace($"Big read {item.Name} Offset: {position} Size: {count}");
            var bs = position / BlockSize;
            var be = (position + count - 1) / BlockSize;
            var blocksRead = GetBlocks(bs, be);
            var bspos = bs * BlockSize;
            var bepos = ((be + 1) * BlockSize) - 1;
            if (bs == be)
            {
                Array.Copy(blocksRead[0], position - bspos, buffer, offset, count);
                return count;
            }

            var inblock = (int)(position - bspos);
            var inblockcount = blocksRead[0].Length - inblock;
            Array.Copy(blocksRead[0], inblock, buffer, offset, inblockcount);
            offset += inblockcount;
            var left = count - inblockcount;

            for (int i = 1; i < blocksRead.Count - 1; i++)
            {
                Array.Copy(blocksRead[i], 0, buffer, offset, blocksRead[i].Length);
                offset += blocksRead[i].Length;
                left -= blocksRead[i].Length;
            }

            Array.Copy(blocksRead[blocksRead.Count - 1], 0, buffer, offset, left);

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

        private List<Block> DownloadBlocks(long block, long blockCount)
        {
            if (lastBlock != block)
            {
                Log.Warn($"Buffered Read block changed from {lastBlock} to {block}");
            }

            var pos = block * BlockSize;
            var totallen = (pos + (BlockSize * blockCount)) <= item.Length ? BlockSize * blockCount : (int)(item.Length - pos);
            if (totallen == 0)
            {
                return new List<Block>();
            }

            var left = totallen;

            var result = new List<Block>();

            Log.Trace($"Download file {item.Name} Offset: {pos} Size: {totallen}");
            var wait = cloud.Files.Download(
                item.Id,
                async (stream) =>
                {
                    var buff = new byte[BlockSize];
                    while (left > 0)
                    {
                        var blockLeft = (int)((left < BlockSize) ? left : BlockSize);
                        var membuf = new MemoryStream(BlockSize);
                        while (blockLeft > 0)
                        {
                            var red = await stream.ReadAsync(buff, 0, blockLeft);
                            if (red == 0)
                            {
                                Log.Error("Download 0");
                                throw new InvalidOperationException("Download 0");
                            }
                            blockLeft -= red;
                            membuf.Write(buff, 0, red);
                            left -= red;
                        }
                        var arr = membuf.ToArray();
                        result.Add(new Block(block, arr));
                        block++;
                        lastBlock = block;
                    }
                },
                pos,
                totallen).Wait(30000);

            if (!wait)
            {
                throw new TimeoutException();
            }

            return result;
        }

        private List<byte[]> GetBlocks(long v1, long v2)
        {
            var resultStart = new List<byte[]>();
            var resultEnd = new List<byte[]>();
            var tasks = new List<Task>();
            long intervalStart;
            for (intervalStart = v1; intervalStart <= v2; intervalStart++)
            {
                Block cachedBlock;
                if (!blocks.TryGetValue(intervalStart, out cachedBlock))
                {
                    break;
                }

                cachedBlock.Access = DateTime.UtcNow;
                resultStart.Add(cachedBlock.Data);
            }

            if (intervalStart > v2)
            {
                return resultStart;
            }

            long intervalEnd;
            for (intervalEnd = v2; intervalEnd > intervalStart; intervalEnd--)
            {
                Block cachedBlock;
                if (!blocks.TryGetValue(intervalEnd, out cachedBlock))
                {
                    break;
                }

                cachedBlock.Access = DateTime.UtcNow;
                resultEnd.Add(cachedBlock.Data);
            }

            if (intervalStart > intervalEnd)
            {
                throw new InvalidOperationException("Start cannot be after end");
            }

            resultEnd.Reverse();

            var result = new List<byte[]>((int)(v2 - v1 + 1));
            result.AddRange(resultStart);

            var resultMiddle = DownloadBlocks(intervalStart, intervalEnd - intervalStart + 1);
            foreach (var block in resultMiddle)
            {
                result.Add(block.Data);
                blocks.AddOrUpdate(block.N, block, (k, b) => b);
            }

            result.AddRange(resultEnd);

            CheckCacheSize();

            return result;
        }

        private void CheckCacheSize()
        {
            while (blocks.Count > KeepLastBlocks)
            {
                var del = blocks.Values.Aggregate((curMin, x) => (curMin == null || (x.Access < curMin.Access)) ? x : curMin);
                Block remove;
                blocks.TryRemove(del.N, out remove);
            }
        }
    }
}