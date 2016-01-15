using Azi.Tools;
using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Azi.ACDDokanNet.Gui
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        ViewModel Model => (ViewModel)DataContext;
        public MainWindow()
        {
            InitializeComponent();
        }

        private async void mountButton_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new MountWaitBox(this);
            var cs = new System.Threading.CancellationTokenSource();
            dlg.Cancellation = cs;
            dlg.Show();
            await Model.Mount(cs.Token);
            dlg.Close();
            Activate();
        }

        private async void unmountButton_Click(object sender, RoutedEventArgs e)
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

        private async void logoutButton_Click(object sender, RoutedEventArgs e)
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

        private void exportLog_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(Properties.Resources.LogWarning);

            Log.Info("Version " + Model.Version);
            using (var dlg = new CommonOpenFileDialog
            {
                Title = "Export Log",
                DefaultExtension=".evtx",
                DefaultFileName="ACDDokanNetLog.evtx",
                AddToMostRecentlyUsedList = false,
                AllowNonFileSystemItems = false,
                EnsureValidNames = true,
                Multiselect = false,
                ShowPlacesList = true
            })
            {
                if (dlg.ShowDialog() == CommonFileDialogResult.Ok)
                {
                    Log.Export(dlg.FileName);
                }
            }
        }

        private void openIssue_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("https://github.com/Rambalac/AmazonCloudDriveApi/issues/new");
        }
    }
}
