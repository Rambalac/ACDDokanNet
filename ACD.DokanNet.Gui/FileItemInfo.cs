namespace Azi.Cloud.DokanNet.Gui
{
    using System.ComponentModel;
    using System.Windows;

    public class FileItemInfo : INotifyPropertyChanged
    {
        private bool dismissOnly;
        private long done;

        private string errorMessage;

        public FileItemInfo(string cloudid, string id)
        {
            CloudId = cloudid;
            Id = id;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public Visibility CancelButton => dismissOnly ? Visibility.Collapsed : Visibility.Visible;

        public string CloudIcon { get; set; }

        public string CloudId { get; internal set; }

        public string CloudName { get; set; }

        public Visibility DismissButton => (!dismissOnly) ? Visibility.Collapsed : Visibility.Visible;

        public bool DismissOnly
        {
            get
            {
                return dismissOnly;
            }

            internal set
            {
                dismissOnly = value;
                OnPropertyChanged(nameof(DismissButton));
                OnPropertyChanged(nameof(CancelButton));
            }
        }

        public long Done
        {
            get
            {
                return done;
            }

            set
            {
                done = value;
                OnPropertyChanged(nameof(Done));
                OnPropertyChanged(nameof(Progress));
                OnPropertyChanged(nameof(ProgressTip));
            }
        }

        public string ErrorMessage
        {
            get
            {
                return errorMessage;
            }

            set
            {
                errorMessage = value;
                OnPropertyChanged(nameof(ErrorMessage));
                OnPropertyChanged(nameof(HasError));
            }
        }

        public string FileName { get; set; }

        public string FullPath { get; internal set; }

        public Visibility HasError => (ErrorMessage == null) ? Visibility.Collapsed : Visibility.Visible;

        public string Id { get; set; }

        public int Progress => Total != 0 ? (int)(Done * 100 / Total) : 0;

        public string ProgressTip
        {
            get
            {
                if (Total < 1024)
                {
                    return $"{Done} of {Total} bytes";
                }

                if (Total < 1024 * 1024)
                {
                    return $"{Done / 1024} of {Total / 1024} KB";
                }

                return $"{Done / 1024 / 1024} of {Total / 1024 / 1024} MB";
            }
        }

        public long Total { get; set; }

        public override bool Equals(object obj)
        {
            var o = obj as FileItemInfo;
            if (o == null)
            {
                return false;
            }

            return Id == o.Id && CloudId == o.CloudId;
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode() ^ CloudId.GetHashCode();
        }

        private void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}