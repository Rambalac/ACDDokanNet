using Azi.Amazon.CloudDrive;
using Azi.Tools;
using Microsoft.Win32;
using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using Application = System.Windows.Application;

namespace Azi.ACDDokanNet.Gui
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application, IDisposable
    {
        public new static App Current => Application.Current as App;

        public bool IsMounted => mountedLetter != null;
        public char? MountedLetter => mountedLetter;

        public FSProvider.StatisticsUpdated OnProviderStatisticsUpdated { get; set; }
        public Action OnMountChanged;

        public long SmallFileSizeLimit
        {
            get
            {
                return Gui.Properties.Settings.Default.SmallFileSizeLimit;
            }

            set
            {
                if (provider != null)
                    provider.SmallFileSizeLimit = value * (1 << 20);
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
                if (provider != null)
                    provider.CachePath = Environment.ExpandEnvironmentVariables(value);
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
                if (provider != null)
                    provider.SmallFilesCacheSize = value * (1 << 20);
                Gui.Properties.Settings.Default.SmallFilesCacheLimit = value;
                Gui.Properties.Settings.Default.Save();
            }
        }


        void ProcessArgs(string[] args)
        {

        }

        NotifyIcon notifyIcon;
        int uploading = 0;
        int downloading = 0;

        public void ProviderStatisticsUpdated(int downloading, int uploading)
        {
            this.uploading = uploading;
            this.downloading = downloading;
            OnProviderStatisticsUpdated?.Invoke(downloading, uploading);
        }

        void SetupNotifyIcon()
        {
            var components = new System.ComponentModel.Container();
            notifyIcon = new NotifyIcon(components);

            var contextMenu = new ContextMenu(
                        new MenuItem[] {
                            new MenuItem("&Settings", (s,e)=>OpenSettings()),
                            new MenuItem("-"),
                            new MenuItem("E&xit", (s,e)=>menuExit_Click())
                        });


            notifyIcon.Icon = Gui.Properties.Resources.app_all;
            notifyIcon.ContextMenu = contextMenu;

            notifyIcon.Text = $"Amazon Cloud Drive Dokan.NET driver settings.";
            notifyIcon.Visible = true;

            notifyIcon.MouseClick += (sender, e) => { if (e.Button == MouseButtons.Left) ShowBalloon(); };
        }

        private void ShowBalloon()
        {
            notifyIcon.ShowBalloonTip(5000, "State",
                $"Downloading: {downloading}\r\nUploading: {uploading}", ToolTipIcon.None);
        }

        void OpenSettings()
        {
            MainWindow.Show();
            MainWindow.Activate();
        }

        private void menuExit_Click()
        {
            if (uploading > 0)
                if (System.Windows.MessageBox.Show("Some files are not uploaded yet", "Are you sure?", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
            shuttingdown = true;
            Shutdown();
        }

        Mutex startedMutex;
        bool shuttingdown = false;

        private void MountDefault()
        {
            var cs = new CancellationTokenSource();
            Task task = Mount(Gui.Properties.Settings.Default.LastDriveLetter, Gui.Properties.Settings.Default.ReadOnly, cs.Token, false);
        }

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            Log.Info("Starting Version " + Assembly.GetEntryAssembly().GetName().Version.ToString());
            if (Gui.Properties.Settings.Default.NeedUpgrade)
            {
                Gui.Properties.Settings.Default.Upgrade();
                Gui.Properties.Settings.Default.NeedUpgrade = false;
                Gui.Properties.Settings.Default.Save();
            }


            bool created;
            startedMutex = new Mutex(false, appName, out created);
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
                    notifyIcon.ShowBalloonTip(5000, "", "Settings window is still accessible from here.\r\nTo close application totally click here with right button and select Exit.", ToolTipIcon.None);
            };

            if (GetAutorun()) MountDefault();


            if (e.Args.Length > 0)
            {
                ProcessArgs(e.Args);
                return;
            }

            MainWindow.Show();
        }

        static char? mountedLetter = null;
        object mountLock = new object();
        VirtualDriveWrapper cloudDrive;
        FSProvider provider;

        async Task<AmazonDrive> Authenticate(CancellationToken cs, bool interactiveAuth = true)
        {
            var settings = Gui.Properties.Settings.Default;
            var amazon = new AmazonDrive(AmazonSecret.clientId, AmazonSecret.clientSecret);
            amazon.OnTokenUpdate = (token, renew, expire) =>
            {
                settings.AuthToken = token;
                settings.AuthRenewToken = renew;
                settings.AuthTokenExpiration = expire;
                settings.Save();
            };

            if (!string.IsNullOrWhiteSpace(settings.AuthRenewToken))
            {
                if (await amazon.Authentication(
                    settings.AuthToken,
                    settings.AuthRenewToken,
                    settings.AuthTokenExpiration)) return amazon;
            }
            if (interactiveAuth)
                if (await amazon.SafeAuthenticationAsync(CloudDriveScope.ReadAll | CloudDriveScope.Write, TimeSpan.FromMinutes(10), cs)) return amazon;
            cs.ThrowIfCancellationRequested();
            return null;
        }

        internal void ClearCredentials()
        {
            var settings = Gui.Properties.Settings.Default;
            settings.AuthToken = null;
            settings.AuthRenewToken = null;
            settings.AuthTokenExpiration = DateTime.MinValue;
            settings.Save();
        }

        internal void ClearCache()
        {
            if (provider != null) provider.ClearSmallFilesCache();
        }

        const string appName = "ACDDokanNet";

        internal void SetAutorun(bool isChecked)
        {
            using (var rk = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true))
            {
                if (isChecked)
                {
                    var uri = new Uri(Assembly.GetExecutingAssembly().CodeBase);
                    var path = uri.LocalPath + Uri.UnescapeDataString(uri.Fragment).Replace("/", "\\");
                    rk.SetValue(appName, $"\"{path}\" /mount");
                }
                else
                    rk.DeleteValue(appName, false);
            }
        }

        internal bool GetAutorun()
        {
            using (var rk = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true))
            {
                return rk.GetValue(appName) != null;
            }
        }
        internal async Task Unmount()
        {
            if (mounted == 0) return;
            VirtualDriveWrapper.Unmount((char)mountedLetter);
            await mountTask;
        }


        Task mountTask;
        int mounted = 0;
        internal async Task<char?> Mount(char driveLetter, bool readOnly, CancellationToken cs, bool interactiveAuth = true)
        {
            if (Interlocked.CompareExchange(ref mounted, 1, 0) != 0) return null;

            var mountedEvent = new TaskCompletionSource<char>();

            mountTask = Task.Factory.StartNew(async () =>
              {
                  try
                  {
                      lock (mountLock)
                      {
                          if (mountedLetter != null) return;
                          mountedLetter = driveLetter;
                      }
                      AmazonDrive amazon = await Authenticate(cs, interactiveAuth);
                      if (amazon == null)
                      {
                          Log.Error("Authentication failed");
                          mountedEvent.SetException(new InvalidOperationException("Authentication failed"));
                          return;
                      }

                      provider = new FSProvider(amazon);
                      provider.CachePath = Environment.ExpandEnvironmentVariables(Gui.Properties.Settings.Default.CacheFolder);
                      provider.SmallFilesCacheSize = Gui.Properties.Settings.Default.SmallFilesCacheLimit * (1 << 20);
                      provider.SmallFileSizeLimit = Gui.Properties.Settings.Default.SmallFileSizeLimit * (1 << 20);
                      provider.OnStatisticsUpdated = ProviderStatisticsUpdated;
                      cloudDrive = new VirtualDriveWrapper(provider);
                      cloudDrive.Mounted = () =>
                      {
                          mountedEvent.SetResult((char)mountedLetter);
                      };

                      OnMountChanged?.Invoke();
                      try
                      {
                          cloudDrive.Mount(mountedLetter + ":\\", readOnly);
                          mountedLetter = null;
                      }
                      catch (InvalidOperationException)
                      {
                          Log.Warn($"Drive letter {mountedLetter} is already used");
                          Exception lastException = null;
                          foreach (char letter in VirtualDriveWrapper.GetFreeDriveLettes())
                          {
                              try
                              {
                                  mountedLetter = letter;
                                  cloudDrive.Mount(mountedLetter + ":\\", readOnly);
                                  break;
                              }
                              catch (InvalidOperationException ex)
                              {
                                  lastException = ex;
                                  Log.Warn($"Drive letter {letter} is already used");
                              }
                          }
                          if (mountedLetter != null)
                          {
                              var message = "Could not find free letter";
                              if (lastException != null && lastException.InnerException != null) message = lastException.InnerException.Message;
                              mountedEvent.SetException(new InvalidOperationException(message));
                          }
                      }
                  }
                  catch (Exception ex)
                  {
                      mountedEvent.SetException(ex);
                  }
                  finally
                  {
                      mountedLetter = null;
                      mounted = 0;
                      OnMountChanged?.Invoke();
                  }
              }, TaskCreationOptions.LongRunning).Unwrap();
            return await mountedEvent.Task;
        }

        private void Application_Exit(object sender, ExitEventArgs e)
        {
            if (notifyIcon != null) notifyIcon.Dispose();
            if (mounted == 0) return;
            VirtualDriveWrapper.Unmount((char)mountedLetter);
            mountTask.Wait();
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    startedMutex.Dispose();
                    notifyIcon.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~App() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion

    }
}
