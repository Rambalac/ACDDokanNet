namespace Azi.Cloud.DokanNet
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Common;
    using Tools;

    public class BufferedHttpCloudBlockReader : AbstractBlockStream
    {
        private const int TotalCache = 50 * 1024 * 1024;
        private const int BlockSize = 64 * 1024;
        private const int KeepLastBlocks = TotalCache / BlockSize;

        private static readonly ConcurrentDictionary<string, Block> Blocks = new ConcurrentDictionary<string, Block>(5, KeepLastBlocks * 5);
        private readonly SemaphoreSlim readStreamSync = new SemaphoreSlim(1, 1);
        private readonly IHttpCloud cloud;
        private readonly FSItem item;
        private long lastBlock;
        private Stream stream;
        private bool closed;

        public BufferedHttpCloudBlockReader(FSItem item, IHttpCloud cloud)
        {
            this.item = item;
            this.cloud = cloud;
        }

        public override void Flush()
        {
        }

        public override async Task<int> Read(long position, byte[] buffer, int offset, int count, int timeout = 1000)
        {
            if (position >= item.Length)
            {
                return 0;
            }

            if (position + count > item.Length)
            {
                count = (int)(item.Length - position);
            }

            using (var timeoutcancel = new CancellationTokenSource(timeout))
            {
                try
                {
                    Log.Trace($"Big read {item.Name} Offset: {position} Size: {count}");
                    var bs = position / BlockSize;
                    var be = (position + count - 1) / BlockSize;
                    var blocksRead = await GetBlocks(bs, be, timeoutcancel.Token);

                    var bspos = bs * BlockSize;
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

                    for (var i = 1; i < blocksRead.Count - 1; i++)
                    {
                        Array.Copy(blocksRead[i], 0, buffer, offset, blocksRead[i].Length);
                        offset += blocksRead[i].Length;
                        left -= blocksRead[i].Length;
                    }

                    Array.Copy(blocksRead[blocksRead.Count - 1], 0, buffer, offset, left);

                    return count;
                }
                catch (TaskCanceledException ex)
                {
                    throw new TimeoutException($"Server did not respond in time: {item.Name} Pos: {position}\r\n{ex}");
                }
            }
        }

        public override void SetLength(long len)
        {
            throw new NotSupportedException();
        }

        public override Task Write(long position, byte[] buffer, int offset, int count, int timeout = 1000)
        {
            throw new NotSupportedException();
        }

        public override void Close()
        {
            if (closed)
            {
                return;
            }

            closed = true;

            stream?.Close();
            base.Close();
        }

        protected override void Dispose(bool disposing)
        {
            Close();
            stream?.Dispose();
            readStreamSync.Dispose();
        }

        private void CheckCacheSize()
        {
            while (Blocks.Count > KeepLastBlocks)
            {
                var del = Blocks.Values.Aggregate((curMin, x) => (curMin == null || (x.Access < curMin.Access)) ? x : curMin);
                Blocks.TryRemove(del.Key, out Block remove);
            }
        }

        private async Task<List<Block>> DownloadBlocks(long block, long blockCount, CancellationToken timeouttoken)
        {
            var start = Stopwatch.StartNew();
            if (lastBlock != block)
            {
                Log.Warn($"Buffered Read block changed from {lastBlock} to {block}", Log.BigFile);
            }

            var pos = block * BlockSize;
            var totallen = (pos + (BlockSize * blockCount)) <= item.Length ? BlockSize * blockCount : (int)(item.Length - pos);
            if (totallen == 0)
            {
                return new List<Block>();
            }

            var left = totallen;

            var result = new List<Block>();

            Log.Trace($"Download file {item.Name} Offset: {pos} Size: {totallen}", Log.BigFile);

            var buff = new byte[BlockSize];

            if (stream == null)
            {
                stream = await cloud.Files.Download(item.Id);
            }

            var firstread = true;
            stream.Position = pos;
            while (left > 0)
            {
                timeouttoken.ThrowIfCancellationRequested();
                var blockLeft = (int)((left < BlockSize) ? left : BlockSize);
                var membuf = new MemoryStream(BlockSize);
                while (blockLeft > 0)
                {
                    var red = await stream.ReadAsync(buff, 0, blockLeft, timeouttoken);
                    if (firstread)
                    {
                        Log.Trace($"First read {item.Name} in {start.Elapsed.TotalSeconds}", Log.BigFile);
                        firstread = false;
                    }

                    if (red == 0)
                    {
                        Log.ErrorTrace("Download 0", Log.BigFile);
                        throw new InvalidOperationException("Download 0");
                    }

                    blockLeft -= red;
                    membuf.Write(buff, 0, red);
                    left -= red;
                }

                var arr = membuf.ToArray();
                result.Add(new Block(item.Id, block, arr));
                block++;
                lastBlock = block;
            }

            Log.Trace($"Dowload finished {item.Name} in {start.Elapsed.TotalSeconds}", Log.BigFile);
            Log.Trace($"Download file finished {item.Name} Offset: {pos} Size: {totallen}", Log.BigFile);
            return result;
        }

        private async Task<List<byte[]>> GetBlocks(long v1, long v2, CancellationToken timeouttoken)
        {
            var resultStart = new List<byte[]>();
            var resultEnd = new List<byte[]>();
            await readStreamSync.WaitAsync(timeouttoken);

            try
            {
                long intervalStart;
                for (intervalStart = v1; intervalStart <= v2; intervalStart++)
                {
                    if (!Blocks.TryGetValue(Block.GetKey(item.Id, intervalStart), out Block cachedBlock))
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
                    if (!Blocks.TryGetValue(Block.GetKey(item.Id, intervalEnd), out Block cachedBlock))
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

                var resultMiddle = await DownloadBlocks(intervalStart, intervalEnd - intervalStart + 1, timeouttoken);
                foreach (var block in resultMiddle)
                {
                    result.Add(block.Data);
                    Blocks.AddOrUpdate(block.Key, block, (k, b) => b);
                }

                result.AddRange(resultEnd);

                CheckCacheSize();

                return result;
            }
            finally
            {
                readStreamSync.Release();
            }
        }
    }
}