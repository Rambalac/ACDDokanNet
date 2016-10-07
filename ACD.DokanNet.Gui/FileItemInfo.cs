namespace Azi.Cloud.DokanNet.Gui
{
    using System.ComponentModel;
    using System.Windows;

    public class FileItemInfo : INotifyPropertyChanged
    {
        private long done;

        private string errorMessage;

        public event PropertyChangedEventHandler PropertyChanged;

        public long Total { get; set; }

        public string Id { get; set; }

        public string CloudIcon { get; set; }

        public string CloudName { get; set; }

        public string FileName { get; set; }

        public int Progress => (int)(Done * 100 / Total);

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

        public Visibility HasError => (ErrorMessage == null) ? Visibility.Collapsed : Visibility.Visible;

        public override bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }

            return Id == ((FileItemInfo)obj).Id;
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        private void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
