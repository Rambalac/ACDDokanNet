using System;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security;

namespace Azi.Tools
{
    public static class Log
    {
        private const string Source = "ACDDokan.Net";

        static Log()
        {
            // try
            // {
            //    EventLog.CreateEventSource(source, "Application");
            // }
            // catch (SecurityException)
            // {
            //    //Just ignore
            // }
        }

        public static void Error(
            string message,
            int eventId = 0,
            short category = 0,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            Console.WriteLine($"{DateTime.Now} {memberName}: {message}\r\n\r\n{sourceFilePath}: {sourceLineNumber}");
            WriteEntry(message, EventLogEntryType.Error, eventId, category, memberName, sourceFilePath, sourceLineNumber);
        }

        public static void Error(
            Exception ex,
            int eventId = 0,
            short category = 0,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            Error(ex.ToString(), eventId, category, memberName, sourceFilePath, sourceLineNumber);
        }

        public static void Warn(
            string message,
            int eventId = 0,
            short category = 0,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            WriteEntry(message, EventLogEntryType.Warning, eventId, category, memberName, sourceFilePath, sourceLineNumber);
        }

        [Conditional("TRACE")]
        public static void Trace(
            string message,
            int eventId = 0,
            short category = 0,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            Info(message, eventId, category, memberName, sourceFilePath, sourceLineNumber);
        }

        public static void Info(
            string message,
            int eventId = 0,
            short category = 0,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            WriteEntry(message, EventLogEntryType.Information, eventId, category, memberName, sourceFilePath, sourceLineNumber);
        }

        public static void WriteEntry(
            string message,
            EventLogEntryType type,
            int eventId = 0,
            short category = 0,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            try
            {
                EventLog.WriteEntry(Source, $"{memberName}: {message}\r\n\r\n{sourceFilePath}: {sourceLineNumber}", type, eventId, category);
            }
            catch (SecurityException)
            {
                // Just ignore
            }
        }

        private static readonly string Query = $"*[System[Provider[@Name = '{Source}']]]";

        public static void Export(string path)
        {
            using (var log = new EventLogSession())
            {
                log.ExportLogAndMessages("Application", PathType.LogName, Query, path);
            }
        }
    }
}