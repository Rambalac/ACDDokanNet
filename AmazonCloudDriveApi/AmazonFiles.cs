using Azi.Amazon.CloudDrive.JsonObjects;
using Azi.Tools;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using HttpClient = Azi.Tools.HttpClient;

namespace Azi.Amazon.CloudDrive
{
    public class AmazonFiles
    {
        private readonly AmazonDrive amazon;
        private HttpClient http => amazon.http;

        public AmazonFiles(AmazonDrive amazonDrive)
        {
            amazon = amazonDrive;
        }

        public async Task<AmazonNode> Overwrite(string id, Func<Stream> streamCreator)
        {
            var url = string.Format("{0}nodes/{1}/content", await amazon.GetContentUrl(), id);
            var file = new FileUpload
            {
                StreamOpener = streamCreator,
                FileName = id,
                FormName = "content"
            };
            return await http.SendFile<AmazonNode>(HttpMethod.Put, url, file);
        }

        public async Task<AmazonNode> UploadNew(string parentId, string fileName, Func<Stream> streamCreator)
        {
            var url = string.Format("{0}nodes", await amazon.GetContentUrl());

            string meta = JsonConvert.SerializeObject(new NewChild { name = fileName, parents = new string[] { parentId }, kind = "FILE" });

            var file = new FileUpload
        {
                StreamOpener = streamCreator,
                FileName = fileName,
                FormName = "content",
                Parameters = new Dictionary<string, string>
            {
                        {"metadata", meta}
                    }
            };
            return await http.SendFile<AmazonNode>(HttpMethod.Post, url, file);
        }

        public async Task Download(string id, Stream stream, long? fileOffset = null, long? length = null, int bufferSize = 4096, Func<long, long> progress = null)
        {
            var url = string.Format("{0}nodes/{1}/content", await amazon.GetContentUrl(), id);
            await http.GetToStreamAsync(url, stream, fileOffset, length, bufferSize, progress);
        }

        public async Task Download(string id, Func<HttpWebResponse, Task> streammer, long? fileOffset = null, long? length = null)
        {
            var url = string.Format("{0}nodes/{1}/content", await amazon.GetContentUrl(), id);
            await http.GetToStreamAsync(url, streammer, fileOffset, length);
        }

        public async Task<int> Download(string id, byte[] buffer, int bufferIndex, long fileOffset, int length)
        {
            var url = string.Format("{0}nodes/{1}/content", await amazon.GetContentUrl(), id);
            return await http.GetToBufferAsync(url, buffer, bufferIndex, fileOffset, length);
        }

    }
}