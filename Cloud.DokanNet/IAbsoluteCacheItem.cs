namespace Azi.Cloud.DokanNet
{
    using System.Threading.Tasks;
    using Azi.Cloud.Common;

    public interface IAbsoluteCacheItem
    {
        FSItem FSItem { get; }

        void Dispose();

        Task<Block> GetBlock(long blockIndex);

        void ReleaseBlock(Block block);
    }
}