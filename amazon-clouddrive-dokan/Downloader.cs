using Azi.Amazon.CloudDrive;
using Azi.Tools;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Diagnostics;
using System.Net.Http.Headers;

namespace Azi.ACDDokanNet
{
    public class Downloader
    {
        public readonly FSItem Item;
        public readonly string Path;
        private Task task;
        public Task Task
        {
            get { return task; }
            set
            {
                if (task != null) throw new InvalidOperationException("Cannot reset task");
                task = value;
            }
        }
        private long downloaded = 0;

        public long Downloaded
        {
            get
            {
                return Interlocked.Read(ref downloaded);
            }

            set
            {
                Interlocked.Exchange(ref downloaded, value);
            }
        }

        public bool WaitToTheEnd(int timeout)
        {
            return Task.Wait(timeout);
        }

        public Downloader(FSItem item, string path)
        {
            Item = item;
            Path = path;
        }
        public static Downloader CreateCompleted(FSItem item, string path, long length)
        {
            return new Downloader(item, path)
            {
                Task = Task.FromResult<bool>(true),
                Downloaded = length
            };
        }
    }
}