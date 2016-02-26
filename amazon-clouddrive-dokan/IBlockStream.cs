using System;

namespace Azi.ACDDokanNet
{
    public interface IBlockStream : IDisposable
    {
        Action OnClose { get; set; }

        int Read(long position, byte[] buffer, int offset, int count, int timeout = 1000);

        void Write(long position, byte[] buffer, int offset, int count, int timeout = 1000);

        void Close();

        void Flush();

        void SetLength(long len);
    }
}