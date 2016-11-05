namespace Azi.Cloud.DokanNet
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Security.AccessControl;
    using System.Security.Principal;
    using Common;
    using global::DokanNet;
    using Tools;
    using FileAccess = global::DokanNet.FileAccess;

    public class VirtualDrive : IDokanOperations
    {
        private const FileAccess DataAccess = FileAccess.ReadData | FileAccess.WriteData | FileAccess.AppendData |
                              FileAccess.Execute |
                              FileAccess.GenericExecute | FileAccess.GenericWrite | FileAccess.GenericRead;

        private const FileAccess DataReadAccess = FileAccess.ReadData | FileAccess.GenericExecute |
                                                   FileAccess.Execute;

        private const FileAccess DataWriteAccess = FileAccess.WriteData | FileAccess.AppendData |
                                                   FileAccess.Delete |
                                                   FileAccess.GenericWrite;

        private const int ReadTimeout = 30000;
#if TRACE
        private string lastFilePath;
#endif

        private string creator = WindowsIdentity.GetCurrent().Name;
        private FSProvider provider;

        public VirtualDrive(FSProvider provider)
        {
            this.provider = provider;
        }

        public string MountPath { get; internal set; }

        public Action OnMount { get; set; }

        public Action OnUnmount { get; set; }

        public bool ReadOnly { get; internal set; }

        public void Cleanup(string fileName, DokanFileInfo info)
        {
            try
            {
                // if (info.Context != null)
                // {
                //    var str = info.Context as IBlockStream;
                //    if (str != null)
                //    {
                //        str.Close();
                //    }
                // }
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

        public void CloseFile(string fileName, DokanFileInfo info)
        {
            try
            {
                if (info.Context != null)
                {
                    var str = info.Context as IBlockStream;
                    if (str != null)
                    {
                        str.Close();
                        info.Context = null;
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }

        public NtStatus CreateFile(string fileName, FileAccess access, FileShare share, FileMode mode, FileOptions options, FileAttributes attributes, DokanFileInfo info)
        {
            try
            {
                var res = MainCreateFile(fileName, access, share, mode, options, attributes, info);
#if TRACE
                bool readWriteAttributes = (access & DataAccess) == 0;
                if (!(readWriteAttributes || info.IsDirectory) || (res != DokanResult.Success && !(lastFilePath == fileName && res == DokanResult.FileNotFound)))
                {
                    if (!(info.Context is NewFileBlockWriter || info.Context is FileBlockReader || info.Context is SmallFileBlockReaderWriter || info.Context is BufferedHttpCloudBlockReader))
                    {
                        Log.Trace($"{fileName}\r\n  Access:[{access}]\r\n  Share:[{share}]\r\n  Mode:[{mode}]\r\n  Options:[{options}]\r\n  Attr:[{attributes}]\r\nStatus:{res}");
                    }

                    lastFilePath = fileName;
                }
#endif
                return res;
            }
            catch (Exception e)
            {
                Log.Error($"{fileName}\r\n  Access:[{access}]\r\n  Share:[{share}]\r\n  Mode:[{mode}]\r\n  Options:[{options}]\r\n  Attr:[{attributes}]\r\n\r\n{e}");
                return DokanResult.Error;
            }
        }

        public NtStatus DeleteDirectory(string fileName, DokanFileInfo info)
        {
            if (ReadOnly)
            {
                return DokanResult.AccessDenied;
            }

            if (!provider.Exists(fileName))
            {
                return DokanResult.PathNotFound;
            }

            try
            {
                provider.DeleteDir(fileName);
                return DokanResult.Success;
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                return DokanResult.Error;
            }
        }

        public NtStatus DeleteFile(string fileName, DokanFileInfo info)
        {
            if (ReadOnly)
            {
                return DokanResult.AccessDenied;
            }

            if (!provider.Exists(fileName))
            {
                return DokanResult.PathNotFound;
            }

            try
            {
                Log.Trace("Delete file:" + fileName);

                provider.DeleteFile(fileName);
                var newfile = info.Context as NewFileBlockWriter;
                if (newfile != null)
                {
                    newfile.CancelUpload();
                }

                return DokanResult.Success;
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                return DokanResult.Error;
            }
        }

        public NtStatus FindFiles(string fileName, out IList<FileInformation> files, DokanFileInfo info)
        {
            if (!HasAccess(info))
            {
                files = null;
                return DokanResult.AccessDenied;
            }

            try
            {
                var itemsTask = provider.GetDirItems(fileName);
                if (!itemsTask.Wait(15000))
                {
                    files = null;
                    return NtStatus.Timeout;
                }

                var items = itemsTask.Result;

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

        public NtStatus FindFilesWithPattern(string fileName, string searchPattern, out IList<FileInformation> files, DokanFileInfo info)
        {
            files = new List<FileInformation>();
            return DokanResult.NotImplemented;

            /*
            if (!HasAccess(info))
            {
                files = null;
                return DokanResult.AccessDenied;
            }

            try
            {
                var items = provider.GetDirItems(fileName).Result;

                var regex = new Regex(Regex.Escape(searchPattern).Replace("\\?", ".").Replace("\\*", ".*"));

                files = items.Where(i=>regex.IsMatch(Path.GetFileName(i.Name))).Select(i => new FileInformation
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
            */
        }

        public NtStatus FindStreams(string fileName, out IList<FileInformation> streams, DokanFileInfo info)
        {
            Log.Trace(fileName);
            streams = new List<FileInformation>();
            try
            {
                var item = provider.GetItem(fileName);
                if (item == null)
                {
                    return DokanResult.FileNotFound;
                }

                if (!item.IsDir)
                {
                    var infostream = MakeFileInformation(item);
                    infostream.FileName = "::$DATA";
                    streams.Add(infostream);
                }

                {
                    var infostream = new FileInformation
                    {
                        FileName = $":{CloudDokanNetItemInfo.StreamName}:$DATA",
                        Length = 1
                    };
                    streams.Add(infostream);
                }

                return DokanResult.Success;
            }
            catch (Exception e)
            {
                Log.Error(e);
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
                const long fakeSize = 100L << 40;

                freeBytesAvailable = fakeSize - provider.TotalUsedSpace;
                totalNumberOfBytes = fakeSize;
                totalNumberOfFreeBytes = freeBytesAvailable;

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
            if (!HasAccess(info))
            {
                fileInfo = default(FileInformation);
                return DokanResult.AccessDenied;
            }

            try
            {
                string streamName = null;
                fileName = fileName.Replace("\\:", ":");

                if (fileName.Contains(':'))
                {
                    var names = fileName.Split(':');
                    fileName = names[0];
                    streamName = names[1];
                }

                var item = provider.GetItem(fileName);

                if (item != null)
                {
                    fileInfo = MakeFileInformation(item);
                    if (streamName != null)
                    {
                        fileInfo.Length = 1;
                    }

                    return DokanResult.Success;
                }

                fileInfo = default(FileInformation);
                return DokanResult.PathNotFound;
            }
            catch (Exception e)
            {
                Log.Error(e);
                fileInfo = default(FileInformation);
                return DokanResult.Error;
            }
        }

        public NtStatus GetFileSecurity(string fileName, out FileSystemSecurity security, AccessControlSections sections, DokanFileInfo info)
        {
            Log.Trace(fileName);

            var identity = WindowsIdentity.GetCurrent().Owner;
            FileSystemSecurity result = info.IsDirectory
                ? (FileSystemSecurity)new DirectorySecurity()
                : (FileSystemSecurity)new FileSecurity();
            if (sections.HasFlag(AccessControlSections.Access))
            {
                result.SetAccessRule(new FileSystemAccessRule(identity, FileSystemRights.FullControl, AccessControlType.Allow));
                result.SetAccessRuleProtection(false, true);
            }

            if (sections.HasFlag(AccessControlSections.Owner))
            {
                result.SetOwner(identity);
            }

            security = result;
            return DokanResult.Success;
        }

        public NtStatus GetVolumeInformation(out string volumeLabel, out FileSystemFeatures features, out string fileSystemName, DokanFileInfo info)
        {
            volumeLabel = provider.VolumeName;
            features =
                FileSystemFeatures.NamedStreams |
                FileSystemFeatures.SupportsRemoteStorage |
                FileSystemFeatures.CasePreservedNames |
                FileSystemFeatures.CaseSensitiveSearch |
                FileSystemFeatures.SupportsRemoteStorage |
                FileSystemFeatures.UnicodeOnDisk |
                FileSystemFeatures.SequentialWriteOnce;
            if (ReadOnly)
            {
                features |= FileSystemFeatures.ReadOnlyVolume;
            }

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

        public NtStatus Mounted(DokanFileInfo info)
        {
            OnMount?.Invoke();
            return DokanResult.Success;
        }

        public NtStatus MoveFile(string oldName, string newName, bool replace, DokanFileInfo info)
        {
            if (!HasAccess(info))
            {
                return DokanResult.AccessDenied;
            }

            if (ReadOnly)
            {
                return DokanResult.AccessDenied;
            }

            try
            {
                provider.MoveFile(oldName, newName, replace);
                Log.Trace("Move file:" + oldName + " - " + newName);

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
            var start = DateTime.UtcNow;
            try
            {
                var reader = info.Context as IBlockStream;

                bytesRead = reader.Read(offset, buffer, 0, buffer.Length, ReadTimeout);
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
                Log.Warn($"Timeout {(DateTime.UtcNow - start).TotalMilliseconds} File: {fileName}");
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
            if (ReadOnly)
            {
                return DokanResult.AccessDenied;
            }

            // Log.Trace(fileName);
            return DokanResult.Success;
        }

        public NtStatus SetEndOfFile(string fileName, long length, DokanFileInfo info)
        {
            try
            {
                if (ReadOnly)
                {
                    return DokanResult.AccessDenied;
                }

                // Log.Trace(fileName);
                var file = info.Context as IBlockStream;
                file.SetLength(length);
                Log.Trace($"{fileName} to {length}");

                return DokanResult.Success;
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                return DokanResult.Error;
            }
        }

        public NtStatus SetFileAttributes(string fileName, FileAttributes attributes, DokanFileInfo info)
        {
            if (ReadOnly)
            {
                return DokanResult.AccessDenied;
            }

            Log.Trace(fileName);
            return DokanResult.Error;
        }

        public NtStatus SetFileSecurity(string fileName, FileSystemSecurity security, AccessControlSections sections, DokanFileInfo info)
        {
            if (ReadOnly)
            {
                return DokanResult.AccessDenied;
            }

            Log.Trace(fileName);
            return DokanResult.NotImplemented;
        }

        public NtStatus SetFileTime(string fileName, DateTime? creationTime, DateTime? lastAccessTime, DateTime? lastWriteTime, DokanFileInfo info)
        {
            if (ReadOnly)
            {
                return DokanResult.AccessDenied;
            }

            Log.Trace(fileName);
            return DokanResult.Error;
        }

        public NtStatus UnlockFile(string fileName, long offset, long length, DokanFileInfo info)
        {
            Log.Trace(fileName);
            return DokanResult.Success;
        }

#pragma warning disable RECS0154 // Parameter is never used
        public NtStatus Unmount(DokanFileInfo info)
#pragma warning restore RECS0154 // Parameter is never used
        {
            return DokanResult.Success;
        }

        public NtStatus Unmounted(DokanFileInfo info)
        {
            OnUnmount?.Invoke();
            return DokanResult.Success;
        }

        public NtStatus WriteFile(string fileName, byte[] buffer, out int bytesWritten, long offset, DokanFileInfo info)
        {
            if (ReadOnly)
            {
                bytesWritten = 0;
                return DokanResult.AccessDenied;
            }

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

        private NtStatus CheckStreams(string fileName, FileMode mode, DokanFileInfo info, string streamName, FSItem item)
        {
            Log.Trace($"Opening alternate stream {fileName}:{streamName}");

            var streamNameGroups = streamName.Split(',');
            switch (streamNameGroups[0])
            {
                case CloudDokanNetItemInfo.StreamName:
                    return ProcessShellCommands(streamNameGroups, mode, item, info);

                case "Zone.Identifier":
                    if (mode != FileMode.CreateNew)
                    {
                        return DokanResult.PathNotFound;
                    }

                    return OpenAsDummyWrite(info);
            }

            return DokanResult.AccessDenied;
        }

        private bool HasAccess(DokanFileInfo info)
        {
            var watch = Stopwatch.StartNew();
            var identity = Processes.GetProcessOwner(info.ProcessId);
            if (identity == "NT AUTHORITY\\SYSTEM" || identity == creator)
            {
                return true;
            }

            Log.Warn($"User {identity} has no access to drive {MountPath}\r\nCreator User is {creator} - Identity User is {identity}");
            return false;
        }

#pragma warning disable RECS0154 // Parameter is never used
        private NtStatus MainCreateDirectory(string fileName, DokanFileInfo info)
#pragma warning restore RECS0154 // Parameter is never used
        {
            if (ReadOnly)
            {
                return DokanResult.AccessDenied;
            }

            if (provider.Exists(fileName))
            {
                return DokanResult.AlreadyExists;
            }

            provider.CreateDir(fileName);
            return DokanResult.Success;
        }

        private NtStatus MainCreateFile(string fileName, FileAccess access, FileShare share, FileMode mode, FileOptions options, FileAttributes attributes, DokanFileInfo info)
        {
            if (!HasAccess(info))
            {
                return DokanResult.AccessDenied;
            }

            if (info.IsDirectory)
            {
                if (mode == FileMode.CreateNew)
                {
                    return MainCreateDirectory(fileName, info);
                }

                if (mode == FileMode.Open && !provider.Exists(fileName))
                {
                    return DokanResult.PathNotFound;
                }

                if (access == FileAccess.Synchronize)
                {
                    info.Context = new object();
                    return DokanResult.Success;
                }

                info.Context = new object();
                return DokanResult.Success;
            }

            string streamName = null;
            fileName = fileName.Replace("\\:", ":");
            if (fileName.Contains(':'))
            {
                var names = fileName.Split(':');
                fileName = names[0];
                streamName = names[1];
            }

            var item = provider.GetItem(fileName);

            if (streamName != null && item != null)
            {
                return CheckStreams(fileName, mode, info, streamName, item);
            }

            bool readWriteAttributes = (access & DataAccess) == 0;
            switch (mode)
            {
                case FileMode.Open:
                    if (item == null)
                    {
                        return DokanResult.FileNotFound;
                    }

                    // check if driver only wants to read attributes, security info, or open directory
                    if (item.IsDir)
                    {
                        info.IsDirectory = item.IsDir;
                        info.Context = new object();

                        // must set it to something if you return DokanError.Success
                        return DokanResult.Success;
                    }

                    break;

                case FileMode.CreateNew:
                    if (item != null)
                    {
                        return DokanResult.FileExists;
                    }

                    break;

                case FileMode.Truncate:
                    if (item == null)
                    {
                        return DokanResult.FileNotFound;
                    }

                    break;
            }

            if (readWriteAttributes)
            {
                info.Context = new object();
                return DokanResult.Success;
            }

            return MainOpenFile(fileName, access, share, mode, options, attributes, info);
        }

#pragma warning disable RECS0154 // Parameter is never used
        private NtStatus MainOpenFile(string fileName, FileAccess access, FileShare share, FileMode mode, FileOptions options, FileAttributes attributes, DokanFileInfo info)
#pragma warning restore RECS0154 // Parameter is never used
        {
            bool readAccess = (access & DataReadAccess) != 0;
            bool writeAccess = (access & DataWriteAccess) != 0;

            if (writeAccess && ReadOnly)
            {
                return DokanResult.AccessDenied;
            }

            var ioaccess = System.IO.FileAccess.Read;
            if (!readAccess && writeAccess)
            {
                ioaccess = System.IO.FileAccess.Write;
            }

            if (readAccess && writeAccess)
            {
                ioaccess = System.IO.FileAccess.ReadWrite;
            }

            var result = provider.OpenFile(fileName, mode, ioaccess, share, options);

            if (result == null)
            {
                return DokanResult.AccessDenied;
            }

            info.Context = result;
            return DokanResult.Success;
        }

        private FileInformation MakeFileInformation(FSItem i)
        {
            var result = new FileInformation
            {
                Length = i.Length,
                FileName = i.Name,
                Attributes = i.IsDir ? FileAttributes.Directory : FileAttributes.Normal,
                LastAccessTime = i.LastAccessTime,
                LastWriteTime = i.LastWriteTime,
                CreationTime = i.CreationTime
            };
            if (ReadOnly)
            {
                result.Attributes |= FileAttributes.ReadOnly;
            }

            return result;
        }

        private NtStatus OpenAsByteArray(byte[] data, DokanFileInfo info)
        {
            info.Context = new ByteArrayBlockReader(data);
            return DokanResult.Success;
        }

        private NtStatus OpenAsDummyWrite(DokanFileInfo info)
        {
            info.Context = new DummyBlockStream();
            return DokanResult.Success;
        }

        private NtStatus ProcessItemInfo(string[] streamNameGroups, FSItem item, DokanFileInfo info)
        {
            if (streamNameGroups.Length == 1)
            {
                return OpenAsByteArray(item.Info, info);
            }

            var result = provider.GetExtendedInfo(streamNameGroups, item);
            return OpenAsByteArray(result, info);
        }

        private NtStatus ProcessShellCommands(string[] streamNameGroups, FileMode mode, FSItem item, DokanFileInfo info)
        {
            if (mode == FileMode.OpenOrCreate)
            {
                return ProcessUploadHere(item, info);
            }

            if (mode == FileMode.Open)
            {
                if (item.Info == null)
                {
                    provider.BuildItemInfo(item).Wait();
                }

                return ProcessItemInfo(streamNameGroups, item, info);
            }

            return NtStatus.AccessDenied;
        }

        private NtStatus ProcessUploadHere(FSItem item, DokanFileInfo info)
        {
            info.Context = provider.OpenUploadHere(item);
            return DokanResult.Success;
        }
    }
}