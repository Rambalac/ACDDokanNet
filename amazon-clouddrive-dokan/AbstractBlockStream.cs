using Azi.Amazon.CloudDrive;
using Azi.Amazon.CloudDrive.JsonObjects;
using Azi.Tools;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Diagnostics;

namespace Azi.ACDDokanNet
{
    public abstract class AbstractBlockStream : IBlockStream
    {
        private int closed = 0;

        public Action OnClose { get; set; }

        public abstract void Flush();

        public abstract int Read(long position, byte[] buffer, int offset, int count, int timeout = 1000);

        public abstract void Write(long position, byte[] buffer, int offset, int count, int timeout = 1000);

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
        }

        public virtual void Close()
        {
            if (Interlocked.CompareExchange(ref closed, 1, 0) != 0)
            {
                return;
            }

            OnClose?.Invoke();
        }

        public abstract void SetLength(long len);

        protected abstract void Dispose(bool disposing);
    }
}