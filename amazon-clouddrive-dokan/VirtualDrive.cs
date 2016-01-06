using DokanNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Security.AccessControl;
using FileAccess = DokanNet.FileAccess;
using Azi.Tools;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Diagnostics;

namespace Azi.ACDDokanNet
{

    internal class VirtualDrive : IDokanOperations
    {
        FSProvider provider;
        public VirtualDrive(FSProvider provider)
        {
            this.provider = provider;
        }

        public void Cleanup(string fileName, DokanFileInfo info)
        {
            try
            {
                if (info.Context != null)
                {
                    var str = info.Context as IBlockStream;
                    if (str != null) str.Close();
                }
                if (info.DeleteOnClose)
                {
                    if (info.IsDirectory)
                        provider.DeleteDir(fileName);
                    else
                        provider.DeleteFile(fileName);
                }
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }

        public void CloseFile(string fileName, DokanFileInfo info)
        {
            try
            {
                if (info.Context != null)
                {
                    var str = info.Context as IBlockStream;
                    if (str != null) str.Close();
                }
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }

        NtStatus _CreateDirectory(string fileName, DokanFileInfo info)
        {
            if (provider.Exists(fileName)) return DokanResult.AlreadyExists;
            provider.CreateDir(fileName);
            return DokanResult.Success;
        }

        private const FileAccess DataAccess = FileAccess.ReadData | FileAccess.WriteData | FileAccess.AppendData |
                                      FileAccess.Execute |
                                      FileAccess.GenericExecute | FileAccess.GenericWrite | FileAccess.GenericRead;

        private const FileAccess DataWriteAccess = FileAccess.WriteData | FileAccess.AppendData |
                                                   FileAccess.Delete |
                                                   FileAccess.GenericWrite;
        private const FileAccess DataReadAccess = FileAccess.ReadData | FileAccess.GenericExecute |
                                                   FileAccess.Execute;
        public NtStatus CreateFile(string fileName, FileAccess access, FileShare share, FileMode mode, FileOptions options, FileAttributes attributes, DokanFileInfo info)
        {
            try
            {
                var res = _CreateFile(fileName, access, share, mode, options, attributes, info);
                Log.Trace($"{fileName}\r\n  Access:[{access}]\r\n  Share:[{share}]\r\n  Mode:[{mode}]\r\n  Options:[{options}]\r\n  Attr:[{attributes}]\r\nStatus:{res}");
                return res;
            }
            catch (Exception e)
            {
                Log.Error($"{fileName}\r\n  Access:[{access}]\r\n  Share:[{share}]\r\n  Mode:[{mode}]\r\n  Options:[{options}]\r\n  Attr:[{attributes}]\r\n\r\n{e}");
                return DokanResult.Error;
            }
        }

        public NtStatus _CreateFile(string fileName, FileAccess access, FileShare share, FileMode mode, FileOptions options, FileAttributes attributes, DokanFileInfo info)
        {
            if (info.IsDirectory)
            {
                if (mode == FileMode.CreateNew) return _CreateDirectory(fileName, info);
                if (mode == FileMode.Open && !provider.Exists(fileName)) return DokanResult.PathNotFound;

                if (access == FileAccess.Synchronize)
                {
                    info.Context = new object();
                    return DokanResult.Success;
                }
                info.Context = new object();
                return DokanResult.Success;
            }

            var item = provider.GetItem(fileName);

            bool readWriteAttributes = (access & DataAccess) == 0;
            switch (mode)
            {
                case FileMode.Open:

                    if (item == null) return DokanResult.FileNotFound;
                    if (item.IsDir)
                    // check if driver only wants to read attributes, security info, or open directory
                    {
                        info.IsDirectory = item.IsDir;
                        info.Context = new object();
                        // must set it to something if you return DokanError.Success

                        return DokanResult.Success;
                    }
                    break;

                case FileMode.CreateNew:
                    if (item != null) return DokanResult.FileExists;
                    break;

                case FileMode.Truncate:
                    if (item == null) return DokanResult.FileNotFound;
                    break;
            }

            if ((access & (FileAccess.ReadAttributes | FileAccess.Delete | FileAccess.Synchronize)) != 0 && (access & DataAccess) == 0)
            {
                info.Context = new object();
                return DokanResult.Success;
            }
            return _OpenFile(fileName, access, share, mode, options, attributes, info);
        }

        private NtStatus _OpenFile(string fileName, FileAccess access, FileShare share, FileMode mode, FileOptions options, FileAttributes attributes, DokanFileInfo info)
        {
            bool readAccess = (access & DataWriteAccess) == 0;

            var result = provider.OpenFile(fileName, mode, readAccess ? System.IO.FileAccess.Read : System.IO.FileAccess.Write, share, options);

            if (result == null) return DokanResult.AccessDenied;

            info.Context = result;
            return DokanResult.Success;
        }


        public NtStatus DeleteDirectory(string fileName, DokanFileInfo info)
        {
            if (!provider.Exists(fileName)) return DokanResult.PathNotFound;

            provider.DeleteDir(fileName);
            return DokanResult.Success;
        }

        public NtStatus DeleteFile(string fileName, DokanFileInfo info)
        {
            if (!provider.Exists(fileName)) return DokanResult.PathNotFound;

            provider.DeleteFile(fileName);
            return DokanResult.Success;
        }

        public NtStatus FindFiles(string fileName, out IList<FileInformation> files, DokanFileInfo info)
        {
            try
            {
                var items = provider.GetDirItems(fileName).Result;

                files = items.Select(i => new FileInformation
                {
                    Length = i.Length,
                    FileName = i.Name,
                    Attributes = i.IsDir ? FileAttributes.Directory : FileAttributes.Normal,
                    LastAccessTime = i.LastAccessTime,
                    LastWriteTime = i.LastWriteTime,
                    CreationTime = i.CreationTime
                }).ToList();
                return DokanResult.Success;
            }
            catch (Exception e)
            {
                Log.Error(e);
                files = new List<FileInformation>();
                return DokanResult.Error;
            }
        }

        public NtStatus FlushFileBuffers(string fileName, DokanFileInfo info)
        {
            try
            {
                if (info.Context != null)
                {
                    (info.Context as IBlockStream)?.Flush();
                }
                return DokanResult.Success;
            }
            catch (Exception e)
            {
                Log.Error(e);
                return DokanResult.Error;
            }
        }

        public NtStatus GetDiskFreeSpace(out long freeBytesAvailable, out long totalNumberOfBytes, out long totalNumberOfFreeBytes, DokanFileInfo info)
        {
            try
            {
                freeBytesAvailable = provider.TotalSize - provider.TotalUsedSpace;
                totalNumberOfBytes = provider.TotalSize;
                totalNumberOfFreeBytes = provider.TotalSize - provider.TotalUsedSpace;

                return DokanResult.Success;
            }
            catch (Exception e)
            {
                freeBytesAvailable = 0;
                totalNumberOfBytes = 0;
                totalNumberOfFreeBytes = 0;
                Log.Error(e);
                return DokanResult.Error;
            }
        }

        public NtStatus GetFileInformation(string fileName, out FileInformation fileInfo, DokanFileInfo info)
        {
            try
            {
                var item = provider.GetItem(fileName);

                if (item != null)
                {
                    fileInfo = MakeFileInformation(item);
                    return DokanResult.Success;
                }

                fileInfo = new FileInformation();
                Log.Warn(fileName);
                return DokanResult.PathNotFound;
            }
            catch (Exception e)
            {
                Log.Error(e);
                fileInfo = new FileInformation();
                return DokanResult.Error;
            }
        }

        private FileInformation MakeFileInformation(FSItem i)
        {
            return new FileInformation
            {
                Length = i.Length,
                FileName = i.Name,
                Attributes = i.IsDir ? FileAttributes.Directory : FileAttributes.Normal,
                LastAccessTime = i.LastAccessTime,
                LastWriteTime = i.LastWriteTime,
                CreationTime = i.CreationTime
            };
        }

        public NtStatus GetFileSecurity(string fileName, out FileSystemSecurity security, AccessControlSections sections, DokanFileInfo info)

        {
            Log.Trace(fileName);
            security = null;
            return DokanResult.NotImplemented;
        }

        public NtStatus GetVolumeInformation(out string volumeLabel, out FileSystemFeatures features, out string fileSystemName, DokanFileInfo info)
        {
            volumeLabel = provider.VolumeName;
            features =
                FileSystemFeatures.SupportsRemoteStorage |
                FileSystemFeatures.CasePreservedNames |
                FileSystemFeatures.CaseSensitiveSearch |
                FileSystemFeatures.SupportsRemoteStorage |
                FileSystemFeatures.UnicodeOnDisk |
                FileSystemFeatures.SequentialWriteOnce;
            fileSystemName = provider.FileSystemName;
            return DokanResult.Success;
        }

        public NtStatus LockFile(string fileName, long offset, long length, DokanFileInfo info)
        {
            try
            {

                return DokanResult.Success;
            }
            catch (Exception e)
            {
                Log.Error(e);
                return DokanResult.Error;
            }
        }

        public NtStatus MoveFile(string oldName, string newName, bool replace, DokanFileInfo info)
        {
            try
            {
                provider.MoveFile(oldName, newName, replace);
                return DokanResult.Success;
            }
            catch (Exception e)
            {
                Log.Error(e);
                return DokanResult.Error;
            }
        }

        const int readTimeout = 30000;
        public NtStatus ReadFile(string fileName, byte[] buffer, out int bytesRead, long offset, DokanFileInfo info)
        {
            var start = DateTime.UtcNow;
            try
            {
                var reader = info.Context as IBlockStream;

                bytesRead = reader.Read(offset, buffer, 0, buffer.Length, readTimeout);
                return DokanResult.Success;
            }
            catch (ObjectDisposedException)
            {
                bytesRead = 0;
                return NtStatus.FileClosed;
            }
            catch (NotSupportedException)
            {
                Log.Warn("ReadWrite not supported: " + fileName);
                bytesRead = 0;
                return DokanResult.AccessDenied;
            }
            catch (TimeoutException)
            {
                Log.Warn("Timeout " + (DateTime.UtcNow - start).TotalMilliseconds);
                bytesRead = 0;
                return NtStatus.Timeout;
            }
            catch (Exception e)
            {
                Log.Error(e);
                bytesRead = 0;
                return DokanResult.Error;
            }
        }

        public NtStatus SetAllocationSize(string fileName, long length, DokanFileInfo info)
        {
            Log.Trace(fileName);
            return DokanResult.NotImplemented;
        }

        public NtStatus SetEndOfFile(string fileName, long length, DokanFileInfo info)
        {
            Log.Trace(fileName);
            return DokanResult.NotImplemented;
        }

        public NtStatus SetFileAttributes(string fileName, FileAttributes attributes, DokanFileInfo info)
        {
            Log.Trace(fileName);
            return DokanResult.Error;
        }

        public NtStatus SetFileSecurity(string fileName, FileSystemSecurity security, AccessControlSections sections, DokanFileInfo info)
        {
            Log.Trace(fileName);
            return DokanResult.NotImplemented;
        }

        public NtStatus SetFileTime(string fileName, DateTime? creationTime, DateTime? lastAccessTime, DateTime? lastWriteTime, DokanFileInfo info)
        {
            Log.Trace(fileName);
            return DokanResult.Error;
        }

        public NtStatus UnlockFile(string fileName, long offset, long length, DokanFileInfo info)
        {
            Log.Warn(fileName);
            return DokanResult.Success;
        }

        public NtStatus Unmount(DokanFileInfo info)
        {
            return DokanResult.Success;
        }

        public NtStatus WriteFile(string fileName, byte[] buffer, out int bytesWritten, long offset, DokanFileInfo info)
        {
            try
            {
                if (info.Context != null)
                {
                    var writer = info.Context as IBlockStream;
                    if (writer != null)
                    {
                        writer.Write(offset, buffer, 0, buffer.Length);
                        bytesWritten = buffer.Length;
                        return DokanResult.Success;
                    }
                }

                bytesWritten = 0;
                return DokanResult.AccessDenied;
            }
            catch (NotSupportedException)
            {
                Log.Warn("ReadWrite not supported: " + fileName);
                bytesWritten = 0;
                return DokanResult.AccessDenied;
            }
            catch (Exception e)
            {
                Log.Error(e);
                bytesWritten = 0;
                return DokanResult.AccessDenied;
            }
        }

        public NtStatus FindStreams(string fileName, out IList<FileInformation> streams, DokanFileInfo info)
        {
            Log.Warn(fileName);
            streams = new FileInformation[0];
            return DokanResult.NotImplemented;
        }

        public Action OnMount;
        internal Action OnUnmount;

        public NtStatus Mounted(DokanFileInfo info)
        {
            OnMount?.Invoke();
            return DokanResult.Success;
        }

        public NtStatus Unmounted(DokanFileInfo info)
        {
            OnUnmount?.Invoke();
            return DokanResult.Success;
        }
    }
}
