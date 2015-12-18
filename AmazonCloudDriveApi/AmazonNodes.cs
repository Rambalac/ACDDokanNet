using Azi.Amazon.CloudDrive.JsonObjects;
using Azi.Tools;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HttpClient = Azi.Tools.HttpClient;

namespace Azi.Amazon.CloudDrive
{

    public class AmazonNodes
    {
        private readonly AmazonDrive amazon;
        HttpClient http => amazon.http;
        static TimeSpan generalExpiration => AmazonDrive.generalExpiration;

        public AmazonNodes(AmazonDrive amazonDrive)
        {
            this.amazon = amazonDrive;
        }

        public async Task<AmazonChild> GetNode(string id)
        {
            var url = "{0}nodes/{1}";
            var result = await http.GetJsonAsync<AmazonChild>(string.Format(url, await amazon.GetMetadataUrl(), id));
            return result;
        }

        public async Task<IList<AmazonChild>> GetChildren(string id = null)
        {
            if (id == null) id = (await GetRoot()).id;
            var url = string.Format("{0}nodes/{1}/children", await amazon.GetMetadataUrl(), id);
            var children = await http.GetJsonAsync<Children>(url);
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
            var url = string.Format("{0}nodes?filters={1} AND {2}", await amazon.GetMetadataUrl(), MakeParentFilter(parentid), MakeNameFilter(name));
            var result = await http.GetJsonAsync<Children>(url);
            if (result.count == 0) return null;
            return result.data[0];
        }

        public async Task Delete(string id)
        {
            var url = string.Format("{0}trash/{1}", await amazon.GetMetadataUrl(), id);

            await http.Send<object>(HttpMethod.Put, url);
        }

        public async Task<AmazonChild> CreateFolder(string parentId, string name)
        {
            var url = string.Format("{0}nodes", await amazon.GetMetadataUrl());
            var folder = new NewChild { name = name, parents = new string[] { parentId }, kind = "FOLDER" };
            return await http.Post<NewChild, AmazonChild>(url, folder);
        }

        AmazonChild root;
        public async Task<AmazonChild> GetRoot()
        {
            if (root != null) return root;

            var url = "{0}nodes?filters=isRoot:true";
            var result = await http.GetJsonAsync<Children>(string.Format(url, await amazon.GetMetadataUrl()));
            if (result.count == 0) return null;
            root = result.data[0];
            if (root == null) throw new InvalidOperationException("Could not retrieve root");
            return root;
        }

        public async Task<AmazonChild> Rename(string id, string newName)
        {
            var url = "{0}nodes/{1}";
            var data = new
            {
                name = newName
            };
            return await http.Patch<object, AmazonChild>(string.Format(url, await amazon.GetMetadataUrl(), id), data);
        }

        public async Task<AmazonChild> Move(string id, string oldDirId, string newDirId)
        {
            var url = "{0}nodes/{1}/children";
            var data = new
            {
                fromParent = oldDirId,
                childId = id
            };
            return await http.Post<object, AmazonChild>(string.Format(url, await amazon.GetMetadataUrl(), newDirId), data);
        }
    }
}