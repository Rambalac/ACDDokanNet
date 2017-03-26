namespace Azi.Cloud.DokanNet.Gui
{
    using System;
    using System.Configuration;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Threading;
    using Hardcodet.Wpf.TaskbarNotification;
    using Microsoft.Win32;
    using Tools;

    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : IDisposable
    {
        private const string AppName = "ACDDokanNet";
        private const string RegistryAutorunPath = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run";
        private readonly UpdateChecker updateCheck = new UpdateChecker(47739891);
        private CommandLineProcessor commandLineProcessor;
        private bool disposedValue;

        private Mutex startedMutex;
        private DispatcherTimer updateCheckTimer;

        public static bool IsShuttingDown { get; private set; }

        public static App MyApp => Current as App;

        public ViewModel Model => FindResource("Model") as ViewModel;

        public TaskbarIcon NotifyIcon { get; private set; }

        public void Dispose()
        {
            Dispose(true);
        }

        public bool GetAutorun()
        {
            using (var rk = Registry.CurrentUser.OpenSubKey(RegistryAutorunPath, true))
            {
                if (rk == null)
                {
                    throw new InvalidOperationException("rk is null");
                }

                return rk.GetValue(AppName) != null;
            }
        }

        public void OpenSettings()
        {
            MainWindow.Show();
            MainWindow.Activate();
        }

        public void SetAutorun(bool isChecked)
        {
            using (var rk = Registry.CurrentUser.OpenSubKey(RegistryAutorunPath, true))
            {
                if (rk == null)
                {
                    throw new InvalidOperationException("rk is null");
                }

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

        private void App_OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Log.Error(e.Exception);
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
                var version = Assembly.GetEntryAssembly().GetName().Version.ToString();
                await Log.Init(version);
                Log.Info("Starting Version " + version);

                System.Net.ServicePointManager.DefaultConnectionLimit = 100;

                if (!VirtualDriveWrapper.IsDokanInstalled)
                {
                    await ShowMessage("Please install Dokan 1.0.1 to use this application");
                    Shutdown();
                }

                Model.BuildAvailableClouds();
                Model.LoadClouds();

                try
                {
                    var test = Gui.Properties.Settings.Default.NeedUpgrade;
                    if (test)
                    {
                        Log.Info("Updating setting");
                    }
                }
                catch (Exception ex)
                {
                    Log.Error("Settings file got currupted. Resetting", ex);
                    var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal);
                    try
                    {
                        File.Delete(config.FilePath);
                    }
                    catch (Exception ex2)
                    {
                        Log.Error("Could not delete settings file", ex2);
                    }

                    await ShowMessage("Settings file got currupted and was reset");
                }

                if (Gui.Properties.Settings.Default.NeedUpgrade)
                {
                    Gui.Properties.Settings.Default.Upgrade();

                    Gui.Properties.Settings.Default.NeedUpgrade = false;
                    Gui.Properties.Settings.Default.Save();
                }

                startedMutex = new Mutex(false, AppName, out bool created);
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
    }
}