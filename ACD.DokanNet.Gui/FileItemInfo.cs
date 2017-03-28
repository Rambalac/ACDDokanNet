namespace Azi.Cloud.DokanNet.Gui
{
    using System;
    using System.ComponentModel;
    using System.Runtime.CompilerServices;
    using System.Windows;
    using Common.Annotations;

    public class FileItemInfo : INotifyPropertyChanged
    {
        private bool dismissOnly;
        private long done;
        private string errorMessage;
        private bool isChecked;
        private UploadState state;
        private DateTime uploadStartTime;

        public FileItemInfo(string cloudid, string id)
        {
            CloudId = cloudid;
            Id = id;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public string CloudIcon { get; set; }

        public string CloudId { get; }

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
                OnPropertyChanged(nameof(IsCheckVisible));
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
                if (done == value)
                {
                    return;
                }

                done = value;
                if (State != UploadState.Waiting && done != Total)
                {
                    State = UploadState.Uploading;
                }

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
                if (errorMessage != null)
                {
                    State = UploadState.Failed;
                }

                OnPropertyChanged(nameof(ErrorMessage));
                OnPropertyChanged(nameof(HasError));
            }
        }

        public string FileName { get; set; }

        public string FullPath { get; internal set; }

        public Visibility HasError => (ErrorMessage == null) ? Visibility.Collapsed : Visibility.Visible;

        public string Id { get; }

        public bool IsChecked
        {
            get
            {
                return isChecked;
            }

            set
            {
                if (value == isChecked)
                {
                    return;
                }

                isChecked = value;
                OnPropertyChanged();
            }
        }

        public Visibility IsCheckVisible => dismissOnly ? Visibility.Collapsed : Visibility.Visible;

        public int Progress => Total != 0 ? (int)(Done * 100 / Total) : 0;

        public string ProgressTip
        {
            get
            {
                if (State == UploadState.Uploading)
                {
                    string donetip;
                    if (Total < 1024)
                    {
                        donetip = $"{Done} of {Total} bytes";
                    }
                    else if (Total < 1024 * 1024)
                    {
                        donetip = $"{Done / 1024} of {Total / 1024} KB";
                    }
                    else
                    {
                        donetip = $"{Done / 1024 / 1024} of {Total / 1024 / 1024} MB";
                    }

                    double past = (DateTime.UtcNow - uploadStartTime).TotalSeconds;
                    if (past > 15 && Done != 0)
                    {
                        var total = past * Total / Done;
                        var left = TimeSpan.FromSeconds(total - past);
                        donetip += ". Est. " + left.ToString("h'h 'mm'm 'ss's'").TrimStart(' ', '0', 'h', 'm') + " left.";
                    }

                    return donetip;
                }

                switch (State)
                {
                    case UploadState.Waiting:
                        return "Waiting in queue";
                    case UploadState.ContentId:
                        return "Calculating content hash";
                    case UploadState.Finishing:
                        return "Finalazing upload";
                    case UploadState.Failed:
                        return "Upload failed";
                    default:
                        throw new ArgumentException();
                }
            }
        }

        public UploadState State
        {
            get
            {
                return state;
            }

            set
            {
                if (state != UploadState.Uploading && value == UploadState.Uploading)
                {
                    uploadStartTime = DateTime.UtcNow;
                }

                state = value;

                OnPropertyChanged(nameof(State));
                OnPropertyChanged(nameof(ProgressTip));
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

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}