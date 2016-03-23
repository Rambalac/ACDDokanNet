using Azi.Cloud.Common;
using Azi.Cloud.DokanNet;
using Azi.Tools;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Azi.Cloud.DokanNet.Gui
{
    [Serializable]
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
            cloudInfo.PropertyChanged += CloudInfoCanged;
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
                if (MountLetter == null || res.Contains((char)MountLetter))
                {
                    return res;
                }

                res.Add((char)MountLetter);
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
            await StartUnmount();
            var settings = Properties.Settings.Default;
            settings.Clouds.RemoveAll(c => c.Id == CloudInfo.Id);
            settings.Save();
            App.DeleteCloud(this);
        }

        public void OnAuthUpdated(IHttpCloud sender, string authinfo)
        {
            var settings = Properties.Settings.Default;
            CloudInfo.AuthSave = authinfo;
            settings.Save();
            Debug.WriteLine("Auth updated: " + CloudInfo.Name);
        }

        public async Task StartMount(bool interactiveAuth = true)
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
                    var mountedEvent = new TaskCompletionSource<char>();

                    var task = Task.Factory.StartNew(() => Mount(mountedEvent, interactiveAuth), TaskCreationOptions.LongRunning).Unwrap();
                    MountLetter = await mountedEvent.Task;
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

        public async Task StartUnmount()
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
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.
                disposedValue = true;
            }
        }

        private void CloudInfoCanged(object sender, PropertyChangedEventArgs e)
        {
            var settings = Properties.Settings.Default;
            settings.Save();

            OnPropertyChanged(nameof(CloudInfo));
        }

        private async Task Mount(TaskCompletionSource<char> mountedEvent, bool interactiveAuth = true)
        {
            try
            {
                Instance.OnAuthUpdated = this;
                var authenticated = await Authenticate(instance, MountCancellation.Token, interactiveAuth);

                if (!authenticated)
                {
                    Log.Error("Authentication failed");
                    mountedEvent.SetException(new InvalidOperationException("Authentication failed"));
                    return;
                }

                Provider = new FSProvider(instance);
                Provider.CachePath = Environment.ExpandEnvironmentVariables(Properties.Settings.Default.CacheFolder);
                Provider.SmallFilesCacheSize = Properties.Settings.Default.SmallFilesCacheLimit * (1 << 20);
                Provider.SmallFileSizeLimit = Properties.Settings.Default.SmallFileSizeLimit * (1 << 20);
                Provider.OnStatisticsUpdated = ProviderStatisticsUpdated;
                var cloudDrive = new VirtualDriveWrapper(Provider);
                cloudDrive.Mounted = (letter) =>
                {
                    mountedEvent.SetResult(letter);
                };
                cloudDrive.Unmounted = (letter) =>
                  {
                      unmountingEvent.Set();
                  };

                App.MountChanged(CloudInfo);

                try
                {
                    cloudDrive.Mount(CloudInfo.DriveLetter, CloudInfo.ReadOnly);
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
            }
            catch (Exception ex)
            {
                mountedEvent.SetException(ex);
            }
            finally
            {
                App.MountChanged(CloudInfo);
            }
        }

        private void ProviderStatisticsUpdated(int downloading, int uploading)
        {
            App.ProviderStatisticsUpdated(CloudInfo, downloading, uploading);
        }

        private async Task<bool> Authenticate(IHttpCloud cloud, CancellationToken cs, bool interactiveAuth)
        {
            var settings = Properties.Settings.Default;
            var authinfo = CloudInfo.AuthSave;
            if (string.IsNullOrWhiteSpace(authinfo))
            {
                Debug.WriteLine("No auth info: " + CloudInfo.Name);
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
        }
    }
}
