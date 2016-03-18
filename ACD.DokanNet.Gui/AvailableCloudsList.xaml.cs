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
    /// Interaction logic for AvailableCloudsList.xaml
    /// </summary>
    public partial class AvailableCloudsList : UserControl
    {
        public event EventHandler Clicked;

        private App App => App.Current;

        public AvailableCloudsList()
        {
            InitializeComponent();
        }

        private void ListBox_Selected(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;

            App.AddCloud((AvailableCloudsModel.AvailableCloud)button.DataContext);

            Clicked?.Invoke(this, null);
        }
    }
}
