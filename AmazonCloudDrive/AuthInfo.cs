using Azi.Amazon.CloudDrive;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Azi.Amazon.CloudDrive.JsonObjects;
using System.Collections.Concurrent;
using System.Net;
using Azi.Cloud.Common;
using Azi.Tools;
using Cloud.Common;
using System.Threading;
using Newtonsoft.Json;

namespace Azi.Cloud.DokanNet.AmazonCloudDrive
{
    class AuthInfo
        {
            public string AuthRenewToken { get; internal set; }
            public string AuthToken { get; internal set; }
            public DateTime AuthTokenExpiration { get; internal set; }
        }
}