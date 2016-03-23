using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using Azi.Tools;
using Microsoft.Win32;
using Application = System.Windows.Application;
using Azi.Cloud.Common;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.IO;
using System.Diagnostics;

namespace Azi.Cloud.DokanNet.Gui
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application, IDisposable
    {
        public static new App Current => Application.Current as App;

        public FSProvider.StatisticsUpdated OnProviderStatisticsUpdated { get; set; }

        public int UploadingCount => Clouds.Sum(c => c.UploadingCount);

        public int DownloadingCount => Clouds.Sum(c => c.DownloadingCount);

        private ObservableCollection<CloudMount> clouds;

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

        public event Action<string> OnMountChanged;

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
                AssemblyName = selectedItem.AssemblyName,
                DriveLetter = letters[0]
            };
            var mount = new CloudMount(info);
            Clouds.Add(mount);
            var settings = Gui.Properties.Settings.Default;
            settings.Clouds = new CloudInfoCollection(Clouds.Select(c => c.CloudInfo));
            settings.Save();
        }

        internal void DeleteCloud(CloudMount cloud)
        {
            Clouds.Remove(cloud);
        }

        private void ProcessArgs(string[] args)
        {
        }

        private NotifyIcon notifyIcon;

        public void ProviderStatisticsUpdated(CloudInfo cloudn, int downloading, int uploading)
        {
            OnProviderStatisticsUpdated?.Invoke(downloading, uploading);
        }

        private void SetupNotifyIcon()
        {
            var components = new System.ComponentModel.Container();
            notifyIcon = new NotifyIcon(components);

            var contextMenu = new ContextMenu(
                        new MenuItem[]
                        {
                            new MenuItem("&Settings", (s, e) => OpenSettings()),
                            new MenuItem("-"),
                            new MenuItem("E&xit", (s, e) => MenuExit_Click())
                        });

            notifyIcon.Icon = Gui.Properties.Resources.app_all;
            notifyIcon.ContextMenu = contextMenu;

            notifyIcon.Text = $"Amazon Cloud Drive Dokan.NET driver settings.";
            notifyIcon.Visible = true;

            notifyIcon.MouseClick += (sender, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    ShowBalloon();
                }
            };
        }

        private void ShowBalloon()
        {
            notifyIcon.ShowBalloonTip(5000, "State", $"Downloading: {DownloadingCount}\r\nUploading: {UploadingCount}", ToolTipIcon.None);
        }

        private void OpenSettings()
        {
            MainWindow.Show();
            MainWindow.Activate();
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

        internal void MountChanged(CloudInfo cloudInfo)
        {
            // throw new NotImplementedException();
        }

        private Mutex startedMutex;
        private bool shuttingdown = false;

        private async Task MountDefault()
        {
            foreach (var cloud in Clouds.Where(c => c.CloudInfo.AutoMount))
            {
                try
                {
                    await cloud.StartMount(false);
                }
                catch (Exception ex)
                {
                    Log.Error(ex);
                }
            }
        }

        private async void Application_Startup(object sender, StartupEventArgs e)
        {
            Log.Info("Starting Version " + Assembly.GetEntryAssembly().GetName().Version.ToString());

            if (Gui.Properties.Settings.Default.NeedUpgrade)
            {
                Gui.Properties.Settings.Default.Upgrade();
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

            MainWindow.Closing += (s2, e2) =>
            {
                if (!shuttingdown)
                {
                    //Shutdown(); // TODO remove
                    notifyIcon.ShowBalloonTip(5000, string.Empty, "Settings window is still accessible from here.\r\nTo close application totally click here with right button and select Exit.", ToolTipIcon.None);
                }
            };

            if (GetAutorun())
            {
                await MountDefault();
            }

            if (e.Args.Length > 0)
            {
                ProcessArgs(e.Args);
                return;
            }

            MainWindow.Show();
        }

        internal void ClearCache()
        {
            // TODO
            // if (provider != null)
            // {
            //    provider.ClearSmallFilesCache();
            // }
        }

        private const string AppName = "ACDDokanNet";

        internal void SetAutorun(bool isChecked)
        {
            using (var rk = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true))
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

        internal bool GetAutorun()
        {
            using (var rk = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true))
            {
                return rk.GetValue(AppName) != null;
            }
        }

        private async void Application_Exit(object sender, ExitEventArgs e)
        {
            if (notifyIcon != null)
            {
                notifyIcon.Dispose();
                notifyIcon = null;
            }

            foreach (var cloud in Clouds)
            {
                await cloud.StartUnmount();
            }
        }

        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    startedMutex.Dispose();
                    if (notifyIcon != null)
                    {
                        notifyIcon.Dispose();
                    }
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.
                disposedValue = true;
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);

            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
    }
}
