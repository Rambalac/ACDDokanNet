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
    public class FileUpload
    {
        public Stream Stream;
        public Dictionary<string, string> Parameters;
        public string FormName;
        public string FileName;
    }
    public class HttpClient
    {
        Func<System.Net.Http.HttpClient, Task> settingsSetter;
        const int retryTimes = 100;

        TimeSpan retryDelay(int time)
        {
            return TimeSpan.FromSeconds(1 << time);
        }

        public HttpClient(Func<System.Net.Http.HttpClient, Task> settingsSetter)
        {
            this.settingsSetter = settingsSetter;
        }
        public async Task<System.Net.Http.HttpClient> GetHttpClient()
        {
            var result = new System.Net.Http.HttpClient(new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip,
                PreAuthenticate = true,
                UseDefaultCredentials = true
            });
            await settingsSetter(result);
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
            Log.Error($"Download failed: {ex}");

            throw ex;
        }

        public async Task<T> GetJsonAsync<T>(string url)
        {
            T result = default(T);
            await Retry.Do(retryTimes, retryDelay, async () =>
            {
                using (var client = await GetHttpClient())
                {
                    var response = await client.GetAsync(url);
                    if (!response.IsSuccessStatusCode)
                    {
                        await LogBadResponse(response);
                        return !retryCodes.Contains(response.StatusCode);
                    }
                    result = await response.Content.ReadAsAsync<T>();
                    return true;
                }
            }, GeneralExceptionProcessor);
            return result;
        }

        public async Task GetToStreamAsync(string url, Stream stream, long? fileOffset = null, long? length = null, int bufferSize = 4096, Func<long, long> progress = null)
        {
            var start = DateTime.UtcNow;
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
                    if (!response.IsSuccessStatusCode)
                    {
                        await LogBadResponse(response);
                        return !retryCodes.Contains(response.StatusCode);
                    }

                    Stream input = await response.Content.ReadAsStreamAsync();

                    byte[] buff = new byte[Math.Min(bufferSize, response.Content.Headers.ContentLength ?? long.MaxValue)];
                    int red;
                    long nextProgress = -1;
                    bool first = true;
                    while ((red = await input.ReadAsync(buff, 0, buff.Length)) > 0)
                    {
                        if (first) Log.Trace("File first response: " + (DateTime.UtcNow - start).TotalMilliseconds);
                        first = false;
                        await stream.WriteAsync(buff, 0, red);
                        if (progress != null && input.Position >= nextProgress)
                        {
                            nextProgress = progress(input.Position);
                        }
                    }
                    if (nextProgress == -1) progress(0);
                }
                return true;
            }, GeneralExceptionProcessor);
        }

        public async Task GetToStreamAsync(string url, Func<Stream, Task> streammer, long? fileOffset = null, long? length = null)
        {
            var start = DateTime.UtcNow;
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
                    if (!response.IsSuccessStatusCode)
                    {
                        await LogBadResponse(response);
                        return !retryCodes.Contains(response.StatusCode);
                    }

                    Stream input = await response.Content.ReadAsStreamAsync();
                    await streammer(input);
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
                    if (!response.IsSuccessStatusCode)
                    {
                        await LogBadResponse(response);
                        return !retryCodes.Contains(response.StatusCode);
                    }

                    result = await response.Content.ReadAsAsync<T>();
                    return true;
                }
            }, GeneralExceptionProcessor);
            return result;
        }

        public async Task<R> Send<P, R>(HttpMethod method, string url, P obj)
        {
            return await Send(method, url, obj, (r) => r.Content.ReadAsAsync<R>());
        }

        public async Task<R> Send<R>(HttpMethod method, string url)
        {
            return await Send(method, url, (r) => r.Content.ReadAsAsync<R>());
        }

        public async Task<R> Send<P, R>(HttpMethod method, string url, P obj, Func<HttpResponseMessage, Task<R>> responseParser)
        {
            R result = default(R);
            await Retry.Do(retryTimes, retryDelay, async () =>
            {
                using (var client = await GetHttpClient())
                {
                    var request = new HttpRequestMessage(method, url);
                    var data = JsonConvert.SerializeObject(obj);
                    var content = new StringContent(data);
                    request.Content = content;

                    var response = await client.SendAsync(request);
                    if (!response.IsSuccessStatusCode)
                    {
                        await LogBadResponse(response);
                        return !retryCodes.Contains(response.StatusCode);
                    }

                    result = await responseParser(response);
                    return true;
                }
            }, GeneralExceptionProcessor);
            return result;
        }

        public async Task<R> Send<R>(HttpMethod method, string url, Func<HttpResponseMessage, Task<R>> responseParser)
        {
            R result = default(R);
            await Retry.Do(retryTimes, retryDelay, async () =>
            {
                using (var client = await GetHttpClient())
                {
                    var request = new HttpRequestMessage(method, url);

                    var response = await client.SendAsync(request);
                    if (!response.IsSuccessStatusCode)
                    {
                        await LogBadResponse(response);
                        return !retryCodes.Contains(response.StatusCode);
                    }

                    result = await responseParser(response);
                    return true;
                }
            }, GeneralExceptionProcessor);
            return result;
        }

        public async Task<T> SendFile<T>(HttpMethod method, string url, FileUpload file)
        {
            T result = default(T);
            long pos = file.Stream.Position;
            await Retry.Do(retryTimes, retryDelay, async () =>
            {
                using (var client = await GetHttpClient())
                {
                    HttpRequestMessage message = new HttpRequestMessage(method, url);
                    var content = new MultipartFormDataContent();
                    if (file.Parameters != null)
                    {
                        foreach (var pair in file.Parameters) content.Add(new StringContent(pair.Value), pair.Key);
                    }
                    file.Stream.Position = pos;
                    var str = new StreamContent(file.Stream);
                    str.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
                    content.Add(str, file.FormName, file.FileName);

                    message.Content = content;
                    var response = await client.SendAsync(message);
                    if (!response.IsSuccessStatusCode)
                    {
                        await LogBadResponse(response);
                        return !retryCodes.Contains(response.StatusCode);
                    }

                    result = await response.Content.ReadAsAsync<T>();
                    return true;
                }
            }, GeneralExceptionProcessor);
            return result;
        }

        private async Task LogBadResponse(HttpResponseMessage response)
        {
            try
            {
                var message = await response.Content.ReadAsStringAsync();
                Log.Warn("Response code: " + response.StatusCode + "\r\n" + message);

            }
            catch (Exception)
            {
                Log.Warn("Response code: " + response.StatusCode);
            }
        }
    }
}
