using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web.Helpers;

namespace Azi.Amazon.CloudDrive
{
    public class JsonHttp
    {
        Func<HttpRequestHeaders, Task> headersSetter;

        public JsonHttp(Func<HttpRequestHeaders, Task> headersSetter)
        {
            this.headersSetter = headersSetter;
        }
        private async Task<HttpClient> GetHttpClient()
        {
            var result = new HttpClient();
            await headersSetter(result.DefaultRequestHeaders);
            return result;
        }

        public async Task<dynamic> GetAsync(string url)
        {
            using (var client = await GetHttpClient())
            {
                var result = await client.GetStringAsync(url);
                return System.Web.Helpers.Json.Decode(result);
            }
        }

        public async Task<T> GetAsync<T>(string url, params string[] pars)
        {
            using (var client = await GetHttpClient())
            {
                var result = await client.GetStringAsync(string.Format(url, pars));
                return System.Web.Helpers.Json.Decode<T>(result);
            }
        }

    }
}
