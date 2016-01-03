using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.Serialization;

namespace Azi.Tools
{
    public class FileUpload
    {
        public Func<Stream> StreamOpener;
        public Dictionary<string, string> Parameters;
        public string FormName;
        public string FileName;

        public int Timeout = 30000;
    }

    [Serializable]
    public class HttpWebException : Exception
    {
        public readonly HttpStatusCode StatusCode;
        public HttpWebException(string message, HttpStatusCode code) : base(message)
        {
            this.StatusCode = code;
        }
        public HttpWebException(string message, HttpStatusCode code, Exception e) : base(message, e)
        {
            this.StatusCode = code;
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
        }
    }

    public static class HttpWebRequestExtensions
    {
        static readonly HttpStatusCode[] successStatusCodes = { HttpStatusCode.OK, HttpStatusCode.Created, HttpStatusCode.PartialContent };
        public static bool IsSuccessStatusCode(this HttpWebResponse response) => successStatusCodes.Contains(response.StatusCode);
        public static async Task<string> ReadAsStringAsync(this HttpWebResponse response)
        {
            using (var reader = new StreamReader(response.GetResponseStream()))
            {
                return await reader.ReadToEndAsync();
            }
        }

        public static async Task<T> ReadAsAsync<T>(this HttpWebResponse response)
        {
            var text = await response.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<T>(text);
        }

        public static ContentRangeHeaderValue GetContentRange(this WebHeaderCollection headers)
        {
            return ContentRangeHeaderValue.Parse(headers["Content-Range"]);
        }
    }

    public class HttpClient
    {
        Func<HttpWebRequest, Task> settingsSetter;
        const int retryTimes = 100;

        TimeSpan retryDelay(int time)
        {
            return TimeSpan.FromSeconds(1 << time);
        }

        public HttpClient(Func<HttpWebRequest, Task> settingsSetter)
        {
            this.settingsSetter = settingsSetter;
        }
        private async Task<HttpWebRequest> GetHttpClient(string url)
        {
            var result = (HttpWebRequest)WebRequest.Create(url);

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

        /// <summary>
        /// Return false to continue
        /// </summary>
        /// <param name="ex"></param>
        /// <returns></returns>
        static bool GeneralExceptionProcessor(Exception ex)
        {
            Log.Error($"HttpClient failed: {ex}");
            if (ex is TaskCanceledException) return false;

            var webex = SearchForException<WebException>(ex);
            if (webex != null)
            {

                var webresp = webex.Response as HttpWebResponse;
                if (webresp != null)
                {
                    if (retryCodes.Contains(webresp.StatusCode)) return false;
                    throw new HttpWebException(webex.Message, webresp.StatusCode);
                }
            }
            throw ex;
        }

        public async Task<T> GetJsonAsync<T>(string url)
        {
            return await Send<T>(HttpMethod.Get, url);
                    }

        public async Task GetToStreamAsync(string url, Stream stream, long? fileOffset = null, long? length = null, int bufferSize = 4096, Func<long, long> progress = null)
        {
            var start = DateTime.UtcNow;
            await GetToStreamAsync(url, async (response) =>
            {
                using (Stream input = response.GetResponseStream())
                {
                    byte[] buff = new byte[Math.Min(bufferSize, (response.ContentLength != -1) ? response.ContentLength : long.MaxValue)];
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
                            nextProgress = progress.Invoke(input.Position);
                        }
                    }
                    if (nextProgress == -1) progress?.Invoke(0);
                }
            }, fileOffset, length);
        }

