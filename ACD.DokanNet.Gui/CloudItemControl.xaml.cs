using Azi.Tools;
using System;
using System.Collections.Generic;
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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Azi.Cloud.DokanNet.Gui
{
    /// <summary>
    /// Interaction logic for CloudItemControl.xaml
    /// </summary>
    public partial class CloudItemControl : UserControl
    {
        private CloudModel Model => (CloudModel)DataContext;

        public CloudItemControl()
        {
            InitializeComponent();
        }

        private async void UnmountButton_Click(object sender, RoutedEventArgs e)
        {
            await Model.Unmount();
        }

        private async void MountButton_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new MountWaitBox(Window.GetWindow(this));
            var cs = new System.Threading.CancellationTokenSource();
            dlg.Cancellation = cs;
            dlg.Show();
            try
            {
                await Model.Mount(cs.Token);
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                MessageBox.Show(Window.GetWindow(this), ex.Message);
            }

            dlg.Close();
            Window.GetWindow(this).Activate();
        }

        private async void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            await Model.Unmount();
            App.Current.ClearCredentials();
        }
    }
}
