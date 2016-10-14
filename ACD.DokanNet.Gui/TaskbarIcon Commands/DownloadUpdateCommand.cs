namespace Azi.Cloud.DokanNet.Gui
{
    using System;
    using System.Diagnostics;
    using System.Linq;
    using System.Windows.Input;
    using Tools;

    public class DownloadUpdateCommand : ICommand
    {
        public event EventHandler CanExecuteChanged;

        public bool CanExecute(object parameter) => true;

        public void Execute(object parameter)
        {
            try
            {
                Process.Start(App.Current.UpdateAvailable.Assets.First(a => a.Name == "ACDDokanNetInstaller.msi").BrowserUrl);
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
        }
    }
}
