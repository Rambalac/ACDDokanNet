using Azi.Amazon.CloudDrive.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
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
            var url = string.Format("{0}/nodes/{1}/content", await amazon.GetMetadataUrl(), id);
            return await http.PostFile<AmazonChild>(url, null, stream);
        }

        public async Task<AmazonChild> UploadNew(string parenId, string fileName, Stream stream)
        {
            var url = string.Format("{0}/nodes", await amazon.GetMetadataUrl());
            var form = new Dictionary<string, string>
            {
                {"name",fileName},
                {"kind","FILE"},
                {"parents",parenId}
            };
            return await http.PostFile<AmazonChild>(url, form, stream);
        }

        public async Task Download(string id, Stream stream)
        {
            var url = string.Format("{0}/nodes/{1}/content", await amazon.GetMetadataUrl(), id);
            await http.GetToStreamAsync(url, stream);
        }
    }
}