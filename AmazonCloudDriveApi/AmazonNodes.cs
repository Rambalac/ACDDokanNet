using Azi.Amazon.CloudDrive.JsonObjects;
using Azi.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HttpClient = Azi.Tools.HttpClient;

namespace Azi.Amazon.CloudDrive
{

    public class AmazonNodes
    {
        private readonly AmazonDrive amazon;
        private HttpClient http => amazon.http;

        public AmazonNodes(AmazonDrive amazonDrive)
        {
            this.amazon = amazonDrive;
        }

        public async Task<AmazonNode> GetNode(string id)
        {
            var url = "{0}nodes/{1}";
            var result = await http.GetJsonAsync<AmazonNode>(string.Format(url, await amazon.GetMetadataUrl(), id));
            return result;
        }

        public async Task<IList<AmazonNode>> GetChildren(string id = null)
        {
            if (id == null) id = (await GetRoot()).id;
            var url = string.Format("{0}nodes/{1}/children", await amazon.GetMetadataUrl(), id);
            var children = await http.GetJsonAsync<Children>(url);
            return children.data.Where(n => n.parents.Contains(id)).ToList(); // Hack for wrong Amazon output when file location was changed recently
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

        private string MakeMD5Filter(string md5)
        {
            return "contentProperties.md5:" + md5;
        }

        public async Task<AmazonNode> GetChild(string parentid, string name)
        {
            if (parentid == null) parentid = (await GetRoot()).id;
            var url = string.Format("{0}nodes?filters={1} AND {2}", await amazon.GetMetadataUrl(), MakeParentFilter(parentid), MakeNameFilter(name));
            var result = await http.GetJsonAsync<Children>(url);
            if (result.count == 0) return null;
            if (result.count != 1) throw new InvalidOperationException("Duplicated node name");

            if (!result.data[0].parents.Contains(parentid)) return null; // Hack for wrong Amazon output when file location was changed recently

            return result.data[0];
        }

        public async Task Add(string parentid, string nodeid)
        {
            var url = string.Format("{0}/nodes/{1}/children/{2}", await amazon.GetMetadataUrl(), parentid, nodeid);
            await http.Send<object>(HttpMethod.Put, url);
        }

        public async Task Remove(string parentid, string nodeid)
        {
            var url = string.Format("{0}/nodes/{1}/children/{2}", await amazon.GetMetadataUrl(), parentid, nodeid);
            await http.Send<object>(HttpMethod.Delete, url);
        }

        public async Task Trash(string id)
        {
            var url = string.Format("{0}trash/{1}", await amazon.GetMetadataUrl(), id);

            await http.Send<object>(HttpMethod.Put, url);
        }

        public async Task<AmazonNode> CreateFolder(string parentId, string name)
        {
            var url = string.Format("{0}nodes", await amazon.GetMetadataUrl());
            var folder = new NewChild { name = name, parents = new string[] { parentId }, kind = "FOLDER" };
            return await http.Post<NewChild, AmazonNode>(url, folder);
        }

        AmazonNode root;
        public async Task<AmazonNode> GetRoot()
        {
            if (root != null) return root;

            var url = "{0}nodes?filters=isRoot:true";
            var result = await http.GetJsonAsync<Children>(string.Format(url, await amazon.GetMetadataUrl()));
            if (result.count == 0) return null;
            root = result.data[0];
            if (root == null) throw new InvalidOperationException("Could not retrieve root");
            return root;
        }

        public async Task<AmazonNode> Rename(string id, string newName)
        {
            var url = "{0}nodes/{1}";
            var data = new
            {
                name = newName
            };
            return await http.Patch<object, AmazonNode>(string.Format(url, await amazon.GetMetadataUrl(), id), data);
        }

        public async Task<AmazonNode> Move(string id, string oldDirId, string newDirId)
        {
            var url = "{0}nodes/{1}/children";
            var data = new
            {
                fromParent = oldDirId,
                childId = id
            };
            return await http.Post<object, AmazonNode>(string.Format(url, await amazon.GetMetadataUrl(), newDirId), data);
        }

        public async Task<AmazonNode> GetNodeByMD5(string md5)
        {
            var url = string.Format("{0}nodes?filters={1}", await amazon.GetMetadataUrl(), MakeMD5Filter(md5));
            var result = await http.GetJsonAsync<Children>(url);
            if (result.count == 0) return null;
            return result.data[0];
        }
    }
}