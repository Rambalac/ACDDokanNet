using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security;

namespace Azi.Tools
{
    public static class Log
    {
        const string source = "ACDDokan.Net";

        static Log()
        {
            //try
            //{
            //    EventLog.CreateEventSource(source, "Application");
            //}
            //catch (SecurityException)
            //{
            //    //Just ignore
            //}
        }

        public static void Error(
            string message,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            WriteEntry(message, EventLogEntryType.Error, memberName, sourceFilePath, sourceLineNumber);
        }

        public static void Error(
            Exception ex,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            Error(ex.ToString(), memberName, sourceFilePath, sourceLineNumber);
        }

        public static void Warn(
            string message,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            WriteEntry(message, EventLogEntryType.Warning, memberName, sourceFilePath, sourceLineNumber);
        }

        [Conditional("TRACE")]
        public static void Trace(
            string message,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            WriteEntry(message, EventLogEntryType.Information, memberName, sourceFilePath, sourceLineNumber);
        }

        public static void WriteEntry(
            string message,
            EventLogEntryType type,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            try
            {
                EventLog.WriteEntry(source, $"{memberName}: {message}\r\n\r\n{sourceFilePath}: {sourceLineNumber}", type);
            }
            catch (SecurityException)
            {
                //Just ignore
            }

        }


    }
}
