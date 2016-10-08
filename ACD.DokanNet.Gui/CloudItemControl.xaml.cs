namespace Azi.Cloud.DokanNet.Gui
{
    using System;
    using System.Windows;
    using System.Windows.Controls;
    using Tools;

    /// <summary>
    /// Interaction logic for CloudItemControl.xaml
    /// </summary>
    public partial class CloudItemControl : UserControl
    {
        private CloudMount Model => (CloudMount)DataContext;

        public CloudItemControl()
        {
            InitializeComponent();
        }

        private async void UnmountButton_Click(object sender, RoutedEventArgs e)
        {
            await Model.UnmountAsync();
        }

        private async void MountButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await Model.MountAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                MessageBox.Show(Window.GetWindow(this), ex.Message);
            }

            Window.GetWindow(this).Activate();
        }

        private async void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            await Model.Delete();
        }
    }
}
