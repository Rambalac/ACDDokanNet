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
using System.Windows.Shapes;

namespace Azi.ACDDokanNet.Gui
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void mountButton_Click(object sender, RoutedEventArgs e)
        {
            unmountButton.IsEnabled = true;
            mountButton.IsEnabled = false;
            automountCheckBox.IsEnabled = true;
            App.Current.Mount((char)comboBox.SelectedItem);
        }

        private void unmountButton_Click(object sender, RoutedEventArgs e)
        {
            unmountButton.IsEnabled = false;
            mountButton.IsEnabled = true;
            automountCheckBox.IsEnabled = true;
            App.Current.Unmount();
        }

        private void Window_Closed(object sender, EventArgs e)
        {

        }
    }
}
