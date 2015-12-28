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

    public class FileBlockReader : AbstractBlockStream
    {
        private readonly ThreadLocal<FileStream> files;
        private readonly long expectedLength;
        private readonly string filePath;

        public FileBlockReader(string path, long length)
        {
            filePath = path;
            expectedLength = length;
            files = new ThreadLocal<FileStream>(() => new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite), true);
        }

        int closed = 0;
        public override void Close()
        {
            if (Interlocked.CompareExchange(ref closed, 1, 0) == 1) return;

            Log.Trace(Path.GetFileName(filePath));

            foreach (var file in files.Values)
            {
                file.Close();
            }
            base.Close();
        }

        const int waitForFile = 50;

        public override int Read(long position, byte[] buffer, int offset, int count, int timeout)
        {
            if (expectedLength == 0) return 0;
            var timeouttime = DateTime.UtcNow.AddMilliseconds(timeout);
            int red;
            do
            {
                var file = files.Value;
                file.Position = position;
                red = file.Read(buffer, offset, count);
                if (red != 0) return red;
                Thread.Sleep(waitForFile);
                if (DateTime.UtcNow > timeouttime) throw new TimeoutException();
            } while (true);
        }

        public override void Write(long position, byte[] buffer, int offset, int count, int timeout = 1000)
        {
            throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected override void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    files.Dispose();
                }

                disposedValue = true;
            }
        }
        #endregion
    }
}