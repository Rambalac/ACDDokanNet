namespace Azi.Cloud.DokanNet.Gui
{
    using System.Windows;
    using System.Windows.Controls;

    /// <summary>
    /// Interaction logic for FileItem.xaml
    /// </summary>
    public partial class FileItem : UserControl
    {
        public FileItem()
        {
            InitializeComponent();
        }

        private void DismissButton_Click(object sender, RoutedEventArgs e)
        {
            var item = (FileItemInfo)DataContext;
            App.Current.UploadFiles.Remove(item);
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            var item = (FileItemInfo)DataContext;
            App.Current.CancelUpload(item);
        }
    }
}