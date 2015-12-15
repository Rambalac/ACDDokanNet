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
        const int retryTimes = 100;

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
                AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip,
                PreAuthenticate = true,
                UseDefaultCredentials = true
            });
            await headersSetter(result.DefaultRequestHeaders);
            return result;
        }

        static T SearchForException<T>(Exception ex, int depth = 3) where T : class
        {
            T res = null;
            var cur = ex;
            for (int i = 0; i < depth; i++)
            {
                res = cur as T;
                if (res != null) return res;
                cur = ex.InnerException;
                if (cur == null) return null;
            }
            return null;
        }

        static readonly HashSet<HttpStatusCode> retryCodes = new HashSet<HttpStatusCode> { HttpStatusCode.ProxyAuthenticationRequired };

        static bool GeneralExceptionProcessor(Exception ex)
        {
            var webex = SearchForException<WebException>(ex);
            if (webex == null) return true;

            var webresp = webex.Response as HttpWebResponse;
            if (webresp == null) return true;

            if (retryCodes.Contains(webresp.StatusCode)) return false;

            return true;
        }

        public async Task<T> GetJsonAsync<T>(string url)
        {
            T result = default(T);
            await Retry.Do(retryTimes, retryDelay, async () =>
            {
                using (var client = await GetHttpClient())
                {
                    var response = await client.GetAsync(url);
                    if (!response.IsSuccessStatusCode) return !retryCodes.Contains(response.StatusCode);
                    result = await response.Content.ReadAsAsync<T>();
                    return true;
                }
            }, GeneralExceptionProcessor);
            return result;
        }

        public async Task GetToStreamAsync(string url, Stream stream, long? fileOffset = null, long? length = null)
        {
            await Retry.Do(retryTimes, retryDelay, async () =>
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
                    if (!response.IsSuccessStatusCode) return !retryCodes.Contains(response.StatusCode);

                    await response.Content.CopyToAsync(stream);
                }
                return true;
            }, GeneralExceptionProcessor);
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
                    if (!response.IsSuccessStatusCode) return !retryCodes.Contains(response.StatusCode);

                    result = await response.Content.ReadAsAsync<T>();
                    return true;
                }
            }, GeneralExceptionProcessor);
            return result;
        }

        public async Task<R> Send<P, R>(HttpMethod method, string url, P obj)
        {
            return await Send<P, R>(method, url, obj, (r) => r.Content.ReadAsAsync<R>());
        }

        public async Task<R> Send<P, R>(HttpMethod method, string url, P obj, Func<HttpResponseMessage, Task<R>> responseParser)
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
                    if (!response.IsSuccessStatusCode) return !retryCodes.Contains(response.StatusCode);

                    result = await responseParser(response);
                    return true;
                }
            }, GeneralExceptionProcessor);
            return result;
        }

        public async Task<T> PostFile<T>(string url, Dictionary<string, string> pars, Stream stream)
        {
            T result = default(T);
            await Retry.Do(retryTimes, retryDelay, async () =>
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
                    if (!response.IsSuccessStatusCode) return !retryCodes.Contains(response.StatusCode);

                    result = await response.Content.ReadAsAsync<T>();
                    return true;
                }
            }, GeneralExceptionProcessor);
            return result;
        }



    }
}
