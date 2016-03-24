using Azi.Cloud.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Net;
using System.Threading;

namespace MicrosoftOneDrive
{
    public class MicrosoftOneDrive : IHttpCloud, IHttpCloudFiles, IHttpCloudNodes
    {
        public static string CloudServiceName => "Microsoft OneDrive";

        public static string CloudServiceIcon => "/Clouds.AmazonCloudDrive;Component/images/cd_icon.png";
 
        public long AvailableFreeSpace
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        string IHttpCloud.CloudServiceIcon => CloudServiceIcon;

        string IHttpCloud.CloudServiceName => CloudServiceName;

        public IHttpCloudFiles Files
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public string Name
        {
            get
            {
                throw new NotImplementedException();
            }

            set
            {
                throw new NotImplementedException();
            }
        }

        public IHttpCloudNodes Nodes
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public IAuthUpdateListener OnAuthUpdated
        {
            get
            {
                throw new NotImplementedException();
            }

            set
            {
                throw new NotImplementedException();
            }
        }

        public long TotalFreeSpace
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public long TotalSize
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public long TotalUsedSpace
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public Task<bool> AuthenticateNew(CancellationToken cs)
        {
            throw new NotImplementedException();
        }

        public Task<bool> AuthenticateSaved(CancellationToken cs, string save)
        {
            throw new NotImplementedException();
        }

        public Task<FSItem.Builder> CreateFolder(string parentid, string name)
        {
            throw new NotImplementedException();
        }

        public Task Download(string id, Func<HttpWebResponse, Task> streammer, long? fileOffset = default(long?), int? length = default(int?))
        {
            throw new NotImplementedException();
        }

        public Task<int> Download(string id, byte[] result, int offset, long pos, int left)
        {
            throw new NotImplementedException();
        }

        public Task<FSItem.Builder> GetChild(string id, string name)
        {
            throw new NotImplementedException();
        }

        public Task<IList<FSItem.Builder>> GetChildren(string id)
        {
            throw new NotImplementedException();
        }

        public Task<object> GetNodeExtended(string id)
        {
            throw new NotImplementedException();
        }

        public Task<FSItem.Builder> GetRoot()
        {
            throw new NotImplementedException();
        }

        public Task<FSItem.Builder> Move(string itemId, string oldParentId, string newParentId)
        {
            throw new NotImplementedException();
        }

        public Task<FSItem.Builder> Overwrite(string id, Func<FileStream> p)
        {
            throw new NotImplementedException();
        }

        public Task Remove(string id1, string id2)
        {
            throw new NotImplementedException();
        }

        public Task<FSItem.Builder> Rename(string id, string newName)
        {
            throw new NotImplementedException();
        }

        public Task Trash(string id)
        {
            throw new NotImplementedException();
        }

        public Task<FSItem.Builder> UploadNew(string parentId, string fileName, Func<FileStream> p)
        {
            throw new NotImplementedException();
        }
    }
}
