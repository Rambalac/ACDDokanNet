namespace Azi.Cloud.DokanNet
{
    using System;
    using System.Threading.Tasks;

    public interface IBlockStream : IDisposable
    {
        Func<Task> OnClose { get; set; }

        Task<int> Read(long position, byte[] buffer, int offset, int count, int timeout = 1000);

        Task Write(long position, byte[] buffer, int offset, int count, int timeout = 1000);

        void Close();

        void Flush();

        void SetLength(long len);
    }
}