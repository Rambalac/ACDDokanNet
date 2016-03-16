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
using Azi.Cloud.AmazonCloudDrive;

namespace Azi.Cloud.DokanNet.Gui
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application, IDisposable, IAuthUpdateListener
    {
        public static new App Current => Application.Current as App;

        public bool IsMounted => mountedLetter != null;

        public char? MountedLetter => mountedLetter;

        public FSProvider.StatisticsUpdated OnProviderStatisticsUpdated { get; set; }

        public event Action<string> OnMountChanged;

        public long SmallFileSizeLimit
        {
            get
            {
                return Gui.Properties.Settings.Default.SmallFileSizeLimit;
            }

            set
            {
                if (provider != null)
                {
                    provider.SmallFileSizeLimit = value * (1 << 20);
                }

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
                {
                    provider.CachePath = Environment.ExpandEnvironmentVariables(value);
                }

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
                {
                    provider.SmallFilesCacheSize = value * (1 << 20);
                }

                Gui.Properties.Settings.Default.SmallFilesCacheLimit = value;
                Gui.Properties.Settings.Default.Save();
            }
        }

        private void ProcessArgs(string[] args)
        {
        }

        private NotifyIcon notifyIcon;
        private int uploading = 0;
        private int downloading = 0;

        public void ProviderStatisticsUpdated(int downloading, int uploading)
        {
            this.uploading = uploading;
            this.downloading = downloading;
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
            notifyIcon.ShowBalloonTip(5000, "State", $"Downloading: {downloading}\r\nUploading: {uploading}", ToolTipIcon.None);
        }

        private void OpenSettings()
        {
            MainWindow.Show();
            MainWindow.Activate();
        }

        private void MenuExit_Click()
        {
            if (uploading > 0)
            {
                if (System.Windows.MessageBox.Show("Some files are not uploaded yet", "Are you sure?", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                {
                    return;
                }
            }

            shuttingdown = true;
            Shutdown();
        }

        private Mutex startedMutex;
        private bool shuttingdown = false;

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
                    notifyIcon.ShowBalloonTip(5000, string.Empty, "Settings window is still accessible from here.\r\nTo close application totally click here with right button and select Exit.", ToolTipIcon.None);
                }
            };

            if (GetAutorun())
            {
                MountDefault();
            }

            if (e.Args.Length > 0)
            {
                ProcessArgs(e.Args);
                return;
            }

            MainWindow.Show();
        }

        private static char? mountedLetter = null;
        private object mountLock = new object();
        private VirtualDriveWrapper cloudDrive;
        private FSProvider provider;

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
            if (provider != null)
            {
                provider.ClearSmallFilesCache();
            }
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

        internal async Task Unmount()
        {
            if (mounted == 0)
            {
                return;
            }

            VirtualDriveWrapper.Unmount((char)mountedLetter);
            await mountTask;
        }

        private Task mountTask;
        private int mounted = 0;

        internal async Task<char?> Mount(char driveLetter, bool readOnly, CancellationToken cs, bool interactiveAuth = true)
        {
            if (Interlocked.CompareExchange(ref mounted, 1, 0) != 0)
            {
                return null;
            }

            var mountedEvent = new TaskCompletionSource<char>();

            mountTask = Task.Factory.StartNew(
                async () =>
              {
                  try
                  {
                      lock (mountLock)
                      {
                          if (mountedLetter != null)
                          {
                              return;
                          }

                          mountedLetter = driveLetter;
                      }
                      var cloud = new AmazonCloud();
                      cloud.OnAuthUpdated = this;
                      await Authenticate(cloud, cs, interactiveAuth);

                      if (cloud == null)
                      {
                          Log.Error("Authentication failed");
                          mountedEvent.SetException(new InvalidOperationException("Authentication failed"));
                          return;
                      }

                      provider = new FSProvider(cloud);
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
                              if (lastException != null && lastException.InnerException != null)
                              {
                                  message = lastException.InnerException.Message;
                              }

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

        private async Task<bool> Authenticate(IHttpCloud cloud, CancellationToken cs, bool interactiveAuth)
        {
            var settings = Gui.Properties.Settings.Default;
            var authinfo = settings.AuthToken;
            if (string.IsNullOrWhiteSpace(authinfo))
            {
                if (!interactiveAuth)
                {
                    return false;
                }

                await cloud.AuthenticateNew(cs);

                return true;
            }

            await cloud.AuthenticateSaved(cs, authinfo);

            return true;
        }

        private void Application_Exit(object sender, ExitEventArgs e)
        {
            if (notifyIcon != null)
            {
                notifyIcon.Dispose();
            }

            if (mounted == 0)
            {
                return;
            }

            VirtualDriveWrapper.Unmount((char)mountedLetter);
            mountTask.Wait();
        }

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

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);

            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }

        public void OnAuthUpdated(IHttpCloud sender, string authinfo)
        {
            var settings = Gui.Properties.Settings.Default;
            settings.AuthToken = authinfo;
            settings.Save();
        }
    }
}
