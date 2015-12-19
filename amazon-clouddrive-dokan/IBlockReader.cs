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
    public interface IBlockStream: IDisposable
    {
        int Read(long position, byte[] buffer, int offset, int count, int timeout = 1000);
        void Write(long position, byte[] buffer, int offset, int count, int timeout = 1000);
        void Close();
        void Flush();
    }

}