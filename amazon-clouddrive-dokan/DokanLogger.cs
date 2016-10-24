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
            Log.Trace(message, Log.Dokan);
        }

        public void Error(string message, params object[] args)
        {
            Log.Error(message, Log.Dokan);
        }

        public void Fatal(string message, params object[] args)
        {
            Log.Error(message, Log.Dokan);
        }

        public void Info(string message, params object[] args)
        {
            Log.Info(message, Log.Dokan);
        }

        public void Warn(string message, params object[] args)
        {
            Log.Warn(message, Log.Dokan);
        }
    }
}