using System;
using System.Diagnostics;
using System.Windows;
using Azi.Tools;
using Microsoft.WindowsAPICodePack.Dialogs;

namespace Azi.ACDDokanNet.Gui
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private ViewModel Model => (ViewModel)DataContext;

        public MainWindow()
        {
            InitializeComponent();
        }

        private async void MountButton_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new MountWaitBox(this);
            var cs = new System.Threading.CancellationTokenSource();
            dlg.Cancellation = cs;
            dlg.Show();
            try
            {
                await Model.Mount(cs.Token);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message);
            }

            dlg.Close();
            Activate();
        }

        private async void UnmountButton_Click(object sender, RoutedEventArgs e)
        {
            await Model.Unmount();
        }

        private void ChangeCacheDir(object sender, RoutedEventArgs e)
        {
            var path = Environment.ExpandEnvironmentVariables(Model.CacheFolder);
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
                    Model.CacheFolder = dlg.FileName;
                }
            }
        }

        private async void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            await Model.Unmount();
            App.Current.ClearCredentials();
        }

        private void ClearSmallFilesCache(object sender, RoutedEventArgs e)
        {
            try
            {
                App.Current.ClearCache();
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show(this, ex.Message);
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            Hide();
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
            Process.Start("https://github.com/Rambalac/AmazonCloudDriveApi/issues/new");
        }
    }
}
