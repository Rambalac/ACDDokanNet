namespace Azi.Cloud.DokanNet
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using System.Linq;
    using global::DokanNet;
    using global::DokanNet.Logging;
    using Tools;

    public class DokanLogger : ILogger
    {
        public void Debug(string message, params object[] args)
        {
            Log.Trace(Format(message, args), Log.Dokan);
        }

        public void Error(string message, params object[] args)
        {
            Log.Error(Format(message, args), Log.Dokan);
        }

        public void Fatal(string message, params object[] args)
        {
            Log.Error(Format(message, args), Log.Dokan);
        }

        public void Info(string message, params object[] args)
        {
            Log.Info(Format(message, args), Log.Dokan);
        }

        public void Warn(string message, params object[] args)
        {
            Log.Warn(Format(message, args), Log.Dokan);
        }

        private string Format(string message, params object[] args)
        {
            if (args == null || args.Length == 0)
            {
                return message;
            }

            return string.Format(message, args);
        }
    }
}