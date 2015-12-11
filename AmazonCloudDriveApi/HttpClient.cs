using AmazonCloudDriveApi;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web.Helpers;

namespace Azi.Tools
{
    public class HttpClient
    {
        Func<HttpRequestHeaders, Task> headersSetter;
        const int retryTimes = 1;

        TimeSpan retryDelay(int time)
        {
            return TimeSpan.FromSeconds(1 << time);
        }

        public HttpClient(Func<HttpRequestHeaders, Task> headersSetter)
        {
            this.headersSetter = headersSetter;
        }
        public async Task<System.Net.Http.HttpClient> GetHttpClient()
        {
            var result = new System.Net.Http.HttpClient(new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip
            });
            await headersSetter(result.DefaultRequestHeaders);
            return result;
        }

        public async Task<T> GetJsonAsync<T>(string url)
        {
            T result = default(T);
            await Retry.Do(retryTimes, retryDelay, async () =>
            {
                using (var client = await GetHttpClient())
                {
                    var response = await client.GetAsync(url);
                    if (!response.IsSuccessStatusCode) return false;
                    result = await response.Content.ReadAsAsync<T>();
                    return true;
                }
            });
            return result;
        }

        public async Task GetToStreamAsync(string url, Stream stream, long? fileOffset = null, long? length = null)
        {
            using (var client = await GetHttpClient())
            {
                var request = new HttpRequestMessage
                {
                    RequestUri = new Uri(url),
                };
                if (fileOffset != null || length != null)
                    request.Headers.Range = new RangeHeaderValue(fileOffset, length);

                var response = await client.SendAsync(request);
                if (!response.IsSuccessStatusCode) throw new HttpRequestException(response.ReasonPhrase);

                await response.Content.CopyToAsync(stream);
            }
        }

        public async Task<int> GetToBufferAsync(string url, byte[] buffer, int bufferIndex, long fileOffset, int length)
        {
            var stream = new MemoryStream(buffer, bufferIndex, length);
            await GetToStreamAsync(url, stream, fileOffset, length);
            return (int)stream.Length;
        }

        public async Task<T> PostForm<T>(string url, Dictionary<string, string> pars)
        {
            T result = default(T);
            await Retry.Do(retryTimes, retryDelay, async () =>
            {
                using (var client = await GetHttpClient())
                {
                    var content = new FormUrlEncodedContent(pars);

                    var response = await client.PostAsync(url, content);
                    if (!response.IsSuccessStatusCode) return false;

                    result = await response.Content.ReadAsAsync<T>();
                    return true;
                }
            });
            return result;
        }

        public async Task<T> PostFile<T>(string url, Dictionary<string, string> pars, Stream stream)
        {
            using (var client = await GetHttpClient())
            {
                var content = new MultipartFormDataContent();
                if (pars != null)
                {
                    foreach (var pair in pars) content.Add(new StringContent(pair.Value), pair.Key);
                }
                content.Add(new StreamContent(stream));

                var response = await client.PostAsync(url, content);
                if (!response.IsSuccessStatusCode) return default(T);

                return await response.Content.ReadAsAsync<T>();
            }
        }



    }
}
