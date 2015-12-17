using Azi.Amazon.CloudDrive.Json;
using Azi.Tools;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

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
        AuthToken token;

        internal readonly Tools.HttpClient http;
        public readonly AmazonAccount Account;
        public readonly AmazonNodes Nodes;
        public readonly AmazonFiles Files;

        public AmazonDrive()
        {
            http = new Tools.HttpClient(HeadersSetter);
            Account = new AmazonAccount(this);
            Nodes = new AmazonNodes(this);
            Files = new AmazonFiles(this);
        }


        private static string BuildLoginUrl(string clientId, string redirectUrl, CloudDriveScope scope)
        {
            Contract.Assert(redirectUrl != null);

            return $"{loginUrlBase}?client_id={clientId}&scope={ScopeToString(scope)}&response_type=code&redirect_uri={redirectUrl}";

        }

        internal async Task<string> GetContentUrl() => (await Account.GetEndpoint()).contentUrl;
        internal async Task<string> GetMetadataUrl() => (await Account.GetEndpoint()).metadataUrl;

        readonly CacheControlHeaderValue standartCache = new CacheControlHeaderValue { NoCache = true };

        private async Task HeadersSetter(HttpRequestHeaders headers)
        {
            if (token != null)
                headers.Add("Authorization", "Bearer " + await GetToken());
            headers.CacheControl = standartCache;
            headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
            headers.UserAgent.Add(new ProductInfoHeaderValue("AZIACDDokanNet", this.GetType().Assembly.ImageRuntimeVersion));
        }

        private async Task<string> GetToken()
        {
            if (token == null) throw new InvalidOperationException("Not authenticated");
            if (token.IsExpired) await UpdateToken();
            return token?.access_token;
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
            token = await http.PostForm<AuthToken>("https://api.amazon.com/auth/o2/token", form);

        }

        static readonly Regex browserPathPattern = new Regex("^(?<path>[^\" ]+)|\"(?<path>[^\"]+)\" (?<args>.*)$");
        public Process OpenUrlInDefaultBrowser(string url)
        {
            using (var nameKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.html\UserChoice", false))
            {
                var appName = nameKey.GetValue("Progid") as string;
                using (var commandKey = Registry.ClassesRoot.OpenSubKey($@"{appName}\shell\open\command", false))
                {
                    var str = commandKey.GetValue(null) as string;
                    var m = browserPathPattern.Match(str);
                    if (!m.Success || !m.Groups["path"].Success) throw new InvalidOperationException("Can not find default browser path");
                    var path = m.Groups["path"].Value;
                    var args = m.Groups["args"].Value.Replace("%1", url);
                    return Process.Start(path, args);
                }
            }
        }

        public async Task SafeAuthenticationAsync(string clientId, string secret, CloudDriveScope scope, TimeSpan timeout)
        {
            using (var listener = new HttpListener())
            {
                int port = 45674;
                string redirectUrl = null;

                if (!Retry.Do(3, (time) =>
                {
                    try
                    {
                        redirectUrl = $"http://localhost:{port + time}/signin/";
                        listener.Prefixes.Add(redirectUrl);
                        return true;
                    }
                    catch (HttpListenerException)
                    {
                        return false;
                    }
                })) throw new InvalidOperationException("Cannot select port for redirect url");

                listener.Start();
                using (var tabProcess = Process.Start(BuildLoginUrl(clientId, redirectUrl, scope)))
                {
                    try
                    {
                        var task = listener.GetContextAsync();
                        if (await Task.WhenAny(task, Task.Delay(timeout)) == task)
                        {
                            await ProcessRedirect(await task, clientId, secret, redirectUrl);

                            this.clientId = clientId;
                            this.clientSecret = secret;
                            this.scope = scope;
                        }
                        else
                        {
                            throw new TimeoutException("No redirection detected");
                        }
                    }
                    finally
                    {
                        listener.Stop();
                        //tabProcess.Kill();
                    }
                }

            }
        }

        private async Task ProcessRedirect(HttpListenerContext context, string clientId, string secret, string redirectUrl)
        {
            ///signin/?error_description=Access+not+permitted.&error=access_denied
            var error = context.Request.Url.ParseQueryString()["error_description"];
            if (error != null)
            {
                throw new InvalidOperationException(error);
            }

            var code = context.Request.Url.ParseQueryString()["code"];

            await SendRedirectResponse(context.Response);

            var form = new Dictionary<string, string>
                                {
                                    { "grant_type","authorization_code" },
                                    {"code",code},
                                    {"client_id",clientId},
                                    {"client_secret",secret},
                                    {"redirect_uri",redirectUrl}
                                };
            token = await http.PostForm<AuthToken>("https://api.amazon.com/auth/o2/token", form);


            await Account.GetEndpoint();
        }

        readonly byte[] closeTabResponse = Encoding.UTF8.GetBytes("<SCRIPT>window.open('', '_parent','');window.close();</SCRIPT>You can close this tab");

        private async Task SendRedirectResponse(HttpListenerResponse response)
        {
            response.StatusCode = 200;
            response.ContentLength64 = closeTabResponse.Length;
            await response.OutputStream.WriteAsync(closeTabResponse, 0, closeTabResponse.Length);
            response.OutputStream.Close();
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
