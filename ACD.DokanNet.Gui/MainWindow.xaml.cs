namespace Azi.Cloud.DokanNet.Gui
{
    using System;
    using System.Diagnostics;
    using System.Linq;
    using System.Windows;
    using Microsoft.WindowsAPICodePack.Dialogs;
    using Tools;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private ViewModel Model => (ViewModel)DataContext;

        private void AvailableCloudsList_Clicked(object sender, EventArgs e)
        {
            CloudAddDropDownButton.IsOpen = false;
        }

        private void ChangeCacheDir(object sender, RoutedEventArgs e)
        {
            try
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
            catch (Exception ex)
            {
                Log.Error(ex);
                MessageBox.Show(this, ex.Message);
            }
        }

        private async void ClearSmallFilesCache(object sender, RoutedEventArgs e)
        {
            try
            {
                await App.MyApp.ClearCache();
            }
            catch (InvalidOperationException ex)
            {
                Log.Error(ex);
                MessageBox.Show(this, ex.Message);
            }
        }

        private void ExportLog_Click(object sender, RoutedEventArgs e)
        {
            try
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
            catch (Exception ex)
            {
                Log.Error(ex);
                MessageBox.Show(this, ex.Message);
            }
        }

        private void OpenIssue_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start("https://github.com/Rambalac/ACDDokanNet/issues/new");
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                MessageBox.Show(this, ex.Message);
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Properties.Settings.Default.Save();
            if (!App.IsShuttingDown)
            {
                e.Cancel = true;
                Hide();
            }
        }

        private void TextBox_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            Process.Start(Model.SmallFileCacheFolder);
        }

        private void CancelAll_OnClick(object sender, RoutedEventArgs e)
        {
            foreach (var item in Model.UploadFiles.Where(f => f.IsChecked && !f.DismissOnly))
            {
                var cloud = Model.Clouds.SingleOrDefault(c => c.CloudInfo.Id == item.CloudId);
                cloud?.Provider.CancelUpload(item.Id);
            }
        }

        private void DismissAll_OnClick(object sender, RoutedEventArgs e)
        {
            FileItemInfo item;
            while ((item = Model.UploadFiles.FirstOrDefault(f => f.DismissOnly)) != null)
            {
                Model.UploadFiles.Remove(item);
            }
        }

        private void SelectAll_OnClick(object sender, RoutedEventArgs e)
        {
            var process = Model.UploadFiles.Where(f => !f.IsChecked).ToList();
            if (process.Count != 0)
            {
                foreach (var upload in process)
                {
                    upload.IsChecked = true;
                }
            }
            else
            {
                foreach (var upload in Model.UploadFiles)
                {
                    upload.IsChecked = false;
                }
            }
        }
    }
}