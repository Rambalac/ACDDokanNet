﻿namespace Azi.Cloud.DokanNet.Gui
{
    using System;
    using System.Windows.Input;

    public class OpenAppSettingsCommand : ICommand
    {
        public event EventHandler CanExecuteChanged;

        public bool CanExecute(object parameter) => true;

        public void Execute(object parameter)
        {
            App.Current.OpenSettings();
        }
    }
}
