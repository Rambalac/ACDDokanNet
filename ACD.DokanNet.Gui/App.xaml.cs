using Azi.Amazon.CloudDrive;
using Microsoft.Win32;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
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

        public FSProvider.StatisticsUpdated OnProviderStatisticsUpdated { get; set; }

        void ProcessArgs(string[] args)
        {

        }

        NotifyIcon notifyIcon;

        public void ProviderStatisticsUpdated(int downloading, int uploading)
        {
            OnProviderStatisticsUpdated?.Invoke(downloading, uploading);
        }

        void SetupNotifyIcon()
        {
            var components = new System.ComponentModel.Container();
            notifyIcon = new NotifyIcon(components);

            var contextMenu = new ContextMenu();
            var menuItem = new MenuItem();

            // Initialize contextMenu1
            contextMenu.MenuItems.AddRange(
                        new MenuItem[] { menuItem });

            // Initialize menuItem1
            menuItem.Index = 0;
            menuItem.Text = "E&xit";
            menuItem.Click += menuItem_Click;

            notifyIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(
             System.Reflection.Assembly.GetEntryAssembly().ManifestModule.Name);

            // The ContextMenu property sets the menu that will
            // appear when the systray icon is right clicked.
            notifyIcon.ContextMenu = contextMenu;

            // The Text property sets the text that will be displayed,
            // in a tooltip, when the mouse hovers over the systray icon.
            notifyIcon.Text = "Form1 (NotifyIcon example)";
            notifyIcon.Visible = true;

            // Handle the DoubleClick event to activate the form.
            notifyIcon.DoubleClick += notifyIcon_DoubleClick;

        }

        private void notifyIcon_DoubleClick(object sender, EventArgs e)
        {
            if (mainWindow != null)
            {
                mainWindow.Focus();
                return;
            }
            mainWindow = new MainWindow();
            mainWindow.Show();
        }

        MainWindow mainWindow;
        private void menuItem_Click(object sender, EventArgs e)
        {
            Shutdown();
        }

        Mutex startedMutex;

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            bool created;
            startedMutex = new Mutex(false, appName, out created);
            if (!created)
            {
                Shutdown();
            }

            SetupNotifyIcon();

            Task task;
            if (GetAutorun()) task = Mount(Gui.Properties.Settings.Default.LastDriveLetter);

            if (e.Args.Length > 0)
            {
                ProcessArgs(e.Args);
                return;
            }

            mainWindow = new MainWindow();
            mainWindow.Show();
        }

        static char? mountedLetter = null;
        object mountLock = new object();
        VirtualDriveWrapper cloudDrive;
        FSProvider provider;

        async Task<AmazonDrive> Authenticate()
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
            if (await amazon.SafeAuthenticationAsync(CloudDriveScope.ReadAll | CloudDriveScope.Write, TimeSpan.FromMinutes(10))) return amazon;
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
                    rk.SetValue(appName, System.Reflection.Assembly.GetExecutingAssembly().CodeBase + " /mount");
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
        internal async Task<char?> Mount(char driveLetter)
        {
            if (Interlocked.CompareExchange(ref mounted, 1, 0) != 0) return null;

            var mountedEvent = new TaskCompletionSource<char>();

            mountTask = Task.Factory.StartNew(async () =>
              {
                  lock (mountLock)
                  {
                      if (mountedLetter != null) return;
                      mountedLetter = driveLetter;
                  }
                  var amazon = await Authenticate();
                  if (amazon == null)
                  {
                      throw new InvalidOperationException("Authentication failed");
                  }

                  provider = new FSProvider(amazon);
                  provider.CachePath = Environment.ExpandEnvironmentVariables(Gui.Properties.Settings.Default.CacheFolder);
                  provider.SmallFileCacheSize = Gui.Properties.Settings.Default.SmallFilesCacheLimit;
                  provider.SmallFileSizeLimit = Gui.Properties.Settings.Default.SmallFileSizeLimit;
                  provider.OnStatisticsUpdated = ProviderStatisticsUpdated;
                  cloudDrive = new VirtualDriveWrapper(provider);
                  cloudDrive.Mounted = () =>
                  {
                      mountedEvent.SetResult((char)mountedLetter);
                  };

                  try
                  {
                      cloudDrive.Mount(mountedLetter + ":\\");
                      mountedLetter = null;
                  }
                  catch (InvalidOperationException)
                  {
                      foreach (char letter in VirtualDriveWrapper.GetFreeDriveLettes())
                      {
                          try
                          {
                              mountedLetter = letter;
                              cloudDrive.Mount(mountedLetter + ":\\");
                              mountedLetter = null;
                              break;
                          }
                          catch (InvalidOperationException)
                          {

                          }
                      }
                      if (mountedLetter != null)
                      {
                          mountedLetter = null;
                          mountedEvent.SetException(new InvalidOperationException("Could not find free letter"));
                      }
                  }
                  mounted = 0;
              }, TaskCreationOptions.LongRunning).Unwrap();
            return await mountedEvent.Task;
        }


        private void Application_Exit(object sender, ExitEventArgs e)
        {
            if (mounted != 0)
                VirtualDriveWrapper.Unmount((char)mountedLetter);

            if (notifyIcon != null) notifyIcon.Dispose();
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
