using AmazonCloudDriveApi;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

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
                    RequestUri = new Uri(url)
                };
                if (fileOffset != null || length != null)
                    request.Headers.Range = new RangeHeaderValue(fileOffset, fileOffset + length - 1);

                var response = await client.SendAsync(request);
                if (!response.IsSuccessStatusCode) throw new HttpRequestException(response.ReasonPhrase);

                await response.Content.CopyToAsync(stream);
            }
        }

        public async Task<int> GetToBufferAsync(string url, byte[] buffer, int bufferIndex, long fileOffset, int length)
        {
            using (var stream = new MemoryStream(buffer, bufferIndex, length))
            {
                await GetToStreamAsync(url, stream, fileOffset, length);
                return (int)stream.Position;
            }
        }

        public async Task<T> PostForm<T>(string url, Dictionary<string, string> pars)
        {
            return await SendForm<T>(HttpMethod.Post, url, pars);
        }

        public async Task<R> Patch<P, R>(string url, P obj)
        {
            return await Send<P, R>(new HttpMethod("PATCH"), url, obj);
        }

        public async Task<R> Post<P, R>(string url, P obj)
        {
            return await Send<P, R>(HttpMethod.Post, url, obj);
        }

        public async Task<T> SendForm<T>(HttpMethod method, string url, Dictionary<string, string> pars)
        {
            T result = default(T);
            await Retry.Do(retryTimes, retryDelay, async () =>
            {
                using (var client = await GetHttpClient())
                {
                    var content = new FormUrlEncodedContent(pars);
                    var request = new HttpRequestMessage(method, url);
                    request.Content = content;

                    var response = await client.SendAsync(request);
                    if (!response.IsSuccessStatusCode) return false;

                    result = await response.Content.ReadAsAsync<T>();
                    return true;
                }
            });
            return result;
        }

        public async Task<R> Send<P, R>(HttpMethod method, string url, P obj)
        {
            R result = default(R);
            await Retry.Do(retryTimes, retryDelay, async () =>
            {
                using (var client = await GetHttpClient())
                {
                    var data = JsonConvert.SerializeObject(obj);
                    var content = new StringContent(data);
                    var request = new HttpRequestMessage(method, url);
                    request.Content = content;

                    var response = await client.SendAsync(request);
                    if (!response.IsSuccessStatusCode) return false;

                    if (typeof(R) == typeof(string))
                        result = (R)(object)await response.Content.ReadAsStringAsync();
                    else
                        result = await response.Content.ReadAsAsync<R>();
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
