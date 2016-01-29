using Azi.Amazon.CloudDrive;
using Azi.Amazon.CloudDrive.JsonObjects;
using Azi.Tools;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Azi.ACDDokanNet
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