namespace Azi.Cloud.DokanNet
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using System.Linq;
    using global::DokanNet;
    using global::DokanNet.Logging;
    using Tools;

    internal class DokanLogger : ILogger
    {
        public void Debug(string format, params object[] args)
        {
            Log.Trace(string.Format(format, args));
        }

        public void Error(string format, params object[] args)
        {
            Log.Error(string.Format(format, args));
        }

        public void Fatal(string format, params object[] args)
        {
            Log.Error(string.Format(format, args));
        }

        public void Info(string format, params object[] args)
        {
            Log.Trace(string.Format(format, args));
        }

        public void Warn(string format, params object[] args)
        {
            Log.Warn(string.Format(format, args));
        }
    }
}