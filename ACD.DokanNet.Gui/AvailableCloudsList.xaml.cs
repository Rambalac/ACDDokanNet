namespace Azi.Cloud.DokanNet.Gui
{
    using System;
    using System.Windows;
    using System.Windows.Controls;

    /// <summary>
    /// Interaction logic for AvailableCloudsList.xaml
    /// </summary>
    public partial class AvailableCloudsList : UserControl
    {
        public AvailableCloudsList()
        {
            InitializeComponent();
        }

        public event EventHandler Clicked;

        private App App => App.Current;

        private void ListBox_Selected(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;

            App.AddCloud((AvailableCloudsModel.AvailableCloud)button.DataContext);

            Clicked?.Invoke(this, null);
        }
    }
}
