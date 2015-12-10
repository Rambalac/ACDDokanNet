using AmazonCloudDriveApi;
using AmazonCloudDriveApi.Json;
using Azi.Amazon.CloudDrive.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
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
        string clientSecret;

        CloudDriveScope scope;
        Token token;

        internal readonly HttpClient http;
        public readonly AmazonAccount Account;
        public readonly AmazonFolders Folders;
        public readonly AmazonFiles Files;

        public AmazonDrive()
        {
            http = new HttpClient(HeadersSetter);
            Account = new AmazonAccount(this);
            Folders = new AmazonFolders(this);
            Files = new AmazonFiles(this);
        }


        private static string BuildLoginUrl(string clientId, string redirectUrl, CloudDriveScope scope)
        {
            Contract.Assert(redirectUrl != null);

            return $"{loginUrlBase}?client_id={clientId}&scope={ScopeToString(scope)}&response_type=code&redirect_uri={redirectUrl}";

        }

        internal async Task<string> GetContentUrl() => (await Account.GetEndpoint()).contentUrl;
        internal async Task<string> GetMetadataUrl() => (await Account.GetEndpoint()).metadataUrl;

        private async Task HeadersSetter(HttpRequestHeaders headers)
        {
            headers.Add("Authorization", "Bearer " + await GetToken());
        }

        private async Task<string> GetToken()
        {
            return token.access_token;
        }

        private async Task UpdateToken()
        {
            var form = new Dictionary<string, string>
                    {
                        {"grant_type","refresh_token" },
                        {"refresh_token",token.refresh_token},
                        {"client_id",clientId},
                        {"client_secret",""}
                    };
            token = await http.PostForm<Token>("https://api.amazon.com/auth/o2/token", form);

        }

        public async Task SafeAuthenticationAsync(string clientId, string secret, CloudDriveScope scope, TimeSpan timeout)
        {
            using (var listener = new HttpListener())
            {
                var random = new Random();
                int port = 0;
                string redirectUrl = null;

                if (!Retry.Do(10, () =>
                {
                    try
                    {
                        port = random.Next(10000, 65000);
                        redirectUrl = $"http://localhost:{port}/";
                        listener.Prefixes.Add(redirectUrl);
                        return true;
                    }
                    catch (HttpListenerException)
                    {
                        return false;
                    }
                })) throw new InvalidOperationException("Cannot select port for redirect url");

                listener.Start();
                System.Diagnostics.Process.Start(BuildLoginUrl(clientId, redirectUrl, scope));
                var task = listener.GetContextAsync();
                if (await Task.WhenAny(task, Task.Delay(timeout)) == task)
                {
                    var context = await task;
                    var code = context.Request.Url.ParseQueryString()["code"];

                    var form = new Dictionary<string, string>
                    {
                        { "grant_type","authorization_code" },
                        {"code ",code},
                        {"client_id",clientId},
                        {"client_secret",secret},
                        {"redirect_uri ",redirectUrl}
                    };
                    token = await http.PostForm<Token>("https://api.amazon.com/auth/o2/token", form);


                    await Account.GetEndpoint();


                    this.clientId = clientId;
                    this.clientSecret = secret;
                    this.scope = scope;
                }
                else
                {
                    listener.Abort();
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
