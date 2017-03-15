namespace Azi.Cloud.DokanNet.Gui
{
    using System;
    using System.Windows;
    using Tools;

    /// <summary>
    /// Interaction logic for CloudItemControl.xaml
    /// </summary>
    public partial class CloudItemControl
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
            var win = Window.GetWindow(this);
            if (win == null)
            {
                Log.ErrorTrace("Window is null");
            }

            try
            {
                await Model.MountAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                if (win != null)
                {
                    MessageBox.Show(win, ex.Message);
                }
                else
                {
                    MessageBox.Show(ex.Message);
                }
            }

            win?.Activate();
        }

        private async void UnmountButton_Click(object sender, RoutedEventArgs e)
        {
            await Model.UnmountAsync();
        }
    }
}