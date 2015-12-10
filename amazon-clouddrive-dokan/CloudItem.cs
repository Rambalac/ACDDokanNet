using DokanNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Security.AccessControl;
using System.Globalization;
using System.Diagnostics;
using FileAccess = DokanNet.FileAccess;

namespace amazon_clouddrive_dokan
{
    public class CloudItem
    {
        public string path;
        public string Path => path;
        public bool isDir;
        public bool IsDir => isDir;

        public string Name
        {
            get
            {
                return System.IO.Path.GetFileName(path);
            }
        }

        internal CloudItem(string path, bool isDir)
        {
            this.path = path;
            this.isDir = isDir;
        }
    }
}