namespace Azi.Cloud.DokanNet.Gui
{
    using System;
    using System.Windows;
    using System.Windows.Controls;

    /// <summary>
    /// Interaction logic for AvailableCloudsList.xaml
    /// </summary>
    public partial class AvailableCloudsList
    {
        public AvailableCloudsList()
        {
            InitializeComponent();
        }

        public event EventHandler Clicked;

        private ViewModel Model => (ViewModel)DataContext;

        private void ListBox_Selected(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;

            Model.AddCloud((AvailableCloud)button.DataContext);

            Clicked?.Invoke(this, null);
        }
    }
}