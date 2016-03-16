using Azi.Cloud.DokanNet;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Azi.Cloud.DokanNet.Gui
{
    public class CloudModel : INotifyPropertyChanged, IDisposable
    {
        private bool mounting = false;

        private bool unmounting = false;

        private bool disposedValue = false; // To detect redundant calls

        private bool mounted = false;

        public event PropertyChangedEventHandler PropertyChanged;

        public char? MountLetter { get; set; }

        public char SelectedDriveLetter { get; set; }

        public bool Automount { get; set; }

        public string DriveName { get; set; }

        public string ClassName { get; set; }

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

        public bool CanMount => (!mounting) && !mounted;

        public bool CanUnmount => (!unmounting) && mounted;

        public bool IsMounted => !mounting && !unmounting && mounted;

        public bool IsUnmounted => !unmounting && !mounting && !mounted;

        public bool ReadOnly { get; set; }

        private App App => App.Current;

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
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
