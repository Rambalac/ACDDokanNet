using DokanNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Security.AccessControl;
using FileAccess = DokanNet.FileAccess;
using System.Diagnostics;
using Azi.Tools;

namespace Azi.ACDDokanNet
{

    public class VirtualDrive : IDokanOperations
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
                var disp = info.Context as IDisposable;
                if (disp != null)
                {
                    disp.Dispose();
                }
                info.Context = null;

                if (info.DeleteOnClose)
                {
                    if (info.IsDirectory)
                    {
                        provider.DeleteDir(fileName);
                    }
                    else
                    {
                        provider.DeleteFile(fileName);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }

        public void Mount(string path)
        {
            try
            {
                this.Mount(path, DokanOptions.DebugMode | DokanOptions.StderrOutput | DokanOptions.NetworkDrive);
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
                var str = info.Context as Stream;
                if (str != null)
                {
                    str.Close();
                    str.Dispose();
                }
                info.Context = null;
            }
            catch (Exception e)
            {
                EventLog.WriteEntry("ACDDokanNet", "Cleanup: " + e.Message, EventLogEntryType.Error);
            }
        }

        private void DeleteFile(string fileName)
        {
            provider.DeleteFile(fileName);
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

        public NtStatus CreateFile(string fileName, FileAccess access, FileShare share, FileMode mode, FileOptions options, FileAttributes attributes, DokanFileInfo info)
        {
            try
            {
                if (info.IsDirectory)
                {
                    if (mode == FileMode.CreateNew) return _CreateDirectory(fileName, info);
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
                        provider.CreateFile(fileName);
                        break;

                    case FileMode.Truncate:
                        if (item == null) return DokanResult.FileNotFound;
                        break;
                }

                return _OpenFile(fileName, access, share, mode, options, attributes, info);
            }
            catch (Exception e)
            {
                Log.Error(e);
                return DokanResult.Error;
            }
        }

        private NtStatus _OpenFile(string fileName, FileAccess access, FileShare share, FileMode mode, FileOptions options, FileAttributes attributes, DokanFileInfo info)
        {
            if (access == (FileAccess.ReadAttributes | FileAccess.Delete | FileAccess.Synchronize)) return DokanResult.Success;

            bool readAccess = (access & DataWriteAccess) == 0;
            var result = provider.OpenFile(fileName, mode, readAccess ? System.IO.FileAccess.Read : System.IO.FileAccess.ReadWrite, share, options);

            if (result == null) return DokanResult.AccessDenied;

            info.Context = result;
            return DokanResult.Success;
        }


        public NtStatus DeleteDirectory(string fileName, DokanFileInfo info)
        {
            try
            {
                provider.DeleteDir(fileName);

                return DokanResult.Success;
            }
            catch (Exception e)
            {
                Log.Error(e);
                return DokanResult.Error;
            }
        }

        public NtStatus DeleteFile(string fileName, DokanFileInfo info)
        {
            try
            {

                DeleteFile(fileName);

                return DokanResult.Success;
            }
            catch (Exception e)
            {
                Log.Error(e);
                return DokanResult.Error;
            }
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
                    LastAccessTime = DateTime.Now,
                    LastWriteTime = DateTime.Now,
                    CreationTime = DateTime.Now
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
                    ((Stream)info.Context).Flush();
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
                freeBytesAvailable = provider.AvailableFreeSpace;
                totalNumberOfBytes = provider.TotalSize;
                totalNumberOfFreeBytes = provider.TotalFreeSpace;

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
                    fileInfo = GetFileInformation(item);
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

        private FileInformation GetFileInformation(FSItem i)
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
            Log.Warn(fileName);
            security = null;
            return DokanResult.Error;
        }

        public NtStatus GetVolumeInformation(out string volumeLabel, out FileSystemFeatures features, out string fileSystemName, DokanFileInfo info)
        {
            volumeLabel = provider.VolumeName;
            features =
                FileSystemFeatures.ReadOnlyVolume |
                FileSystemFeatures.CasePreservedNames |
                FileSystemFeatures.SupportsRemoteStorage |
                FileSystemFeatures.UnicodeOnDisk;
            fileSystemName = "ACD";
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

        public NtStatus ReadFile(string fileName, byte[] buffer, out int bytesRead, long offset, DokanFileInfo info)
        {
            try
            {
                var stream = info.Context as Stream;
                stream.Position = offset;

                bytesRead = stream.Read(buffer, 0, buffer.Length);
                return DokanResult.Success;
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
            Log.Warn(fileName);
            return DokanResult.Error;
        }

        public NtStatus SetEndOfFile(string fileName, long length, DokanFileInfo info)
        {
            Log.Warn(fileName);
            return DokanResult.Error;
        }

        public NtStatus SetFileAttributes(string fileName, FileAttributes attributes, DokanFileInfo info)
        {
            Log.Warn(fileName);
            return DokanResult.Error;
        }

        public NtStatus SetFileSecurity(string fileName, FileSystemSecurity security, AccessControlSections sections, DokanFileInfo info)
        {
            Log.Warn(fileName);
            return DokanResult.Error;
        }

        public NtStatus SetFileTime(string fileName, DateTime? creationTime, DateTime? lastAccessTime, DateTime? lastWriteTime, DokanFileInfo info)
        {
            Log.Warn(fileName);
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
                var stream = info.Context as Stream;
                stream.Position = offset;

                stream.Write(buffer, 0, buffer.Length);
                bytesWritten = buffer.Length;
                return DokanResult.Success;
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

        public NtStatus Mounted(DokanFileInfo info)
        {
            return DokanResult.Success;
        }

        public NtStatus Unmounted(DokanFileInfo info)
        {
            return DokanResult.Success;
        }
    }
}
