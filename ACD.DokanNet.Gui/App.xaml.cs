using Azi.Amazon.CloudDrive;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Azi.ACDDokanNet.Gui
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public new static App Current => Application.Current as App;

        public bool IsMounted => mountedLetter != null;

        public FSProvider.StatisticsUpdated OnProviderStatisticsUpdated { get; set; }

        void ProcessArgs(string[] args)
        {

        }

        public void ProviderStatisticsUpdated(int downloading, int uploading)
        {
            OnProviderStatisticsUpdated?.Invoke(downloading, uploading);
        }

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            if (e.Args.Length > 0)
            {
                ProcessArgs(e.Args);
                return;
            }

            var win = new MainWindow();
            win.Show();
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


        private async void Application_Exit(object sender, ExitEventArgs e)
        {
            await Unmount();
        }
    }
}
