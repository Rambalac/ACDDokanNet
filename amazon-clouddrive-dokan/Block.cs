using System;

namespace Azi.Cloud.DokanNet
{
    internal class Block
    {
        public Block(long n, byte[] d)
        {
            N = n;
            Data = d;
        }

        public long N { get; private set; }

        public DateTime Access { get; set; } = DateTime.UtcNow;

        public byte[] Data { get; private set; }
    }
}