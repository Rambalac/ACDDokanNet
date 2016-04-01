namespace Azi.Cloud.DokanNet.Gui
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows;
    using Azi.Cloud.DokanNet;
    using Azi.Tools;
    using Common;

    public class CloudMount : INotifyPropertyChanged, IDisposable, IAuthUpdateListener
    {
        private readonly CloudInfo cloudInfo;

        private ManualResetEventSlim unmountingEvent;

        private IHttpCloud instance;

        private bool mounting = false;

        private bool unmounting = false;

        private bool disposedValue = false; // To detect redundant calls

        public CloudMount(CloudInfo info)
        {
            cloudInfo = info;
            cloudInfo.PropertyChanged += CloudInfoChanged;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public CloudInfo CloudInfo
        {
            get
            {
                return cloudInfo;
            }
        }

        public IHttpCloud Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = Activator.CreateInstance(CloudInfo.AssemblyName, CloudInfo.ClassName).Unwrap() as IHttpCloud;
                    instance.Id = CloudInfo.Id;
                }

                return instance;
            }
        }

        public char? MountLetter { get; set; }

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

        public bool CanMount => (!mounting) && !(MountLetter != null);

        public bool CanUnmount => (!unmounting) && (MountLetter != null);

        public bool IsMounted => !mounting && !unmounting && (MountLetter != null);

        public bool IsUnmounted => !unmounting && !unmounting && !(MountLetter != null);

        public Visibility MountVisible => (!unmounting && (MountLetter == null)) ? Visibility.Visible : Visibility.Collapsed;

        public Visibility UnmountVisible => (!mounting && (MountLetter != null)) ? Visibility.Visible : Visibility.Collapsed;

        public CancellationTokenSource MountCancellation { get; } = new CancellationTokenSource();

        public FSProvider Provider { get; private set; }

        public int UploadingCount => Provider?.UploadingCount ?? 0;

        public int DownloadingCount => Provider.DownloadingCount;

        private App App => App.Current;

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
        }

        public async Task Delete()
        {
            await UnmountAsync();
            await Instance.SignOut(CloudInfo.AuthSave);
            App.DeleteCloud(this);
        }

        public void OnAuthUpdated(IHttpCloud sender, string authinfo)
        {
            CloudInfo.AuthSave = authinfo;
            App.SaveClouds();
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
                    using (unmountingEvent = new ManualResetEventSlim(false))
                    {
                        VirtualDriveWrapper.Unmount((char)MountLetter);
                        unmountingEvent.Wait();
                    }
                });

                MountLetter = null;
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

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.
                disposedValue = true;
            }
        }

        private void CloudInfoChanged(object sender, PropertyChangedEventArgs e)
        {
            OnPropertyChanged(nameof(CloudInfo));
            if (Provider != null)
            {
                Provider.VolumeName = CloudInfo.Name;
            }

            App.SaveClouds();
        }

        private async Task<char> Mount(bool interactiveAuth = true)
        {
            try
            {
                Instance.OnAuthUpdated = this;
                var authenticated = await Authenticate(instance, MountCancellation.Token, interactiveAuth);

                if (!authenticated)
                {
                    Log.Error("Authentication failed");
                    throw new InvalidOperationException("Authentication failed");
                }

                Provider = new FSProvider(instance);
                Provider.VolumeName = CloudInfo.Name;
                Provider.CachePath = Environment.ExpandEnvironmentVariables(Properties.Settings.Default.CacheFolder);
                Provider.SmallFilesCacheSize = Properties.Settings.Default.SmallFilesCacheLimit * (1 << 20);
                Provider.SmallFileSizeLimit = Properties.Settings.Default.SmallFileSizeLimit * (1 << 20);
                Provider.OnStatisticsUpdated = ProviderStatisticsUpdated;
                var cloudDrive = new VirtualDriveWrapper(Provider);

                var mountedEvent = new TaskCompletionSource<char>();

                cloudDrive.Mounted = (letter) =>
                {
                    mountedEvent.SetResult(letter);
                };

                NotifyMount();

                var task = Task.Run(() =>
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
                        bool wasMounted = false;
                        foreach (char letter in VirtualDriveWrapper.GetFreeDriveLettes())
                        {
                            try
                            {
                                cloudDrive.Mount(letter, CloudInfo.ReadOnly);
                                unmountingEvent.Set();

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
                            if (lastException != null && lastException.InnerException != null)
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
                });
                return await mountedEvent.Task;
            }
            finally
            {
                NotifyMount();
            }
        }

        private void ProviderStatisticsUpdated(int downloading, int uploading)
        {
            App.ProviderStatisticsUpdated(CloudInfo, downloading, uploading);
        }

        private async Task<bool> Authenticate(IHttpCloud cloud, CancellationToken cs, bool interactiveAuth)
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

        private void RefreshLetters(object state)
        {
            if (!CanMount)
            {
                return;
            }

            OnPropertyChanged(nameof(DriveLetters));
        }

        private void NotifyMount()
        {
            OnPropertyChanged(nameof(MountVisible));
            OnPropertyChanged(nameof(UnmountVisible));
            OnPropertyChanged(nameof(CanMount));
            OnPropertyChanged(nameof(CanUnmount));
            OnPropertyChanged(nameof(IsMounted));
            OnPropertyChanged(nameof(IsUnmounted));

            App.NotifyMountChanged(CloudInfo.Id);
        }
    }
}
