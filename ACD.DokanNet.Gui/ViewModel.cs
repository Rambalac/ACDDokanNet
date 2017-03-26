namespace Azi.Cloud.DokanNet.Gui
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Collections.Specialized;
    using System.ComponentModel;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Windows;
    using Common;
    using Tools;

    public class ViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly Timer refreshTimer;
        private bool disposedValue;

        private UpdateChecker.UpdateInfo updateAvailable;

        public ViewModel()
        {
            try
            {
                refreshTimer = new Timer(RefreshLetters, null, 1000, 1000);

                UploadFiles.CollectionChanged += UploadFiles_CollectionChanged;
                DownloadFiles.CollectionChanged += DownloadFiles_CollectionChanged;
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                throw;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public List<AvailableCloud> AvailableClouds { get; private set; }

        // TODO
        public bool CanClearCache => true;

        public bool CheckFileHash
        {
            get
            {
                return Properties.Settings.Default.CheckFileHash;
            }

            set
            {
                Properties.Settings.Default.CheckFileHash = value;
                foreach (var cloud in Clouds)
                {
                    if (cloud.Provider != null)
                    {
                        cloud.Provider.CheckFileHash = value;
                    }
                }
            }
        }

        public ObservableCollection<CloudMount> Clouds { get; private set; } = new ObservableCollection<CloudMount>();

        public ObservableCollection<FileItemInfo> DownloadFailedFiles { get; } = new ObservableCollection<FileItemInfo>();

        public ObservableCollection<FileItemInfo> DownloadFiles { get; } = new ObservableCollection<FileItemInfo>();

        public int DownloadFilesCount => DownloadFiles.Count;

        public string DownloadFilesTooltip => string.Join("\r\n", DownloadFiles.Select(f => f.FileName));

        public bool HasFreeLetters => VirtualDriveWrapper.GetFreeDriveLettes().Any();

        public Visibility HasUpdate => UpdateAvailable != null ? Visibility.Visible : Visibility.Collapsed;

        public bool HockeyAppEnabled
        {
            get { return Log.HockeyAppEnabled; }
            set { Log.HockeyAppEnabled = value; }
        }

        public bool IsAutorun
        {
            get
            {
                return App?.GetAutorun() ?? false;
            }

            set
            {
                App.SetAutorun(value);
            }
        }

        public string SmallFileCacheFolder
        {
            get
            {
                return Properties.Settings.Default.CacheFolder;
            }

            set
            {
                Properties.Settings.Default.CacheFolder = value;
                Properties.Settings.Default.Save();
                foreach (var cloud in Clouds)
                {
                    if (cloud.Provider != null)
                    {
                        cloud.Provider.CachePath = Environment.ExpandEnvironmentVariables(value);
                    }
                }

                OnPropertyChanged(nameof(SmallFileCacheFolder));
            }
        }

        public long SmallFilesCacheSize
        {
            get
            {
                return Properties.Settings.Default.SmallFilesCacheLimit;
            }

            set
            {
                Properties.Settings.Default.SmallFilesCacheLimit = value;
                Properties.Settings.Default.Save();
                foreach (var cloud in Clouds)
                {
                    if (cloud.Provider != null)
                    {
                        cloud.Provider.SmallFilesCacheSize = value * (1 << 20);
                    }
                }
            }
        }

        public long SmallFileSizeLimit
        {
            get
            {
                return Properties.Settings.Default.SmallFileSizeLimit;
            }

            set
            {
                Properties.Settings.Default.SmallFileSizeLimit = value;
                Properties.Settings.Default.Save();
                foreach (var cloud in Clouds)
                {
                    if (cloud.Provider != null)
                    {
                        cloud.Provider.SmallFileSizeLimit = value * (1 << 20);
                    }
                }
            }
        }

        public UpdateChecker.UpdateInfo UpdateAvailable
        {
            get
            {
                return updateAvailable;
            }

            set
            {
                updateAvailable = value;
                OnPropertyChanged(nameof(UpdateAvailable));
                OnPropertyChanged(nameof(HasUpdate));
                OnPropertyChanged(nameof(UpdateVersion));
            }
        }

        public string UpdateVersion => UpdateAvailable?.Version ?? string.Empty;

        public ObservableCollection<FileItemInfo> UploadFiles { get; } = new ObservableCollection<FileItemInfo>();

        public int UploadFilesCount => UploadFiles.Count;

        public string Version => Assembly.GetEntryAssembly().GetName().Version.ToString();

        private static App App => App.MyApp;

        public void AddCloud(AvailableCloud selectedItem)
        {
            var name = selectedItem.Name;
            var letters = VirtualDriveWrapper.GetFreeDriveLettes();
            if (letters.Count == 0)
            {
                throw new InvalidOperationException("No free letters");
            }

            if (Clouds.Any(c => c.CloudInfo.Name == name))
            {
                var i = 1;
                while (Clouds.Any(c => c.CloudInfo.Name == name + " " + i))
                {
                    i++;
                }

                name = name + " " + i;
            }

            var info = new CloudInfo
            {
                Id = Guid.NewGuid().ToString(),
                Name = name,
                ClassName = selectedItem.ClassName,
                AssemblyFileName = selectedItem.AssemblyFileName,
                DriveLetter = letters[0]
            };
            var mount = new CloudMount(info, this);
            Clouds.Add(mount);
            SaveClouds();
        }

        public void BuildAvailableClouds()
        {
            AvailableClouds = new List<AvailableCloud>();
            try
            {
                foreach (var file in Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory, "Clouds.*.dll"))
                {
                    try
                    {
                        var assembly = Assembly.LoadFrom(file);

                        var types = assembly.GetExportedTypes().Where(t => typeof(IHttpCloud).IsAssignableFrom(t));

                        AvailableClouds.AddRange(types.Where(t => t.IsClass)
                                .Select(t => new AvailableCloud
                                {
                                    AssemblyFileName = Path.GetFileName(file),
                                    ClassName = t.FullName,
                                    Name = (string)t.GetProperty("CloudServiceName").GetValue(null),
                                    Icon = (string)t.GetProperty("CloudServiceIcon").GetValue(null)
                                }));
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
        }

        public void DeleteCloud(CloudMount cloud)
        {
            Clouds.Remove(cloud);
            SaveClouds();
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
        }

        public void LoadClouds()
        {
            var settings = Properties.Settings.Default;
            if (settings.Clouds == null)
            {
                Log.ErrorTrace("No clouds!");
                settings.Clouds = new CloudInfoCollection();
                settings.Save();
            }

            Clouds = new ObservableCollection<CloudMount>(settings.Clouds.Select(s => new CloudMount(s, this)));
            Clouds.CollectionChanged += Clouds_CollectionChanged;
        }

        public void NotifyUnmount(string cloudid)
        {
            var toremove = UploadFiles.Where(f => f.CloudId == cloudid).ToList();
            foreach (var item in toremove)
            {
                UploadFiles.Remove(item);
            }

            toremove = DownloadFiles.Where(f => f.CloudId == cloudid).ToList();
            foreach (var item in toremove)
            {
                DownloadFiles.Remove(item);
            }
        }

        public void OnProviderDownloadStatisticsUpdated(CloudMount mount, StatisticUpdateReason reason, AStatisticFileInfo info)
        {
            var cloud = mount.CloudInfo;
            switch (reason)
            {
                case StatisticUpdateReason.DownloadAdded:
                    {
                        var item = new FileItemInfo(cloud.Id, info.Id)
                        {
                            CloudIcon = mount.Instance.CloudServiceIcon,
                            FileName = info.FileName,
                            FullPath = $"{mount.MountLetter}:{info.Path}",
                            ErrorMessage = info.ErrorMessage,
                            Total = info.Total,
                            CloudName = cloud.Name
                        };
                        DownloadFiles.Remove(item);
                        DownloadFiles.Add(item);
                    }

                    break;

                case StatisticUpdateReason.DownloadFinished:
                    {
                        DownloadFiles.Remove(new FileItemInfo(cloud.Id, info.Id));
                    }

                    break;

                case StatisticUpdateReason.DownloadFailed:
                    {
                        var item = UploadFiles.Single(f => f.Id == info.Id && f.CloudId == cloud.Id);
                        item.ErrorMessage = info.ErrorMessage;
                        DownloadFiles.Remove(item);
                        DownloadFailedFiles.Add(item);
                        if (DownloadFailedFiles.Count > 10)
                        {
                            DownloadFailedFiles.RemoveAt(DownloadFailedFiles.Count - 1);
                        }
                    }

                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public void OnProviderStatisticsUpdated(CloudMount mount, StatisticUpdateReason reason, AStatisticFileInfo info)
        {
            if (info is UploadStatisticInfo uploadInfo)
            {
                OnProviderUploadStatisticsUpdated(mount, reason, uploadInfo);
                return;
            }

            if (info is DownloadStatisticInfo downloadInfo)
            {
                OnProviderDownloadStatisticsUpdated(mount, reason, downloadInfo);
                return;
            }

            throw new ArgumentOutOfRangeException(nameof(info));
        }

        public void OnProviderUploadStatisticsUpdated(CloudMount mount, StatisticUpdateReason reason, UploadStatisticInfo info)
        {
            var cloud = mount.CloudInfo;
            if (reason == StatisticUpdateReason.UploadAdded)
            {
                var newitem = new FileItemInfo(cloud.Id, info.Id)
                {
                    CloudIcon = mount.Instance.CloudServiceIcon,
                    FileName = info.FileName,
                    FullPath = $"{mount.MountLetter}:{info.Path}",
                    ErrorMessage = info.ErrorMessage,
                    Total = info.Total,
                    CloudName = cloud.Name
                };
                UploadFiles.Remove(newitem);
                UploadFiles.Add(newitem);
                return;
            }

            var item = UploadFiles.SingleOrDefault(f => f.Id == info.Id && f.CloudId == cloud.Id);
            if (item == null)
            {
                return;
            }

            switch (reason)
            {
                case StatisticUpdateReason.UploadFinished:
                    UploadFiles.Remove(new FileItemInfo(cloud.Id, info.Id));
                    break;

                case StatisticUpdateReason.UploadFailed:
                    {
                        item.ErrorMessage = info.ErrorMessage;
                        UploadFiles.Remove(item);
                        UploadFiles.Add(item);
                    }

                    break;

                case StatisticUpdateReason.UploadAborted:
                    {
                        item.ErrorMessage = info.ErrorMessage;
                        item.DismissOnly = true;
                        UploadFiles.Remove(item);
                        UploadFiles.Add(item);
                    }

                    break;

                case StatisticUpdateReason.Progress:
                    {
                        item.Done = info.Done;
                    }

                    break;

                case StatisticUpdateReason.UploadState:
                    {
                        item.State = info.State;
                    }

                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public void SaveClouds()
        {
            var settings = Properties.Settings.Default;
            settings.Clouds = new CloudInfoCollection(Clouds.Select(c => c.CloudInfo));
            settings.Save();
        }

        public void Shutdown()
        {
            App.Dispatcher.Invoke(App.Shutdown);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposedValue)
            {
                return;
            }

            if (disposing)
            {
                refreshTimer.Dispose();
            }

            disposedValue = true;
        }

        private void Clouds_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(Clouds));
        }

        private void DownloadFiles_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(DownloadFiles));
            OnPropertyChanged(nameof(DownloadFilesCount));
            OnPropertyChanged(nameof(DownloadFilesTooltip));
        }

        private void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private void RefreshLetters(object state)
        {
            if (App == null)
            {
                return;
            }

            if (Clouds != null)
            {
                foreach (var cloud in Clouds)
                {
                    cloud.OnPropertyChanged(nameof(cloud.DriveLetters));
                }
            }

            OnPropertyChanged(nameof(HasFreeLetters));
        }

        private void UploadFiles_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(UploadFiles));
            OnPropertyChanged(nameof(UploadFilesCount));
        }
    }
}