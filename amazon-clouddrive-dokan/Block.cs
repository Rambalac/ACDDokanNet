namespace Azi.Cloud.DokanNet
{
    using System;

    internal class Block
    {
        public Block(string itemid, long n, byte[] d)
        {
            Key = GetKey(itemid, n);
            Data = d;
        }

        public string Key { get; private set; }

        public DateTime Access { get; set; } = DateTime.UtcNow;

        public byte[] Data { get; private set; }

        internal static string GetKey(string id, long intervalStart)
        {
            return id + '\0' + intervalStart;
        }
    }
}