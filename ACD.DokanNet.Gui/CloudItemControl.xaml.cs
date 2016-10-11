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
        public CloudItemControl()
        {
            InitializeComponent();
        }

        private CloudMount Model => (CloudMount)DataContext;

        private async void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            await Model.Delete();
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

        private async void UnmountButton_Click(object sender, RoutedEventArgs e)
        {
            await Model.UnmountAsync();
        }
    }
}