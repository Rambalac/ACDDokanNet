using Azi.Amazon.CloudDrive.Json;
using Azi.Tools;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Azi.Amazon.CloudDrive
{
    public class AmazonFiles
    {
        private readonly AmazonDrive amazon;
        HttpClient http => amazon.http;
        static TimeSpan generalExpiration => AmazonDrive.generalExpiration;

        public AmazonFiles(AmazonDrive amazonDrive)
        {
            amazon = amazonDrive;
        }

        public async Task<AmazonChild> Overwrite(string id, Stream stream)
        {
            var url = string.Format("{0}/nodes/{1}/content", await amazon.GetContentUrl(), id);
            return await http.PostFile<AmazonChild>(url, null, stream);
        }

        public async Task<AmazonChild> UploadNew(string parenId, string fileName, Stream stream)
        {
            var url = string.Format("{0}/nodes", await amazon.GetContentUrl());
            var form = new Dictionary<string, string>
            {
                {"name",fileName},
                {"kind","FILE"},
                {"parents",parenId}
            };
            return await http.PostFile<AmazonChild>(url, form, stream);
        }

        public async Task Download(string id, Stream stream, long? fileOffset = null, long? length = null, int bufferSize = 4096, Func<long, long> progress = null)
        {
            var url = string.Format("{0}/nodes/{1}/content", await amazon.GetContentUrl(), id);
            await http.GetToStreamAsync(url, stream, fileOffset, length, bufferSize, progress);
        }

        public async Task Download(string id, Func<Stream, Task> streammer, long? fileOffset = null, long? length = null)
        {
            var url = string.Format("{0}/nodes/{1}/content", await amazon.GetContentUrl(), id);
            await http.GetToStreamAsync(url, streammer, fileOffset, length);
        }

        public async Task<int> Download(string id, byte[] buffer, int bufferIndex, long fileOffset, int length)
        {
            var url = string.Format("{0}/nodes/{1}/content", await amazon.GetContentUrl(), id);
            return await http.GetToBufferAsync(url, buffer, bufferIndex, fileOffset, length);
        }

        public Task Download(string id, FileStream writer, long length, Func<long, long> progress)
        {
            throw new NotImplementedException();
        }
    }
}