using AmazonCloudDriveApi.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azi.Amazon.CloudDrive;
using Azi.Tools;

namespace Azi.Amazon.CloudDrive
{
    public class AmazonAccount
    {
        Endpoint _endpoint;
        Quota _quota;
        Usage _usage;

        static readonly TimeSpan endpointExpiration = TimeSpan.FromDays(3);
        private readonly AmazonDrive amazon;
        HttpClient json => amazon.http;
        static TimeSpan generalExpiration => AmazonDrive.generalExpiration;

        internal AmazonAccount(AmazonDrive amazonDrive)
        {
            this.amazon = amazonDrive;
        }

        public async Task<Endpoint> GetEndpoint()
        {
            if (_endpoint == null || DateTime.UtcNow - _endpoint.lastCalculated > endpointExpiration)
            {
                _endpoint = await json.GetJsonAsync<Endpoint>("https://drive.amazonaws.com/drive/v1/account/endpoint");
            }
            return _endpoint;
        }

        public async Task<Quota> GetQuota()
        {
            if (_quota == null || DateTime.UtcNow - _quota.lastCalculated > generalExpiration)
            {
                _quota = await json.GetJsonAsync<Quota>(string.Format("{0}/account/quota", await amazon.GetMetadataUrl()));
            }
            return _quota;
        }

        public async Task<Usage> GetUsage()
        {
            if (_usage == null || DateTime.UtcNow - _usage.lastCalculated > generalExpiration)
            {
                _usage = await json.GetJsonAsync<Usage>(string.Format("{0}/account/usage", await amazon.GetMetadataUrl()));
            }
            return _usage;
        }
    }
}
