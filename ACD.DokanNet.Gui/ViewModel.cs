using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Azi.Cloud.DokanNet.Gui
{
    public class ViewModel : INotifyPropertyChanged, IDisposable
    {
        private Timer refreshTimer;

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

        public IList<CloudModel> Clouds { get; set; }

        public bool IsAutorun
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

        public string Version => Assembly.GetEntryAssembly().GetName().Version.ToString();

        private App App => App.Current;

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
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
                    refreshTimer.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.
                disposedValue = true;
            }
        }

        private void NotifyMount(string obj)
        {
            RefreshLetters(null);
        }

        private void RefreshLetters(object state)
        {
            foreach (var cloud in Clouds)
            {
                cloud.OnPropertyChanged(nameof(cloud.DriveLetters));
            }
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
