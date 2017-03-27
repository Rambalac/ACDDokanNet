namespace Azi.Cloud.DokanNet.Gui
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows;
    using Common;
    using DokanNet;
    using Tools;

    public class CloudMount : INotifyPropertyChanged, IDisposable, IAuthUpdateListener
    {
        private readonly CloudInfo cloudInfo;
        private readonly ViewModel model;

        private bool disposedValue;
        private IHttpCloud instance;
        private bool mounting;
        private bool unmounting;
        private ManualResetEventSlim unmountingEvent;

        public CloudMount(CloudInfo info, ViewModel model)
        {
            this.model = model;
            cloudInfo = info;
            cloudInfo.PropertyChanged += CloudInfoChanged;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public bool CanMount => (Instance != null) && (!mounting) && MountLetter == null;

        public bool CanUnmount => (!unmounting) && (MountLetter != null);

        public CloudInfo CloudInfo => cloudInfo;

        public string CloudServiceIcon => Instance?.CloudServiceIcon ?? "images/lib_load_error.png";

        public IList<char> DriveLetters
        {
            get
            {
                var res = VirtualDriveWrapper.GetFreeDriveLettes();

                if (MountLetter != null && !res.Contains((char)MountLetter))
                {
                    res.Add((char)MountLetter);
                }
                else
                if (MountLetter == null && (mounting || unmounting) && !res.Contains(CloudInfo.DriveLetter))
                {
                    res.Add(CloudInfo.DriveLetter);
                }
                else
                {
                    return res;
                }

                return res.OrderBy(c => c).ToList();
            }
        }

        public IHttpCloud Instance
        {
            get
            {
                try
                {
                    if (instance == null)
                    {
                        instance = CreateInstance();
                        instance.Id = CloudInfo.Id;
                    }

                    return instance;
                }
                catch (Exception ex)
                {
                    Log.Error(ex);
                    return null;
                }
            }
        }

        public bool IsMounted => !mounting && !unmounting && (MountLetter != null);

        public bool IsUnmounted => !unmounting && !unmounting && MountLetter == null;

        public CancellationTokenSource MountCancellation { get; } = new CancellationTokenSource();

        public char? MountLetter { get; set; }

        public Visibility MountVisible => (!unmounting && (MountLetter == null)) ? Visibility.Visible : Visibility.Collapsed;

        public IFSProvider Provider { get; private set; }

        public Visibility UnmountVisible => (!mounting && (MountLetter != null)) ? Visibility.Visible : Visibility.Collapsed;

        private App App => App.MyApp;

        public async Task Delete()
        {
            try
            {
                model.DeleteCloud(this);
                await UnmountAsync();
                if (Instance != null)
                {
                    await Instance.SignOut(CloudInfo.AuthSave);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
        }

        public async Task MountAsync(bool interactiveAuth = true)
        {
            if (App == null)
            {
                throw new NullReferenceException();
            }

            mounting = true;
            NotifyMount();
            try
            {
                try
                {
                    MountLetter = await Mount(interactiveAuth);
                }
                catch (TimeoutException)
                {
                    // Ignore if timeout
                }
                catch (OperationCanceledException)
                {
                    // Ignore if aborted
                }
            }
            finally
            {
                mounting = false;
                NotifyMount();
            }
        }

        public void OnAuthUpdated(IHttpCloud sender, string authinfo)
        {
            CloudInfo.AuthSave = authinfo;
            model.SaveClouds();
        }

        public async Task UnmountAsync()
        {
            if (MountLetter == null)
            {
                return;
            }

            if (App == null)
            {
                throw new NullReferenceException();
            }

            unmounting = true;
            NotifyMount();
            try
            {
                await Task.Factory.StartNew(() =>
                {
                    if (MountLetter == null)
                    {
                        return;
                    }

                    using (unmountingEvent = new ManualResetEventSlim(false))
                    {
                        VirtualDriveWrapper.Unmount(MountLetter.Value);
                        unmountingEvent.Wait();
                    }
                });

                MountLetter = null;
                Provider.StopUpload();
                model.NotifyUnmount(cloudInfo.Id);
            }
            finally
            {
                unmounting = false;
                NotifyMount();
            }
        }

        internal void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    MountCancellation.Dispose();
                }

                disposedValue = true;
            }
        }

        private async Task<bool> Authenticate(IHttpCloud cloud, CancellationToken cs, bool interactiveAuth)
        {
            try
            {
                var authinfo = CloudInfo.AuthSave;
                if (!string.IsNullOrWhiteSpace(authinfo))
                {
                    if (await cloud.AuthenticateSaved(cs, authinfo))
                    {
                        return true;
                    }
                }

                Debug.WriteLine("No auth info: " + CloudInfo.Name);
                if (!interactiveAuth)
                {
                    return false;
                }

                return await cloud.AuthenticateNew(cs);
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                return false;
            }
        }

        private void CloudInfoChanged(object sender, PropertyChangedEventArgs e)
        {
            OnPropertyChanged(nameof(CloudInfo));
            if (Provider != null)
            {
                Provider.VolumeName = CloudInfo.Name;
            }

            model.SaveClouds();
        }

        private IHttpCloud CreateInstance()
        {
            var assembly = Assembly.LoadFrom(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, CloudInfo.AssemblyFileName));

            var types = assembly.GetExportedTypes().Where(t => typeof(IHttpCloud).IsAssignableFrom(t));

            var assemblyName = types.Single(t => t.IsClass).Assembly.FullName;

            return Activator.CreateInstance(assemblyName, CloudInfo.ClassName).Unwrap() as IHttpCloud;
        }

        private async Task<char> Mount(bool interactiveAuth = true)
        {
            try
            {
                Instance.OnAuthUpdated = this;
                var authenticated = await Authenticate(Instance, MountCancellation.Token, interactiveAuth);

                if (!authenticated)
                {
                    Log.ErrorTrace("Authentication failed");
                    throw new InvalidOperationException("Authentication failed");
                }

                var origProv = new FSProvider(instance, ProviderStatisticsUpdated)
                {
                    VolumeName = CloudInfo.Name,
                    CachePath = Environment.ExpandEnvironmentVariables(Properties.Settings.Default.CacheFolder),
                    SmallFilesCacheSize = Properties.Settings.Default.SmallFilesCacheLimit * (1 << 20),
                    SmallFileSizeLimit = Properties.Settings.Default.SmallFileSizeLimit * (1 << 20)
                };

                var rfProv = new RootFolderFSProvider(origProv);
                await rfProv.SetRootFolder(CloudInfo.RootFolder);

                Provider = rfProv;

                var cloudDrive = new VirtualDriveWrapper(Provider);

                var mountedEvent = new TaskCompletionSource<char>();

                cloudDrive.Mounted = letter =>
                {
                    mountedEvent.SetResult(letter);
                };

                NotifyMount();

                var task = Task.Factory.StartNew(
                    () =>
                    {
                        try
                        {
                            cloudDrive.Mount(CloudInfo.DriveLetter, CloudInfo.ReadOnly);
                            unmountingEvent.Set();
                        }
                        catch (InvalidOperationException)
                        {
                            Log.Warn($"Drive letter {CloudInfo.DriveLetter} is already used");
                            Exception lastException = null;
                            var wasMounted = false;
                            foreach (var letter in VirtualDriveWrapper.GetFreeDriveLettes())
                            {
                                try
                                {
                                    cloudDrive.Mount(letter, CloudInfo.ReadOnly);
                                    unmountingEvent.Set();
                                    Instance.Dispose();
                                    instance = null;
                                    wasMounted = true;
                                    break;
                                }
                                catch (InvalidOperationException ex)
                                {
                                    lastException = ex;
                                    Log.Warn($"Drive letter {letter} is already used");
                                }
                            }

                            if (!wasMounted)
                            {
                                var message = "Could not find free letter";
                                if (lastException?.InnerException != null)
                                {
                                    message = lastException.InnerException.Message;
                                }

                                mountedEvent.SetException(new InvalidOperationException(message));
                            }
                        }
                        catch (Exception ex)
                        {
                            mountedEvent.SetException(ex);
                        }
                    },
                    TaskCreationOptions.LongRunning);
                return await mountedEvent.Task;
            }
            finally
            {
                NotifyMount();
            }
        }

        private void NotifyMount()
        {
            OnPropertyChanged(nameof(MountVisible));
            OnPropertyChanged(nameof(UnmountVisible));
            OnPropertyChanged(nameof(CanMount));
            OnPropertyChanged(nameof(CanUnmount));
            OnPropertyChanged(nameof(IsMounted));
            OnPropertyChanged(nameof(IsUnmounted));
        }

        private async Task ProviderStatisticsUpdated(IHttpCloud cloud, StatisticUpdateReason reason, AStatisticFileInfo info)
        {
            try
            {
                await App.Dispatcher.InvokeAsync(() =>
                {
                    model.OnProviderStatisticsUpdated(this, reason, info);
                });
            }
            catch (TaskCanceledException)
            {
                Log.Trace("Task cancelled");
            }
        }
    }
}