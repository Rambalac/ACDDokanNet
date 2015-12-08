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
    public abstract class CloudItem
    {
        public string NodeId;
        private string mappedPath;
        public abstract bool IsDir { get; }

        private string name;
        public string MappedPath
        {
            get
            {
                return mappedPath;
            }

            set
            {
                mappedPath = value;
                name = Path.GetFileName(value);
            }
        }

        public string Name
        {
            get
            {
                return name;
            }
        }
    }
    public class CloudFile : CloudItem
    {
        public string Nodeid;
        public string CachedPath;
        public FileStream Stream;

        public override bool IsDir => false;
    }

    public class CloudDir : CloudItem
    {
        public override bool IsDir => true;
        public List<CloudItem> Items { get; } = new List<CloudItem>();
    }

    public class CloudDrive : IDokanOperations
    {
        string cacheFolder;
        public CloudDrive(string cacheFolder)
        {
            if (Directory.Exists(cacheFolder)) Directory.Delete(cacheFolder, true);
            Directory.CreateDirectory(cacheFolder);
            this.cacheFolder = cacheFolder;
            mappedToDir["\\"] = new CloudDir();
        }

        readonly Dictionary<string, CloudFile> mappedToFile = new Dictionary<string, CloudFile>();
        readonly Dictionary<string, CloudDir> mappedToDir = new Dictionary<string, CloudDir>();

        public void Cleanup(string fileName, DokanFileInfo info)
        {
        }

        public void CloseFile(string fileName, DokanFileInfo info)
        {

            if (info.IsDirectory) throw new ArgumentException("CloseFile: Cannot be directory", nameof(info));
            var file = mappedToFile[fileName];
            file.Stream.Close();
            if (info.DeleteOnClose) DeleteFile(fileName);
        }

        private void DeleteFile(string fileName)
        {

            var parent = mappedToDir[Path.GetDirectoryName(fileName)];
            var file = mappedToFile[fileName];
            mappedToFile.Remove(fileName);
            parent.Items.Remove(file);
        }

        NtStatus _CreateDirectory(string fileName, DokanFileInfo info)
        {


            if (mappedToDir.ContainsKey(fileName)) return DokanResult.AlreadyExists;
            var parent = mappedToDir[Path.GetDirectoryName(fileName)];
            var dir = new CloudDir();
            parent.Items.Add(dir);
            mappedToDir.Add(fileName, dir);

            return DokanResult.Success;
        }

        FileAttributes? GetFlags(string path)
        {
            if (mappedToDir.ContainsKey(path)) return FileAttributes.Directory;
            if (mappedToFile.ContainsKey(path)) return FileAttributes.Normal;

            return null;
        }

        private const FileAccess DataAccess = FileAccess.ReadData | FileAccess.WriteData | FileAccess.AppendData |
                                      FileAccess.Execute |
                                      FileAccess.GenericExecute | FileAccess.GenericWrite | FileAccess.GenericRead;

        private const FileAccess DataWriteAccess = FileAccess.WriteData | FileAccess.AppendData |
                                                   FileAccess.Delete |
                                                   FileAccess.GenericWrite;

        public NtStatus CreateFile(string fileName, FileAccess access, FileShare share, FileMode mode, FileOptions options, FileAttributes attributes, DokanFileInfo info)
        {
            if (info.IsDirectory)
            {
                if (mode == FileMode.CreateNew) return _CreateDirectory(fileName, info);
                return DokanResult.Success;
            }

            var flags = GetFlags(fileName);
            bool pathExists = flags != null;
            bool pathIsDirectory = flags == FileAttributes.Directory;

            bool readWriteAttributes = (access & DataAccess) == 0;

            switch (mode)
            {
                case FileMode.Open:

                    if (!pathExists) return DokanResult.FileNotFound;
                    if (readWriteAttributes || pathIsDirectory)
                    // check if driver only wants to read attributes, security info, or open directory
                    {
                        info.IsDirectory = pathIsDirectory;
                        info.Context = new object();
                        // must set it to something if you return DokanError.Success

                        return DokanResult.Success;
                    }
                    break;

                case FileMode.CreateNew:
                    if (pathExists) return DokanResult.FileExists;
                    break;

                case FileMode.Truncate:
                    if (!pathExists) return DokanResult.FileNotFound;
                    break;
            }

            return _OpenFile(fileName, access, share, mode, options, attributes, info);

        }

        private NtStatus _OpenFile(string fileName, FileAccess access, FileShare share, FileMode mode, FileOptions options, FileAttributes attributes, DokanFileInfo info)
        {
            var file = mappedToFile[fileName];

            bool readAccess = (access & DataWriteAccess) == 0;

            info.Context = file.Stream = new FileStream(fileName, mode, readAccess ? System.IO.FileAccess.Read : System.IO.FileAccess.ReadWrite, share, 4096, options);
            return DokanResult.Success;
        }

        NtStatus _CreateFile(string fileName, DokanNet.FileAccess access, FileShare share, FileMode mode, FileOptions options, FileAttributes attributes, DokanFileInfo info)
        {

            var parent = mappedToDir[Path.GetDirectoryName(fileName)];

            var localId = Guid.NewGuid().ToString();
            var cachedPath = Path.Combine(cacheFolder, localId);

            var file = new CloudFile
            {
                Nodeid = localId,
                MappedPath = fileName,
                CachedPath = cachedPath
            };
            parent.Items.Add(file);

            mappedToFile.Add(fileName, file);

            return DokanResult.Success;
        }

        public NtStatus DeleteDirectory(string fileName, DokanFileInfo info)
        {

            var parent = mappedToDir[Path.GetDirectoryName(fileName)];
            var dir = mappedToDir[fileName];
            mappedToDir.Remove(fileName);
            parent.Items.Remove(dir);

            return DokanResult.Success;
        }

        public NtStatus DeleteFile(string fileName, DokanFileInfo info)
        {

            DeleteFile(fileName);

            return DokanResult.Success;
        }

        public NtStatus EnumerateNamedStreams(string fileName, IntPtr enumContext, out string streamName, out long streamSize, DokanFileInfo info)
        {

            streamName = String.Empty;
            streamSize = 0;
            return DokanResult.NotImplemented;
        }

        public NtStatus FindFiles(string fileName, out IList<FileInformation> files, DokanFileInfo info)
        {

            var parent = mappedToDir[fileName];

            files = parent.Items.Select(i => new FileInformation
            {
                FileName = i.Name,
                Attributes = i.IsDir ? FileAttributes.Directory : FileAttributes.Normal,
                LastAccessTime = DateTime.Now,
                LastWriteTime = DateTime.Now,
                CreationTime = DateTime.Now
            }).ToList();
            return DokanResult.Success;
        }

        public NtStatus FlushFileBuffers(string fileName, DokanFileInfo info)
        {

            var file = mappedToFile[fileName];
            file.Stream.Flush();

            return DokanResult.Success;
        }

        public NtStatus GetDiskFreeSpace(out long freeBytesAvailable, out long totalNumberOfBytes, out long totalNumberOfFreeBytes, DokanFileInfo info)
        {
            var di = new DriveInfo(Path.GetPathRoot(cacheFolder));
            freeBytesAvailable = di.AvailableFreeSpace;
            totalNumberOfBytes = di.TotalSize;
            totalNumberOfFreeBytes = di.TotalFreeSpace;

            return DokanResult.Success;
        }

        public NtStatus GetFileInformation(string fileName, out FileInformation fileInfo, DokanFileInfo info)
        {

            CloudDir dir;
            if (mappedToDir.TryGetValue(fileName, out dir))
            {
                fileInfo = GetFileInformation(dir);
                return DokanResult.Success;
            }
            CloudFile file;
            if (!mappedToFile.TryGetValue(fileName, out file))
            {
                fileInfo = GetFileInformation(dir);
                return DokanResult.Success;
            }

            fileInfo = new FileInformation();
            return DokanResult.PathNotFound;
        }

        private FileInformation GetFileInformation(CloudItem i)
        {
            return new FileInformation
            {
                FileName = i.Name,
                Attributes = i.IsDir ? FileAttributes.Directory : FileAttributes.Normal,
                LastAccessTime = DateTime.Now,
                LastWriteTime = DateTime.Now,
                CreationTime = DateTime.Now
            };
        }

        public NtStatus GetFileSecurity(string fileName, out FileSystemSecurity security, AccessControlSections sections, DokanFileInfo info)
        {
            security = null;
            return DokanResult.Error;
        }

        public NtStatus GetVolumeInformation(out string volumeLabel, out FileSystemFeatures features, out string fileSystemName, DokanFileInfo info)
        {
            volumeLabel = "CloudDrive";
            features = FileSystemFeatures.None;
            fileSystemName = String.Empty;
            return DokanResult.Error;
        }

        public NtStatus LockFile(string fileName, long offset, long length, DokanFileInfo info)
        {

            return DokanResult.Success;
        }

        public NtStatus MoveFile(string oldName, string newName, bool replace, DokanFileInfo info)
        {
            if (mappedToFile.ContainsKey(newName))
            {
                if (replace)
                    DeleteFile(newName);
                else
                    return DokanResult.FileExists;
            }
            var file = mappedToFile[oldName];
            mappedToFile.Remove(oldName);
            file.MappedPath = newName;
            mappedToFile.Add(newName, file);

            return DokanResult.Success;
        }

        public NtStatus ReadFile(string fileName, byte[] buffer, out int bytesRead, long offset, DokanFileInfo info)
        {

            var file = mappedToFile[fileName];
            file.Stream.Position = offset;
            bytesRead = file.Stream.Read(buffer, 0, buffer.Length);

            return DokanResult.Success;
        }

        public NtStatus SetAllocationSize(string fileName, long length, DokanFileInfo info)
        {
            return DokanResult.Error;
        }

        public NtStatus SetEndOfFile(string fileName, long length, DokanFileInfo info)
        {

            var file = mappedToFile[fileName];
            file.Stream.SetLength(length);
            return DokanResult.Success;
        }

        public NtStatus SetFileAttributes(string fileName, FileAttributes attributes, DokanFileInfo info)
        {
            return DokanResult.Error;
        }

        public NtStatus SetFileSecurity(string fileName, FileSystemSecurity security, AccessControlSections sections, DokanFileInfo info)
        {
            return DokanResult.Error;
        }

        public NtStatus SetFileTime(string fileName, DateTime? creationTime, DateTime? lastAccessTime, DateTime? lastWriteTime, DokanFileInfo info)
        {
            return DokanResult.Error;
        }

        public NtStatus UnlockFile(string fileName, long offset, long length, DokanFileInfo info)
        {
            return DokanResult.Success;
        }

        public NtStatus Unmount(DokanFileInfo info)
        {
            return DokanResult.Success;
        }

        public NtStatus WriteFile(string fileName, byte[] buffer, out int bytesWritten, long offset, DokanFileInfo info)
        {

            var file = mappedToFile[fileName];
            file.Stream.Position = offset;
            file.Stream.Write(buffer, 0, buffer.Length);
            bytesWritten = buffer.Length;
            return DokanResult.Success;
        }

        public NtStatus FindStreams(string fileName, out IList<FileInformation> streams, DokanFileInfo info)
        {
            streams = new FileInformation[0];
            return DokanResult.NotImplemented;
        }
    }
}
