using System.Threading;
using System.Windows;
using System.Windows.Input;

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

        public MountWaitBox(Window owner)
            : this()
        {
            Owner = owner;
        }

        public CancellationTokenSource Cancellation { get; internal set; }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
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
