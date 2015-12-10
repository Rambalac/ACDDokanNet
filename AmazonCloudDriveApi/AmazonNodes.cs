using AmazonCloudDriveApi;
using Azi.Amazon.CloudDrive.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Azi.Amazon.CloudDrive
{

    public class AmazonNodes
    {
        private readonly AmazonDrive amazon;
        JsonHttp json => amazon.json;
        static TimeSpan generalExpiration => AmazonDrive.generalExpiration;

        public AmazonNodes(AmazonDrive amazonDrive)
        {
            this.amazon = amazonDrive;
        }

        public async Task<IList<AmazonChild>> GetChildren(string id = null)
        {
            var url = (id != null) ? "{0}/nodes/{1}/children" : "{0}/nodes?filters=isRoot:true";
            var children = await json.GetAsync<Children>(url, await amazon.GetMetadataUrl(), id);
            return children.data;
        }
    }
}