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
        private Task task;
        private long downloaded = 0;

        public Downloader(FSItem item, string path)
        {
            Item = item;
            Path = path;
        }

        public FSItem Item { get; private set; }

        public string Path { get; private set; }

        public Task Task
        {
            get
            {
                return task;
            }

            set
            {
                if (task != null)
                {
                    throw new InvalidOperationException("Cannot reset task");
                }

                task = value;
            }
        }

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

        public static Downloader CreateCompleted(FSItem item, string path, long length)
        {
            return new Downloader(item, path)
            {
                Task = Task.FromResult<bool>(true),
                Downloaded = length
            };
        }

        public bool WaitToTheEnd(int timeout)
        {
            return Task.Wait(timeout);
        }
    }
}