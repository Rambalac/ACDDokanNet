namespace Azi.Cloud.Common
{
    using System.ComponentModel;
    using System.Configuration;
    using System.Runtime.CompilerServices;
    using Annotations;

    [SettingsSerializeAs(SettingsSerializeAs.Xml)]
    public class CloudInfo : INotifyPropertyChanged
    {
        private string assemblyFileName;

        private bool autoMount;

        private string className;

        private char driveLetter;

        private string name;

        private bool readOnly;

        private string rootFolder;

        public event PropertyChangedEventHandler PropertyChanged;

        public string AssemblyFileName
        {
            get
            {
                return assemblyFileName;
            }

            set
            {
                assemblyFileName = value;
                OnPropertyChanged();
            }
        }

        public string AuthSave { get; set; }

        public bool AutoMount
        {
            get
            {
                return autoMount;
            }

            set
            {
                autoMount = value;
                OnPropertyChanged();
            }
        }

        public string ClassName
        {
            get
            {
                return className;
            }

            set
            {
                className = value;
                OnPropertyChanged();
            }
        }

        public char DriveLetter
        {
            get
            {
                return driveLetter;
            }

            set
            {
                driveLetter = value;
                OnPropertyChanged();
            }
        }

        public string Id { get; set; }

        public string Name
        {
            get
            {
                return name;
            }

            set
            {
                name = value;
                OnPropertyChanged();
            }
        }

        public bool ReadOnly
        {
            get
            {
                return readOnly;
            }

            set
            {
                readOnly = value;
                OnPropertyChanged();
            }
        }

        public string RootFolder
        {
            get => rootFolder;
            set
            {
                rootFolder = value;
                OnPropertyChanged();
            }
        }

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}