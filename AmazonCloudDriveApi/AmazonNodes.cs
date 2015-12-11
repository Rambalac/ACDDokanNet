using AmazonCloudDriveApi;
using Azi.Amazon.CloudDrive.Json;
using Azi.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Azi.Amazon.CloudDrive
{

    public class AmazonNodes
    {
        private readonly AmazonDrive amazon;
        HttpClient json => amazon.http;
        static TimeSpan generalExpiration => AmazonDrive.generalExpiration;

        public AmazonNodes(AmazonDrive amazonDrive)
        {
            this.amazon = amazonDrive;
        }

        public async Task<AmazonChild> GetNode(string id)
        {
            var url = "{0}/nodes/{1}";
            var result = await json.GetJsonAsync<AmazonChild>(string.Format(url, await amazon.GetMetadataUrl(), id));
            return result;
        }

        public async Task<IList<AmazonChild>> GetChildren(string id = null)
        {
            if (id == null) id = (await GetRoot()).id;
            var url = "{0}/nodes/{1}/children";
            var children = await json.GetJsonAsync<Children>(string.Format(url, await amazon.GetMetadataUrl(), id));
            return children.data;
        }

        readonly static Regex filterEscapeChars = new Regex("[ \\+\\-&|!(){}[\\]^'\"~\\*\\?:\\\\]");
        private string MakeNameFilter(string name)
        {
            return "name:" + filterEscapeChars.Replace(name, "\\$0");
        }

        private string MakeParentFilter(string id)
        {
            return "parents:" + id;
        }

        public async Task<AmazonChild> GetChild(string parentid, string name)
        {
            if (parentid == null) parentid = (await GetRoot()).id;
            var url = string.Format("{0}/nodes?filters={1} AND {2}", await amazon.GetMetadataUrl(), MakeParentFilter(parentid), MakeNameFilter(name));
            var result = await json.GetJsonAsync<Children>(url);
            if (result.count == 0) return null;
            return result.data[0];
        }

        AmazonChild root;
        private async Task<AmazonChild> GetRoot()
        {
            if (root != null) return root;

            var url = "{0}/nodes?filters=isRoot:true";
            var result = await json.GetJsonAsync<Children>(string.Format(url, await amazon.GetMetadataUrl()));
            if (result.count == 0) return null;
            return result.data[0];
        }
    }
}