using AmazonCloudDriveApi;
using AmazonCloudDriveApi.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web.Helpers;

namespace Azi.Amazon.CloudDrive
{
    [Flags]
    public enum CloudDriveScope
    {
        ReadImage = 1,
        ReadVideo = 2,
        ReadDocument = 4,
        ReadOther = 8,
        ReadAll = 16,
        Write = 32
    }

    public class AmazonDrive
    {
        internal static readonly TimeSpan generalExpiration = TimeSpan.FromMinutes(5);
        const string loginUrlBase = "https://www.amazon.com/ap/oa";

        string clientId;
        CloudDriveScope scope;
        string refreshToken;
        string token;

        internal readonly JsonHttp json;
        public readonly AmazonAccount Account;
        public readonly AmazonNodes Nodes;

        public AmazonDrive()
        {
            json = new JsonHttp(HeadersSetter);
            Account = new AmazonAccount(this);
            Nodes = new AmazonNodes(this);
        }


        private static string BuildLoginUrl(string clientId, int port, CloudDriveScope scope)
        {
            return $"{loginUrlBase}?client_id={clientId}&scope={ScopeToString(scope)}&response_type=code&redirect_uri=http://localhost:{port}";

        }

        internal async Task<string> GetContentUrl() => (await Account.GetEndpoint()).contentUrl;
        internal async Task<string> GetMetadataUrl() => (await Account.GetEndpoint()).metadataUrl;

        private async Task HeadersSetter(HttpRequestHeaders headers)
        {
            headers.Add("Authorization", "Bearer " + await GetToken());
        }

        private async Task<string> GetToken()
        {
            return token;
        }


        public async Task SafeAuthenticationAsync(string clientId, CloudDriveScope scope, TimeSpan timeout)
        {
            using (var http = new HttpListener())
            {
                var random = new Random();
                int port = 0;
                if (!Retry.Do(10, () =>
                {
                    try
                    {
                        port = random.Next(10000, 65000);
                        http.Prefixes.Add($"http://localhost:{port}/");
                        return true;
                    }
                    catch (HttpListenerException)
                    {
                        return false;
                    }
                })) throw new InvalidOperationException("Cannot select port for redirect url");

                http.Start();
                System.Diagnostics.Process.Start(BuildLoginUrl(clientId, port, scope));
                var task = http.GetContextAsync();
                if (await Task.WhenAny(task, Task.Delay(timeout)) == task)
                {
                    var context = await task;

                    await Account.GetEndpoint();


                    this.clientId = clientId;
                    this.scope = scope;
                }
                else
                {
                    http.Abort();
                    throw new TimeoutException("No redirection detected");
                }
            }
        }

        static readonly Dictionary<CloudDriveScope, string> scopeToStringMap = new Dictionary<CloudDriveScope, string>
        {
            {CloudDriveScope.ReadImage,"clouddrive:read_image" },
            {CloudDriveScope.ReadVideo,"clouddrive:read_video" },
            {CloudDriveScope.ReadDocument,"clouddrive:read_document" },
            {CloudDriveScope.ReadOther,"clouddrive:read_other" },
            {CloudDriveScope.ReadAll,"clouddrive:read_all" },
            {CloudDriveScope.Write,"clouddrive:write" }
        };
        private static string ScopeToString(CloudDriveScope scope)
        {
            var result = new List<string>();
            var values = Enum.GetValues(typeof(CloudDriveScope));
            foreach (CloudDriveScope value in values)
                if (scope.HasFlag(value))
                    result.Add(scopeToStringMap[value]);
            return string.Join(" ", result);
        }

    }
}
