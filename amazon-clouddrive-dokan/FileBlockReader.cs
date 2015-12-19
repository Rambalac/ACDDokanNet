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

    public class FileBlockReader : IBlockStream
    {
        private readonly FileStream file;
        private object fileLock=new object();

        public FileBlockReader(string path)
        {
            file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        }

        public FileBlockReader(FileStream str)
        {
            file = str;
        }

        public void Close()
        {
            Log.Trace(Path.GetFileName(file.Name));

            lock (fileLock)
            {
                file.Close();
            }
        }

        const int waitForFile = 50;

        public int Read(long position, byte[] buffer, int offset, int count, int timeout)
        {
            var timeouttime = DateTime.UtcNow.AddMilliseconds(timeout);
            int red;
            do
            {
                lock (fileLock)
                {
                    file.Position = position;
                    red = file.Read(buffer, offset, count);
                }
                if (red != 0) return red;
                Thread.Sleep(waitForFile);
                if (DateTime.UtcNow > timeouttime) throw new TimeoutException();
            } while (true);
        }

        public void Write(long position, byte[] buffer, int offset, int count, int timeout = 1000)
        {
            throw new NotSupportedException();
        }

        public void Flush()
        {
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    file.Dispose();
                }

                disposedValue = true;
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
        }
        #endregion
    }
}