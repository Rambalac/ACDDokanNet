// <copyright file="HttpWebRequestExtensions.cs" company="Rambalac">
// Copyright (c) Rambalac. All rights reserved.
// </copyright>

namespace Azi.Tools
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http.Headers;
    using System.Threading.Tasks;
    using Newtonsoft.Json;

    /// <summary>
    /// Helper methods to work with Http protocol
    /// </summary>
    public static class HttpWebRequestExtensions
    {
        private static readonly HttpStatusCode[] SuccessStatusCodes = { HttpStatusCode.OK, HttpStatusCode.Created, HttpStatusCode.PartialContent };

        /// <summary>
        /// Returns ContentRange from headers.
        /// </summary>
        /// <param name="headers">Headers collection</param>
        /// <returns>ContentRange object</returns>
        public static ContentRangeHeaderValue GetContentRange(this WebHeaderCollection headers)
        {
            return ContentRangeHeaderValue.Parse(headers["Content-Range"]);
        }

        /// <summary>
        /// Check if response is successful
        /// </summary>
        /// <param name="response">Response to check</param>
        /// <returns>True if success</returns>
        public static bool IsSuccessStatusCode(this HttpWebResponse response) => SuccessStatusCodes.Contains(response.StatusCode);

        /// <summary>
        /// Returns object as parsed JSON from response.
        /// </summary>
        /// <typeparam name="T">Type of object to parse</typeparam>
        /// <param name="response">Response to parse</param>
        /// <returns>Parsed object</returns>
        public static async Task<T> ReadAsAsync<T>(this HttpWebResponse response)
        {
            var text = await response.ReadAsStringAsync().ConfigureAwait(false);
            return JsonConvert.DeserializeObject<T>(text);
        }

        /// <summary>
        /// Returns response as string.
        /// </summary>
        /// <param name="response">Response to read.</param>
        /// <returns>String of response.</returns>
        public static async Task<string> ReadAsStringAsync(this HttpWebResponse response)
        {
            var stream = response.GetResponseStream();
            if (stream == null)
            {
                throw new InvalidOperationException("stream is null");
            }

            using (var reader = new StreamReader(stream))
            {
                return await reader.ReadToEndAsync().ConfigureAwait(false);
            }
        }
    }
}