using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
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
    /// Interaction logic for MountWaitBox.xaml
    /// </summary>
    public partial class MountWaitBox : Window
    {
        public MountWaitBox()
        {
            InitializeComponent();
        }

        public MountWaitBox(Window owner) : this()
        {
            Owner = owner;
        }

        public CancellationTokenSource Cancellation { get; internal set; }

        private void button_Click(object sender, RoutedEventArgs e)
        {
            Cancellation.Cancel();
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }
    }
}
