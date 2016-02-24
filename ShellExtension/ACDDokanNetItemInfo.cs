using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace ShellExtension
{
    public class ACDDokanNetAssetInfo
    {
        public string WebLink => "https://www.amazon.com/clouddrive/folder/" + Id;

        public string Id { get; set; }

        public string TempLink { get; set; }
    }

    public class ACDDokanNetItemInfo : ACDDokanNetAssetInfo
    {
        public IList<ACDDokanNetAssetInfo> Assets { get; set; }
    }
}