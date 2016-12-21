namespace Azi.Cloud.DokanNet.Gui
{
    using System;
    using System.Diagnostics.Contracts;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows;
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
    public partial class App : IDisposable
    {
        private const string AppName = "ACDDokanNet";
        private const string RegistryAutorunPath = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run";
        private readonly UpdateChecker updateCheck = new UpdateChecker(47739891);
        private bool disposedValue;

        private Mutex startedMutex;
        private DispatcherTimer updateCheckTimer;
        private CommandLineProcessor commandLineProcessor;

        public static bool IsShuttingDown { get; private set; }

        public static App MyApp => Current as App;

        public ViewModel Model => FindResource("Model") as ViewModel;

        public TaskbarIcon NotifyIcon { get; private set; }

        public void Dispose()
        {
            Dispose(true);
        }

        public void OpenSettings()
        {
            MainWindow.Show();
            MainWindow.Activate();
        }

        internal void CancelUpload(FileItemInfo item)
        {
            var cloud = Model.Clouds.SingleOrDefault(c => c.CloudInfo.Id == item.CloudId);

            cloud?.Provider.CancelUpload(item.Id);
        }

        internal async Task ClearCache()
        {
            var any = false;
            foreach (var mount in Model.Clouds)
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

        internal bool GetAutorun()
        {
            using (var rk = Registry.CurrentUser.OpenSubKey(RegistryAutorunPath, true))
            {
                Contract.Assert(rk != null, "rk != null");
                return rk.GetValue(AppName) != null;
            }
        }

        internal void SetAutorun(bool isChecked)
        {
            using (var rk = Registry.CurrentUser.OpenSubKey(RegistryAutorunPath, true))
            {
                Contract.Assert(rk != null, "rk != null");
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
                    NotifyIcon?.Dispose();
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

            foreach (var cloud in Model.Clouds)
            {
                cloud.UnmountAsync().Wait(500);
            }
        }

        private async void Application_Startup(object sender, StartupEventArgs e)
        {
            try
            {
                Log.Info("Starting Version " + Assembly.GetEntryAssembly().GetName().Version);

                System.Net.ServicePointManager.DefaultConnectionLimit = 100;

                if (!VirtualDriveWrapper.IsDokanInstalled)
                {
                    await ShowMessage("Please install Dokan 1.0.1 to use this application");
                    Shutdown();
                }

                try
                {
                    var test = Gui.Properties.Settings.Default.NeedUpgrade;
                }
                catch (Exception ex)
                {
                    Log.Error($"Settings file got currupted. Resetting\r\n{ex}");
                    Gui.Properties.Settings.Default.Reset();
                    Gui.Properties.Settings.Default.Save();
                    await ShowMessage("Settings file got currupted and was reset");
                }

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

                CreateServerPipe();

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
                    if (!IsShuttingDown)
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
            catch (Exception ex)
            {
                Log.Error(ex);
            }
        }

        private void CreateServerPipe()
        {
            commandLineProcessor = new CommandLineProcessor(Model, AppName);
            commandLineProcessor.Start();
        }

        private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
        {
            MenuExit_Click();
        }

        private void MenuExit_Click()
        {
            if (Model.UploadFiles.Count > 0)
            {
                try
                {
                    if (MessageBox.Show("Some files are not uploaded yet", "Are you sure?", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No, MessageBoxOptions.DefaultDesktopOnly) != MessageBoxResult.Yes)
                    {
                        return;
                    }
                }
                catch (Exception e)
                {
                    Log.Error(e);
                }
            }

            IsShuttingDown = true;
            commandLineProcessor.Stop();
            Shutdown();
        }

        private async Task MountDefault()
        {
            foreach (var cloud in Model.Clouds.Where(c => c.CloudInfo.AutoMount))
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

        private void SetupNotifyIcon()
        {
            NotifyIcon = (TaskbarIcon)FindResource("MyNotifyIcon");
        }

        private async Task ShowMessage(string message)
        {
            var task = Dispatcher.BeginInvoke(new Action<App>(s => { MessageBox.Show(message); }), this);
            await task;
        }

        private void ShowSettingsBalloon()
        {
            NotifyIcon.ShowBalloonTip(string.Empty, "Settings window is still accessible from here.\r\nTo close application totally click here with right button and select Exit.", BalloonIcon.None);
        }

        private async Task UpdateCheck()
        {
            try
            {
                Model.UpdateAvailable = await updateCheck.CheckUpdate();

                if (Model.UpdateAvailable != null)
                {
                    NotifyIcon.ShowBalloonTip(AppName, $"Update to {Model.UpdateAvailable.Version} is available.\r\n{Model.UpdateAvailable.Description}", BalloonIcon.None);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex);
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
            var topVersion = Directory.GetDirectories(versionsPath).OrderByDescending(Path.GetFileName).FirstOrDefault();
            if (topVersion == null)
            {
                return;
            }

            var config = Path.Combine(topVersion, "user.config");

            var doc = new XmlDocument();
            doc.Load(config);

            var authinfo = new AmazonCloudDrive.AuthInfo
            {
                AuthToken = doc.SelectSingleNode("//setting[@name='AuthToken']/value")?.InnerText,
                AuthRenewToken = doc.SelectSingleNode("//setting[@name='AuthRenewToken']/value")?.InnerText,
                AuthTokenExpiration = DateTime.Parse(doc.SelectSingleNode("//setting[@name='AuthTokenExpiration']/value")?.InnerText)
            };

            var readOnly = bool.Parse(doc.SelectSingleNode("//setting[@name='ReadOnly']/value")?.InnerText ?? "false");
            var letter = doc.SelectSingleNode("//setting[@name='LastDriveLetter']/value")?.InnerText[0] ?? VirtualDriveWrapper.GetFreeDriveLettes().FirstOrDefault();

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
            settings.Clouds = new CloudInfoCollection(new[] { cloudinfo });

            var cacheFolder = Environment.ExpandEnvironmentVariables(settings.CacheFolder);
            var newPath = Path.Combine(cacheFolder, UploadService.UploadFolder, cloudinfo.Id);
            Directory.CreateDirectory(newPath);

            var files = Directory.GetFiles(Path.Combine(cacheFolder, UploadService.UploadFolder));

            foreach (var file in files)
            {
                var fileName = Path.GetFileName(file);
                Contract.Assert(fileName != null, "fileName != null");
                File.Move(file, Path.Combine(newPath, fileName));
            }
        }
    }
}