        public async Task GetToStreamAsync(string url, Func<HttpWebResponse, Task> streammer, long? fileOffset = null, long? length = null)
        {
            await Retry.Do(retryTimes, retryDelay, async () =>
            {
                var client = await GetHttpClient(url);
                if (fileOffset != null && length != null)
                    client.AddRange((long)fileOffset, (long)(fileOffset + length - 1));
                else
                    if (fileOffset != null && length == null)
                    client.AddRange((long)fileOffset);
                client.Method = "GET";

                using (var response = (HttpWebResponse)await client.GetResponseAsync())
                    {
                    if (!response.IsSuccessStatusCode())
                    {
                        return await LogBadResponse(response);
                    }

                    await streammer(response);
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
                var client = await GetHttpClient(url);
                client.Method = method.ToString();
                    var content = new FormUrlEncodedContent(pars);
                client.ContentType = content.Headers.ContentType.ToString();

                using (var output = await client.GetRequestStreamAsync())
                {
                    await content.CopyToAsync(output);
                }

                using (var response = (HttpWebResponse)await client.GetResponseAsync())
                {
                    if (!response.IsSuccessStatusCode())
                    {
                        return await LogBadResponse(response);
                    }

                    result = await response.ReadAsAsync<T>();
                }
                    return true;
            }, GeneralExceptionProcessor);
            return result;
        }

        public async Task<R> Send<P, R>(HttpMethod method, string url, P obj)
        {
            return await Send(method, url, obj, (r) => r.ReadAsAsync<R>());
        }

        public async Task<R> Send<R>(HttpMethod method, string url)
        {
            return await Send(method, url, (r) => r.ReadAsAsync<R>());
        }

        public async Task<R> Send<P, R>(HttpMethod method, string url, P obj, Func<HttpWebResponse, Task<R>> responseParser)
        {
            R result = default(R);
            await Retry.Do(retryTimes, retryDelay, async () =>
            {
                var client = await GetHttpClient(url);
                client.Method = method.ToString();
                    var data = JsonConvert.SerializeObject(obj);
                    var content = new StringContent(data);
                client.ContentType = content.Headers.ContentType.ToString();

                using (var output = await client.GetRequestStreamAsync())
                {
                    await content.CopyToAsync(output);
                }

                using (var response = (HttpWebResponse)await client.GetResponseAsync())
                {
                    if (!response.IsSuccessStatusCode())
                    {
                        return await LogBadResponse(response);
                    }

                    result = await responseParser(response);
                }
                    return true;
            }, GeneralExceptionProcessor);
            return result;
        }

        public async Task<R> Send<R>(HttpMethod method, string url, Func<HttpWebResponse, Task<R>> responseParser)
        {
            R result = default(R);
            await Retry.Do(retryTimes, retryDelay, async () =>
            {
                var client = await GetHttpClient(url);
                client.Method = method.ToString();

                using (var response = (HttpWebResponse)await client.GetResponseAsync())
                {
                    if (!response.IsSuccessStatusCode())
                    {
                        return await LogBadResponse(response);
                    }

                    result = await responseParser(response);
                }
                    return true;
            }, GeneralExceptionProcessor);
            return result;
        }

        static Encoding UTF8 => new UTF8Encoding(false, true);

        private Stream GetMultipartFormPre(FileUpload file, long filelength, string boundry)
        {
            var result = new MemoryStream(1000);
            using (var writer = new StreamWriter(result, UTF8, 16, true))
            {
                foreach (var pair in file.Parameters)
                {
                    writer.Write($"--{boundry}\r\n");
                    writer.Write($"Content-Disposition: form-data; name=\"{pair.Key}\"\r\n\r\n{pair.Value}\r\n");
                }

                writer.Write($"--{boundry}\r\n");
                writer.Write($"Content-Disposition: form-data; name=\"{file.FormName}\"; filename={file.FileName}\r\n");
                writer.Write($"Content-Type: application/octet-stream\r\n");

                writer.Write($"Content-Length: {filelength}\r\n\r\n");
            }
            result.Position = 0;
            return result;
        }

        private Stream GetMultipartFormPost(FileUpload file, string boundry)
        {
            var result = new MemoryStream(1000);
            using (var writer = new StreamWriter(result, UTF8, 16, true))
            {
                writer.Write($"\r\n--{boundry}--\r\n");
            }
            result.Position = 0;
            return result;
        }

        public async Task<T> SendFile<T>(HttpMethod method, string url, FileUpload file)
        {
            T result = default(T);
            await Retry.Do(retryTimes, retryDelay, async () =>
            {
                var client = await GetHttpClient(url);
                client.Method = method.ToString();
                client.AllowWriteStreamBuffering = false;

                string boundry = Guid.NewGuid().ToString();
                client.ContentType = $"multipart/form-data; boundary={boundry}";
                client.SendChunked = true;

                using (var input = file.StreamOpener())
                {

                    var pre = GetMultipartFormPre(file, input.Length, boundry);
                    var post = GetMultipartFormPost(file, boundry);
                    client.ContentLength = pre.Length + input.Length + post.Length;
                    using (var output = await client.GetRequestStreamAsync())
                    {
                        await pre.CopyToAsync(output);
                        await input.CopyToAsync(output);
                        await post.CopyToAsync(output);
                    }
                }
                using (var response = (HttpWebResponse)await client.GetResponseAsync())
                {
                    if (!response.IsSuccessStatusCode())
                    {
                        return await LogBadResponse(response);
                    }

                    result = await response.ReadAsAsync<T>();
                }
                return true;
            }, GeneralExceptionProcessor);
            return result;
        }

        private async Task PushFile(Stream input, Stream output, int timeout)
        {
            using (input)
            using (output)
            {
                var buf = new byte[81920];
                int red;
                do
                {
                    red = await input.ReadAsync(buf, 0, buf.Length);
                    if (red == 0) break;
                    var cancellationSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(timeout));
                    await output.WriteAsync(buf, 0, red, cancellationSource.Token);
                    Log.Trace("Pushed byted: " + red);
                } while (red != 0);
            }
        }

        private async Task<bool> LogBadResponse(HttpWebResponse response)
        {
            try
            {
                var message = await response.ReadAsStringAsync();
                Log.Warn("Response code: " + response.StatusCode + "\r\n" + message);
                if (!retryCodes.Contains(response.StatusCode)) throw new HttpWebException(message, response.StatusCode);
                return false;
            }
            catch (Exception e)
            {
                Log.Warn("Response code: " + response.StatusCode);
                throw new HttpWebException(e.Message, response.StatusCode, e);
            }
        }
    }
}
