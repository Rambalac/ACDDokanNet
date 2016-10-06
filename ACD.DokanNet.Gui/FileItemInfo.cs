namespace Azi.Cloud.DokanNet.Gui
{
    using System.ComponentModel;
    using System.Windows;

    public class FileItemInfo : INotifyPropertyChanged
    {
        private int progress;

        public event PropertyChangedEventHandler PropertyChanged;

        public string Id { get; set; }

        public string CloudIcon { get; set; }

        public string FileName { get; set; }

        public int Progress
        {
            get
            {
                return progress;
            }

            set
            {
                progress = value;
                OnPropertyChanged(nameof(Progress));
            }
        }

        public string ErrorMessage { get; set; }

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
