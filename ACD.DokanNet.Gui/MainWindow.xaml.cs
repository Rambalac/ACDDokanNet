using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.Collections.Generic;
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
            await Model.Mount();
        }

        private async void unmountButton_Click(object sender, RoutedEventArgs e)
        {
            await Model.Unmount();
        }

        private void ChangeCacheDir(object sender, RoutedEventArgs e)
        {
            var path = Environment.ExpandEnvironmentVariables(Model.CacheFolder);
            var dlg = new CommonOpenFileDialog();
            dlg.Title = "Select Cache Folder";
            dlg.IsFolderPicker = true;
            dlg.InitialDirectory = path;

            dlg.AddToMostRecentlyUsedList = false;
            dlg.AllowNonFileSystemItems = false;
            dlg.DefaultDirectory = path;
            dlg.EnsureFileExists = true;
            dlg.EnsurePathExists = true;
            dlg.EnsureReadOnly = false;
            dlg.EnsureValidNames = true;
            dlg.Multiselect = false;
            dlg.ShowPlacesList = true;

            if (dlg.ShowDialog() == CommonFileDialogResult.Ok)
            {
                Model.CacheFolder = dlg.FileName;
                // Do something with selected folder string
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
    }
}
