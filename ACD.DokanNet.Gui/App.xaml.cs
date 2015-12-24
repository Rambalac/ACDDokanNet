using Azi.Amazon.CloudDrive;
using System;
using System.Threading.Tasks;
using System.Windows;

namespace Azi.ACDDokanNet.Gui
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public new static App Current => (App)Application.Current;
        void ProcessArgs(string[] args)
        {

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

        async Task<AmazonDrive> Authenticate()
        {
            var settings = ACD.DokanNet.Gui.Properties.Settings.Default;
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

        internal void Unmount()
        {
            lock (mountLock)
            {
                if (mountedLetter == null) return;

                VirtualDriveWrapper.Unmount((char)mountedLetter);

                mountedLetter = null;
            }
        }

        internal void Mount(char driveLetter)
        {
            Task.Run(async () =>
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


                var cloudDrive = new VirtualDriveWrapper(new FSProvider(amazon));
                cloudDrive.Mount(mountedLetter + ":\\");
                mountedLetter = null;
            });
        }

        private void Application_Exit(object sender, ExitEventArgs e)
        {
            Unmount();
        }
    }
}
