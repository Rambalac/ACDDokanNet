using Azi.Amazon.CloudDrive;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace amazon_clouddrive_dokan
{
    public class FSProvider
    {
        readonly Dictionary<string, CloudItem> mappedToFile = new Dictionary<string, CloudItem>();

        AmazonDrive amazon;

        public long AvailableFreeSpace { get; internal set; }
        public long TotalSize { get; internal set; }
        public long TotalFreeSpace { get; internal set; }

        public void DeleteFile(string fileName)
        {
            throw new NotImplementedException();
        }

        public bool Exists(string fileName)
        {
            throw new NotImplementedException();
        }

        public void CreateDir(string fileName)
        {
            throw new NotImplementedException();
        }

        public Stream Open(FileMode mode, FileAccess fileAccess, FileShare share, FileOptions options)
        {
            throw new NotImplementedException();
        }

        public void CreateFile(string fileName)
        {
            throw new NotImplementedException();
        }

        public void DeleteDir(string fileName)
        {
            throw new NotImplementedException();
        }

        public IList<CloudItem> GetDirItems(string fileName)
        {
            throw new NotImplementedException();
        }

        public CloudItem GetItem(string fileName)
        {
            throw new NotImplementedException();
        }

        public void MoveFile(string oldName, string newName, bool replace)
        {
            throw new NotImplementedException();
        }
    }
}
