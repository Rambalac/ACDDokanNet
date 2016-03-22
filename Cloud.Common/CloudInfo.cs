using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Azi.Cloud.Common
{
    [SettingsSerializeAs(SettingsSerializeAs.Xml)]
    public class CloudInfo : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private string className;

        public string ClassName
        {
            get
            {
                return className;
            }

            set
            {
                className = value;
                OnPropertyChanged(nameof(ClassName));
            }
        }

        private string assemblyName;

        public string AssemblyName
        {
            get
            {
                return assemblyName;
            }

            set
            {
                assemblyName = value;
                OnPropertyChanged(nameof(AssemblyName));
            }
        }

        private char driveLetter;

        public char DriveLetter
        {
            get
            {
                return driveLetter;
            }

            set
            {
                driveLetter = value;
                OnPropertyChanged(nameof(DriveLetter));
            }
        }

        private bool autoMount;

        public bool AutoMount
        {
            get
            {
                return autoMount;
            }

            set
            {
                autoMount = value;
                OnPropertyChanged(nameof(AutoMount));
            }
        }

        public string AuthSave { get; set; }

        private string name;

        public string Name
        {
            get
            {
                return name;
            }

            set
            {
                name = value;
                OnPropertyChanged(nameof(Name));
            }
        }

        public string Id { get; set; }

        private bool readOnly;

        public bool ReadOnly
        {
            get
            {
                return readOnly;
            }

            set
            {
                readOnly = value;
                OnPropertyChanged(nameof(ReadOnly));
            }
        }

        internal void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
