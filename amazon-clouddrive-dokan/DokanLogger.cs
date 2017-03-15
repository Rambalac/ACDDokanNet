namespace Azi.Cloud.DokanNet
{
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
            Log.ErrorTrace(Format(message, args), Log.Dokan);
        }

        public void Fatal(string message, params object[] args)
        {
            Log.ErrorTrace(Format(message, args), Log.Dokan);
        }

        public void Info(string message, params object[] args)
        {
            Log.Trace(Format(message, args), Log.Dokan);
        }

        public void Warn(string message, params object[] args)
        {
            Log.Warn(Format(message, args), Log.Dokan);
        }

        private static string Format(string message, params object[] args)
        {
            if (args == null || args.Length == 0)
            {
                return message;
            }

            return string.Format(message, args);
        }
    }
}