namespace Azi.Cloud.DokanNet.Gui
{
    using System;
    using System.Diagnostics;
    using System.Linq;
    using System.Text.RegularExpressions;
    using Tools;

    public class DownloadUpdateCommand : ModelBasedCommand
    {
        private static readonly Regex Msifile = new Regex("ACDDokanNet.*\\.msi");

        public override void Execute(object parameter)
        {
            try
            {
                Process.Start(Model.UpdateAvailable.Assets.First(a => Msifile.IsMatch(a.Name)).BrowserUrl);
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
        }

        public override bool CanExecute(object parameter) => Model.UpdateAvailable != null;
    }
}
