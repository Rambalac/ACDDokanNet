namespace Azi.Cloud.DokanNet.Gui
{
    using System;
    using System.Collections.ObjectModel;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Threading;
    using System.Xml;
    using Common;
    using Hardcodet.Wpf.TaskbarNotification;
    using Microsoft.Win32;
    using Newtonsoft.Json;
    using Tools;
    using Application = System.Windows.Application;

    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application, IDisposable, INotifyPropertyChanged
    {
        private const string AppName = "ACDDokanNet";
        private const string RegistryAutorunPath = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run";
        private ObservableCollection<CloudMount> clouds;
        private bool disposedValue;
        private int downloadingCount;
        private bool shuttingdown;
        private Mutex startedMutex;
        private UpdateChecker.UpdateInfo updateAvailable;
        private UpdateChecker updateCheck = new UpdateChecker(47739891);
        private DispatcherTimer updateCheckTimer;
        private ObservableCollection<FileItemInfo> uploadFiles = new ObservableCollection<FileItemInfo>();

        public event PropertyChangedEventHandler PropertyChanged;

        public static new App Current => Application.Current as App;

        public ObservableCollection<CloudMount> Clouds
        {
            get
            {
                if (clouds == null)
                {
                    var settings = Gui.Properties.Settings.Default;
                    if (settings.Clouds == null)
                    {
                        Debug.WriteLine("No clouds!");
                        settings.Clouds = new CloudInfoCollection();
                        settings.Save();
                    }

                    clouds = new ObservableCollection<CloudMount>(settings.Clouds.Select(s => new CloudMount(s)));
                }

                return clouds;
            }
        }

        public int DownloadingCount => downloadingCount;

        public TaskbarIcon NotifyIcon { get; private set; }

        public string SmallFileCacheFolder
        {
            get
            {
                return Gui.Properties.Settings.Default.CacheFolder;
            }

            set
            {
                // TODO
                // if (provider != null)
                // {
                //    provider.CachePath = Environment.ExpandEnvironmentVariables(value);
                // }
                Gui.Properties.Settings.Default.CacheFolder = value;
                Gui.Properties.Settings.Default.Save();
            }
        }

        public long SmallFilesCacheSize
        {
            get
            {
                return Gui.Properties.Settings.Default.SmallFilesCacheLimit;
            }

            set
            {
                // TODO
                // if (provider != null)
                // {
                //    provider.SmallFilesCacheSize = value * (1 << 20);
                // }
                Gui.Properties.Settings.Default.SmallFilesCacheLimit = value;
                Gui.Properties.Settings.Default.Save();
            }
        }

        public long SmallFileSizeLimit
        {
            get
            {
                return Gui.Properties.Settings.Default.SmallFileSizeLimit;
            }

            set
            {
                // TODO
                // if (provider != null)
                // {
                //    provider.SmallFileSizeLimit = value * (1 << 20);
                // }
                Gui.Properties.Settings.Default.SmallFileSizeLimit = value;
                Gui.Properties.Settings.Default.Save();
            }
        }

        public UpdateChecker.UpdateInfo UpdateAvailable
        {
            get
            {
                return updateAvailable;
            }

            private set
            {
                updateAvailable = value;
                OnPropertyChanged(nameof(UpdateAvailable));
            }
        }

        public ObservableCollection<FileItemInfo> UploadFiles => uploadFiles;

        public int UploadingCount => uploadFiles.Count;

        public void AddCloud(AvailableCloudsModel.AvailableCloud selectedItem)
        {
            var name = selectedItem.Name;
            var letters = VirtualDriveWrapper.GetFreeDriveLettes();
            if (letters.Count == 0)
            {
                throw new InvalidOperationException("No free letters");
            }

            if (Clouds.Any(c => c.CloudInfo.Name == name))
            {
                int i = 1;
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
            var mount = new CloudMount(info);
            Clouds.Add(mount);
            var settings = Gui.Properties.Settings.Default;
            settings.Clouds = new CloudInfoCollection(Clouds.Select(c => c.CloudInfo));
            settings.Save();
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);

            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }

        public void OnProviderStatisticsUpdated(CloudInfo cloud, StatisticUpdateReason reason, AStatisticFileInfo info)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    switch (reason)
                    {
                        case StatisticUpdateReason.UploadAdded:
                            {
                                var mount = clouds.Single(c => c.CloudInfo.Id == cloud.Id);
                                var item = new FileItemInfo
                                {
                                    CloudId = cloud.Id,
                                    Id = info.Id,
                                    CloudIcon = mount.Instance.CloudServiceIcon,
                                    FileName = info.FileName,
                                    FullPath = $"{mount.MountLetter}:{info.Path}",
                                    ErrorMessage = info.ErrorMessage,
                                    Total = info.Total,
                                    CloudName = cloud.Name
                                };
                                uploadFiles.Remove(item);
                                uploadFiles.Add(item);
                            }
                            break;

                        case StatisticUpdateReason.UploadFinished:
                            uploadFiles.Remove(new FileItemInfo { Id = info.Id });
                            break;

                        case StatisticUpdateReason.DownloadAdded:
                            downloadingCount++;
                            OnPropertyChanged(nameof(DownloadingCount));
                            break;

                        case StatisticUpdateReason.DownloadFinished:
                            downloadingCount--;
                            OnPropertyChanged(nameof(DownloadingCount));
                            break;

                        case StatisticUpdateReason.DownloadFailed:
                            downloadingCount--;
                            OnPropertyChanged(nameof(DownloadingCount));
                            break;

                        case StatisticUpdateReason.UploadFailed:
                            {
                                var item = UploadFiles.Single(f => f.Id == info.Id);
                                item.ErrorMessage = info.ErrorMessage;
                                UploadFiles.Remove(item);
                                UploadFiles.Add(item);
                            }
                            break;

                        case StatisticUpdateReason.UploadAborted:
                            {
                                var item = UploadFiles.Single(f => f.Id == info.Id);
                                item.ErrorMessage = info.ErrorMessage;
                                item.DismissOnly = true;
                                UploadFiles.Remove(item);
                                UploadFiles.Add(item);
                            }
                            break;

                        case StatisticUpdateReason.Progress:
                            {
                                var item = UploadFiles.SingleOrDefault(f => f.Id == info.Id);
                                if (item != null)
                                {
                                    item.Done = info.Done;
                                }
                            }
                            break;

                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                });
            }
            catch (TaskCanceledException)
            {
            }
        }

        public void OpenSettings()
        {
            MainWindow.Show();
            MainWindow.Activate();
        }

        internal void CancelUpload(FileItemInfo item)
        {
            var cloud = Clouds.SingleOrDefault(c => c.CloudInfo.Id == item.CloudId);
            if (cloud == null)
            {
                return;
            }

            cloud.Provider.CancelUpload(item.Id);
        }

        internal async Task ClearCache()
        {
            bool any = false;
            foreach (var mount in Clouds)
            {
                var provider = mount.Provider;
                if (provider != null)
                {
                    any = true;
                    await provider.ClearSmallFilesCache();
                }
            }

            if (!any)
            {
                throw new InvalidOperationException("Mount at least one cloud");
            }
        }

        internal void DeleteCloud(CloudMount cloud)
        {
            Clouds.Remove(cloud);
            var settings = Gui.Properties.Settings.Default;
            settings.Clouds = new CloudInfoCollection(Clouds.Select(c => c.CloudInfo));
            settings.Save();
        }

        internal bool GetAutorun()
        {
            using (var rk = Registry.CurrentUser.OpenSubKey(RegistryAutorunPath, true))
            {
                return rk.GetValue(AppName) != null;
            }
        }

        internal void NotifyUnmount(string id)
        {
            var toremove = uploadFiles.Where(f => f.CloudId == id).ToList();
            foreach (var item in toremove)
            {
                uploadFiles.Remove(item);
            }
        }

        internal void SaveClouds()
        {
            var settings = Gui.Properties.Settings.Default;
            settings.Clouds = new CloudInfoCollection(Clouds.Select(c => c.CloudInfo));
            settings.Save();
        }

        internal void SetAutorun(bool isChecked)
        {
            using (var rk = Registry.CurrentUser.OpenSubKey(RegistryAutorunPath, true))
            {
                if (isChecked)
                {
                    var uri = new Uri(Assembly.GetExecutingAssembly().CodeBase);
                    var path = uri.LocalPath + Uri.UnescapeDataString(uri.Fragment).Replace("/", "\\");
                    rk.SetValue(AppName, $"\"{path}\" /mount");
                }
                else
                {
                    rk.DeleteValue(AppName, false);
                }
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    startedMutex.Dispose();
                    if (NotifyIcon != null)
                    {
                        NotifyIcon.Dispose();
                    }
                }

                disposedValue = true;
            }
        }

        private void Application_Exit(object sender, ExitEventArgs e)
        {
            if (NotifyIcon != null)
            {
                NotifyIcon.Dispose();
                NotifyIcon = null;
            }

            foreach (var cloud in Clouds)
            {
                cloud.UnmountAsync().Wait(500);
            }
        }

        private async void Application_Startup(object sender, StartupEventArgs e)
        {
            Log.Info("Starting Version " + Assembly.GetEntryAssembly().GetName().Version);

            if (Gui.Properties.Settings.Default.NeedUpgrade)
            {
                Gui.Properties.Settings.Default.Upgrade();

                try
                {
                    UpdateSettingsV1();
                }
                catch (Exception ex)
                {
                    Log.Error(ex);
                }

                Gui.Properties.Settings.Default.NeedUpgrade = false;
                Gui.Properties.Settings.Default.Save();
            }

            bool created;
            startedMutex = new Mutex(false, AppName, out created);
            if (!created)
            {
                Shutdown();
                return;
            }

            MainWindow = new MainWindow();
            SetupNotifyIcon();

            updateCheckTimer = new DispatcherTimer()
            {
                Interval = new TimeSpan(1, 0, 0, 0, 0),
            };
            updateCheckTimer.Tick += UpdateCheckTimer_Tick;
            updateCheckTimer.Start();
            await UpdateCheck();

            MainWindow.Closing += (s2, e2) =>
            {
                if (!shuttingdown)
                {
                    ShowSettingsBalloon();
                }
            };

            if (GetAutorun())
            {
                await MountDefault();
            }

            if (e.Args.Length > 0)
            {
                // ProcessArgs(e.Args);
                return;
            }

            MainWindow.Show();
        }

        private void DownloadUpdate()
        {
            Process.Start(UpdateAvailable.Assets.First(a => a.Name == "ACDDokanNetInstaller.msi").BrowserUrl);
        }

        private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
        {
            MenuExit_Click();
        }

        private void MenuExit_Click()
        {
            if (UploadingCount > 0)
            {
                if (System.Windows.MessageBox.Show("Some files are not uploaded yet", "Are you sure?", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                {
                    return;
                }
            }

            shuttingdown = true;
            Shutdown();
        }

        private async Task MountDefault()
        {
            foreach (var cloud in Clouds.Where(c => c.CloudInfo.AutoMount))
            {
                try
                {
                    await cloud.MountAsync(false);
                }
                catch (Exception ex)
                {
                    Log.Error(ex);
                }
            }
        }

        private void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private void SettingsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            OpenSettings();
        }

        private void SetupNotifyIcon()
        {
            NotifyIcon = (TaskbarIcon)FindResource("MyNotifyIcon");
        }

        private void ShowSettingsBalloon()
        {
            NotifyIcon.ShowBalloonTip(string.Empty, "Settings window is still accessible from here.\r\nTo close application totally click here with right button and select Exit.", BalloonIcon.None);
        }

        private async Task UpdateCheck()
        {
            UpdateAvailable = await updateCheck.CheckUpdate();

            if (UpdateAvailable != null)
            {
                OnPropertyChanged(nameof(UpdateAvailable));
                NotifyIcon.ShowBalloonTip(AppName, $"Update to {updateAvailable.Version} is available", BalloonIcon.None);
            }
        }

        private async void UpdateCheckTimer_Tick(object sender, EventArgs e)
        {
            await UpdateCheck();
        }

        private void UpdateSettingsV1()
        {
            var settings = Gui.Properties.Settings.Default;
            if (settings.Clouds != null && settings.Clouds.Count > 0)
            {
                return;
            }

            var versionsPath = Environment.ExpandEnvironmentVariables("%LOCALAPPDATA%\\Rambalac\\ACD.DokanNet.Gui.exe_Url_3nx2g2l53nrtm2p32mr11ar350hkhmgy");
            var topVersion = Directory.GetDirectories(versionsPath).OrderByDescending(s => Path.GetFileName(s)).FirstOrDefault();
            if (topVersion == null)
            {
                return;
            }

            var config = Path.Combine(topVersion, "user.config");

            XmlDocument doc = new XmlDocument();
            doc.Load(config);

            var authinfo = new AmazonCloudDrive.AuthInfo
            {
                AuthToken = doc.SelectSingleNode("//setting[@name='AuthToken']/value").InnerText,
                AuthRenewToken = doc.SelectSingleNode("//setting[@name='AuthRenewToken']/value").InnerText,
                AuthTokenExpiration = DateTime.Parse(doc.SelectSingleNode("//setting[@name='AuthTokenExpiration']/value").InnerText)
            };

            var readOnly = bool.Parse(doc.SelectSingleNode("//setting[@name='ReadOnly']/value").InnerText);
            var letter = doc.SelectSingleNode("//setting[@name='LastDriveLetter']/value").InnerText[0];

            var cloudinfo = new CloudInfo
            {
                Id = Guid.NewGuid().ToString(),
                Name = "Amazon Cloud Drive",
                ClassName = "Azi.Cloud.AmazonCloudDrive.AmazonCloud",
                AssemblyFileName = "Clouds.AmazonCloudDrive.dll",
                DriveLetter = letter,
                ReadOnly = readOnly,
                AuthSave = JsonConvert.SerializeObject(authinfo)
            };
            settings.Clouds = new CloudInfoCollection(new CloudInfo[] { cloudinfo });

            var cacheFolder = Environment.ExpandEnvironmentVariables(settings.CacheFolder);
            var newPath = Path.Combine(cacheFolder, UploadService.UploadFolder, cloudinfo.Id);
            Directory.CreateDirectory(newPath);

            var files = Directory.GetFiles(Path.Combine(cacheFolder, UploadService.UploadFolder));

            foreach (var file in files)
            {
                File.Move(file, Path.Combine(newPath, Path.GetFileName(file)));
            }
        }
    }
}