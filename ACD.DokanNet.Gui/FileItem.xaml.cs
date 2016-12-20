namespace Azi.Cloud.DokanNet.Gui
{
    using System.Windows;

    /// <summary>
    /// Interaction logic for FileItem.xaml
    /// </summary>
    public partial class FileItem
    {
        public FileItem()
        {
            InitializeComponent();
        }

        private FileItemInfo Item => (FileItemInfo)DataContext;

        private void DismissButton_Click(object sender, RoutedEventArgs e)
        {
            App.Current.Model.UploadFiles.Remove(Item);
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            App.Current.CancelUpload(Item);
        }
    }
}