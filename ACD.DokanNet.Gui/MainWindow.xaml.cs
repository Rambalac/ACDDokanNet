namespace Azi.Cloud.DokanNet.Gui
{
    using System;
    using System.Diagnostics;
    using System.Windows;
    using Microsoft.WindowsAPICodePack.Dialogs;
    using Tools;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private ViewModel Model => (ViewModel)DataContext;

        private void AvailableCloudsList_Clicked(object sender, EventArgs e)
        {
            cloudAdd_DropDownButton.IsOpen = false;
        }

        private void ChangeCacheDir(object sender, RoutedEventArgs e)
        {
            var path = Environment.ExpandEnvironmentVariables(Model.SmallFileCacheFolder);
            using (var dlg = new CommonOpenFileDialog
            {
                Title = "Select Cache Folder",
                IsFolderPicker = true,
                InitialDirectory = path,

                AddToMostRecentlyUsedList = false,
                AllowNonFileSystemItems = false,
                DefaultDirectory = path,
                EnsureFileExists = true,
                EnsurePathExists = true,
                EnsureReadOnly = false,
                EnsureValidNames = true,
                Multiselect = false,
                ShowPlacesList = true
            })
            {
                if (dlg.ShowDialog() == CommonFileDialogResult.Ok)
                {
                    Model.SmallFileCacheFolder = dlg.FileName;
                }
            }
        }

        private async void ClearSmallFilesCache(object sender, RoutedEventArgs e)
        {
            try
            {
                await App.Current.ClearCache();
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show(this, ex.Message);
            }
        }

        private void ExportLog_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(Properties.Resources.LogWarning);

            Log.Info("Version " + Model.Version);
            using (var dlg = new CommonSaveFileDialog
            {
                Title = "Export Log",
                DefaultExtension = ".evtx",
                DefaultFileName = "ACDDokanNetLog.evtx",
                AddToMostRecentlyUsedList = false,
                EnsureValidNames = true,
                ShowPlacesList = true,
                RestoreDirectory = true
            })
            {
                if (dlg.ShowDialog() == CommonFileDialogResult.Ok)
                {
                    try
                    {
                        Log.Export(dlg.FileName);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex);
                        MessageBox.Show(this, ex.Message);
                    }
                }
            }
        }

        private void OpenIssue_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("https://github.com/Rambalac/ACDDokanNet/issues/new");
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            Hide();
            Properties.Settings.Default.Save();
        }
    }
}