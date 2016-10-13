namespace Azi.Cloud.DokanNet.Gui
{
    using System;
    using System.Windows.Input;
    using Hardcodet.Wpf.TaskbarNotification;

    public class TaskbarIconClickCommand : ICommand
    {
        public event EventHandler CanExecuteChanged;

        public bool CanExecute(object parameter) => true;

        public void Execute(object parameter)
        {
            var message = $"Downloading: {App.Current.DownloadingCount}\r\nUploading: {App.Current.UploadingCount}";
            if (App.UpdateAvailable != null)
            {
                message += $"\r\n\r\nUpdate to {App.UpdateAvailable.Version} is available.\r\nClick here to download.";
            }
            else
            {
                message += "\r\nClick to open settings.";
            }

            App.Current.NotifyIcon.ShowBalloonTip("State", message, BalloonIcon.None);
        }
    }
}
