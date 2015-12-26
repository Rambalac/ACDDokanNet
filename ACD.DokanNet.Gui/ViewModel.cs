using Azi.ACDDokanNet;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Azi.ACDDokanNet.Gui
{
    public class ViewModel : INotifyPropertyChanged
    {
        readonly private App App = App.Current;

        public event PropertyChangedEventHandler PropertyChanged;

        public IList<char> DriveLetters => VirtualDriveWrapper.GetFreeDriveLettes();

        public ViewModel()
        {
            App.OnProviderStatisticsUpdated = ProviderStatisticsUpdated;
        }

        public char SelectedDriveLetter
        {
            get
            {
                var saved = Properties.Settings.Default.LastDriveLetter;
                var free = VirtualDriveWrapper.GetFreeDriveLettes();
                if (!free.Any() || free.Contains(saved)) return saved;
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
            get { return Properties.Settings.Default.CacheFolder; }
            set
            {
                Properties.Settings.Default.CacheFolder = value;
                Properties.Settings.Default.Save();
            }
        }

        void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        bool mounting = false;
        internal async Task Mount()
        {
            if (App == null) throw new NullReferenceException();
            mounting = true;
            NotifyMount();
            try
            {
                var letter = await App.Mount(SelectedDriveLetter);
                if (letter != null) SelectedDriveLetter = (char)letter;
            }
            finally
            {
                mounting = false;
                NotifyMount();
            }
        }

        bool unmounting = false;
        internal async Task Unmount()
        {
            if (App == null) throw new NullReferenceException();
            unmounting = true;
            NotifyMount(); try
            {
                await App.Unmount();
            }
            finally
            {
                unmounting = false;
                NotifyMount();
            }
        }

        private void NotifyMount()
        {
            OnPropertyChanged(nameof(CanMount));
            OnPropertyChanged(nameof(CanUnmount));
            OnPropertyChanged(nameof(IsMounted));
            OnPropertyChanged(nameof(IsUnmounted));
        }

        public bool CanMount => (!mounting) && !(App?.IsMounted ?? false) && DriveLetters.Contains(SelectedDriveLetter);
        public bool CanUnmount => (!unmounting) && (App?.IsMounted ?? false);

        public bool IsMounted => !mounting && !unmounting && (App?.IsMounted ?? false);
        public bool IsUnmounted => !unmounting && !mounting && !(App?.IsMounted ?? false);

        private void ProviderStatisticsUpdated(int downloading, int uploading)
        {
            UploadingFilesCount = uploading;
            DownloadingFilesCount = downloading;
            OnPropertyChanged(nameof(UploadingFilesCount));
            OnPropertyChanged(nameof(DownloadingFilesCount));
        }
        public int UploadingFilesCount { get; private set; }
        public int DownloadingFilesCount { get; private set; }
    }
}
