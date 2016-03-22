using Azi.Cloud.Common;
using Azi.Cloud.DokanNet;
using Azi.Tools;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Azi.Cloud.DokanNet.Gui
{
    [Serializable]
    public class CloudMount : INotifyPropertyChanged, IDisposable, IAuthUpdateListener
    {
        private IHttpCloud instance;

        private bool mounting = false;

        private bool unmounting = false;

        private bool disposedValue = false; // To detect redundant calls

        public CloudMount(CloudInfo info)
        {
            cloudInfo = info;
            cloudInfo.PropertyChanged += CloudInfoCanged;
        }

        private void CloudInfoCanged(object sender, PropertyChangedEventArgs e)
        {
            var settings = Properties.Settings.Default;
            settings.Save();

            OnPropertyChanged(nameof(CloudInfo));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private readonly CloudInfo cloudInfo;

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

        public bool IsUnmounted => !unmounting && !mounting && !(MountLetter != null);

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

        public void Delete()
        {
            Unmount();
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

        public void Unmount()
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
                VirtualDriveWrapper.Unmount((char)MountLetter);
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
            OnPropertyChanged(nameof(CanMount));
            OnPropertyChanged(nameof(CanUnmount));
            OnPropertyChanged(nameof(IsMounted));
            OnPropertyChanged(nameof(IsUnmounted));
        }
    }
}
