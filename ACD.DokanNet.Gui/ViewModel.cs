using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Azi.ACDDokanNet.Gui
{
    public class ViewModel : INotifyPropertyChanged, IDisposable
    {
        private Timer refreshTimer;

        private bool mounting = false;

        private bool unmounting = false;

        private bool disposedValue = false; // To detect redundant calls

        public ViewModel()
        {
            if (App != null)
            {
                App.OnProviderStatisticsUpdated = ProviderStatisticsUpdated;
                App.OnMountChanged = NotifyMount;
                refreshTimer = new Timer(RefreshLetters, null, 1000, 1000);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public IList<char> DriveLetters => VirtualDriveWrapper.GetFreeDriveLettes();

        public bool IsAutomount
        {
            get
            {
                return App.GetAutorun();
            }

            set
            {
                App.SetAutorun(value);
            }
        }

        public char SelectedDriveLetter
        {
            get
            {
                var saved = Properties.Settings.Default.LastDriveLetter;
                var free = VirtualDriveWrapper.GetFreeDriveLettes();
                if (!free.Any() || free.Contains(saved))
                {
                    return saved;
                }

                return free[0];
            }

            set
            {
                Properties.Settings.Default.LastDriveLetter = value;
                Properties.Settings.Default.Save();
                NotifyMount();
            }
        }

        public string CacheFolder
        {
            get
            {
                return Properties.Settings.Default.CacheFolder;
            }

            set
            {
                App.SmallFileCacheFolder = value;
                OnPropertyChanged(nameof(CacheFolder));
            }
        }

        public bool CanMount => (!mounting) && !(App?.IsMounted ?? false) && DriveLetters.Contains(SelectedDriveLetter);

        public bool CanUnmount => (!unmounting) && (App?.IsMounted ?? false);

        public bool IsMounted => !mounting && !unmounting && (App?.IsMounted ?? false);

        public bool IsUnmounted => !unmounting && !mounting && !(App?.IsMounted ?? false);

        public int UploadingFilesCount { get; private set; }

        public int DownloadingFilesCount { get; private set; }

        public long SmallFileSizeLimit
        {
            get
            {
                return App.SmallFileSizeLimit;
            }

            set
            {
                App.SmallFileSizeLimit = value;
            }
        }

        public long SmallFilesCacheSize
        {
            get
            {
                return App.SmallFilesCacheSize;
            }

            set
            {
                App.SmallFilesCacheSize = value;
            }
        }

        public bool ReadOnly
        {
            get { return Properties.Settings.Default.ReadOnly; }
            set { Properties.Settings.Default.ReadOnly = value; }
        }

        public string Version => Assembly.GetEntryAssembly().GetName().Version.ToString();

        private App App => App.Current;

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);

            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }

        internal void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        internal async Task Mount(CancellationToken cs)
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
                    var letter = await App.Mount(SelectedDriveLetter, ReadOnly, cs);
                    if (letter != null)
                    {
                        SelectedDriveLetter = (char)letter;
                    }
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

        internal async Task Unmount()
        {
            if (App == null)
            {
                throw new NullReferenceException();
            }

            unmounting = true;
            NotifyMount();
            try
            {
                await App.Unmount();
            }
            finally
            {
                unmounting = false;
                NotifyMount();
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    refreshTimer.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.
                disposedValue = true;
            }
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

        private void ProviderStatisticsUpdated(int downloading, int uploading)
        {
            UploadingFilesCount = uploading;
            DownloadingFilesCount = downloading;
            OnPropertyChanged(nameof(UploadingFilesCount));
            OnPropertyChanged(nameof(DownloadingFilesCount));
        }
    }
}
