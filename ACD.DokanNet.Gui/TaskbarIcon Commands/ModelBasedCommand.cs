namespace Azi.Cloud.DokanNet.Gui
{
    using System;
    using System.ComponentModel;
    using System.Windows;
    using System.Windows.Input;

    public abstract class ModelBasedCommand : ICommand
    {
        public static readonly DependencyProperty ModelProperty = DependencyProperty.RegisterAttached(
            nameof(Model),
            typeof(ViewModel),
            typeof(DownloadUpdateCommand),
            new FrameworkPropertyMetadata(null));

        private ViewModel model;

        public event EventHandler CanExecuteChanged;

        public ViewModel Model
        {
            get
            {
                return model;
            }

            set
            {
                model = value;
                if (model != null)
                {
                    model.PropertyChanged += Model_PropertyChanged;
                }
            }
        }

        public static string GetMyProperty(UIElement element)
        {
            if (element == null)
            {
                throw new ArgumentNullException(nameof(element));
            }

            return (string)element.GetValue(ModelProperty);
        }

        public static void SetMyProperty(UIElement element, string value)
        {
            if (element == null)
            {
                throw new ArgumentNullException(nameof(element));
            }

            element.SetValue(ModelProperty, value);
        }

        public virtual bool CanExecute(object parameter)
        {
            return true;
        }

        public abstract void Execute(object parameter);

        public virtual void OnModelChanged(string property)
        {
        }

        private void Model_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            OnModelChanged(e.PropertyName);
            try
            {
                CanExecuteChanged?.Invoke(this, null);
            }
            catch (Exception)
            {
                // Ignore
            }
        }
    }
}