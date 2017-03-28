// ReSharper disable ExplicitCallerInfoArgument

namespace Azi.Tools
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Eventing.Reader;
    using System.Runtime.CompilerServices;
    using System.Security;
    using System.Threading.Tasks;
    using Microsoft.HockeyApp;

    public static class UnitTestDetector
    {
        public static bool IsUnitTest { get; set; }
    }

    public static class Log
    {
        public const int BigFile = 300;
        public const int Dokan = 100;
        public const int Performance = 10000;
        public const int VirtualDrive = 200;
        private const string Source = "ACDDokan.Net";
        private static readonly string Query = $"*[System[Provider[@Name = '{Source}']]]";
        private static string version;

        static Log()
        {
            if (!UnitTestDetector.IsUnitTest)
            {
                HockeyClient.Current.Configure("7ca5f74596c44804825d1d2a4d3a99e5");
#if DEBUG
                ((HockeyClient) HockeyClient.Current).OnHockeySDKInternalException += (sender, args) =>
                {
                    if (Debugger.IsAttached)
                    {
                        Debugger.Break();
                    }
                };
#endif

                try
                {
                    EventLog.CreateEventSource(Source, "Application");
                }
                catch (Exception)
                {
                    // Just ignore
                }
            }
        }

#if DEBUG
        public static bool HockeyAppEnabled { get; set; } = !UnitTestDetector.IsUnitTest;
#else
        public static bool HockeyAppEnabled { get; set; }
#endif

        public static void Error(
            Exception ex,
            int eventId = 0,
            short category = 0,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            Console.WriteLine($"{DateTime.Now} {memberName}: {ex}\r\n\r\n{sourceFilePath}: {sourceLineNumber}");
            WriteEntry($"{memberName}: {ex}", EventLogEntryType.Error, eventId, category, memberName, sourceFilePath, sourceLineNumber);
            if (HockeyAppEnabled)
            {
                TrackException(ex, MakeDict(eventId, category, memberName, sourceFilePath, sourceLineNumber));
            }
        }

        public static void Error(
            string message,
            Exception ex,
            int eventId = 0,
            short category = 0,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            Console.WriteLine($"{DateTime.Now} {memberName}: {message}\r\n{ex}\r\n\r\n{sourceFilePath}: {sourceLineNumber}");
            WriteEntry($"{memberName}: {message} - {ex}", EventLogEntryType.Error, eventId, category, memberName, sourceFilePath, sourceLineNumber);
            if (HockeyAppEnabled)
            {
                TrackException(ex, MakeDict(eventId, category, memberName, sourceFilePath, sourceLineNumber, message));
            }
        }

        public static void ErrorTrace(
            string message,
            int eventId = 0,
            short category = 0,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            Console.WriteLine($"{DateTime.Now} {memberName}: {message}\r\n\r\n{sourceFilePath}: {sourceLineNumber}");
            WriteEntry($"{memberName}: {message}", EventLogEntryType.Error, eventId, category, memberName, sourceFilePath, sourceLineNumber);
            if (HockeyAppEnabled)
            {
                TrackException(new MessageException(message), MakeDict(eventId, category, memberName, sourceFilePath, sourceLineNumber));
            }
        }

        public static void Export(string path)
        {
            using (var log = new EventLogSession())
            {
                log.ExportLogAndMessages("Application", PathType.LogName, Query, path);
            }
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

        public static void Init(string version)
        {
            Log.version = version;
            //await HockeyClient.Current.SendCrashesAsync(true);
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

        public static void Warn(
            string message,
            int eventId = 0,
            short category = 0,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            WriteEntry(message, EventLogEntryType.Warning, eventId, category, memberName, sourceFilePath, sourceLineNumber);
            if (HockeyAppEnabled)
            {
                TrackException(new WarningException(message), MakeDict(eventId, category, memberName, sourceFilePath, sourceLineNumber));
            }
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

        private static IDictionary<string, string> MakeDict(int eventId, short category, string memberName, string sourceFilePath, int sourceLineNumber, string message = null)
        {
            var result = new Dictionary<string, string>
            {
                { "eventId", eventId.ToString() },
                { "category", category.ToString() },
                { "memberName", memberName },
                { "sourceFilePath", sourceFilePath },
                { "sourceLineNumber", sourceLineNumber.ToString() },
                { "version", version }
            };
            if (message != null)
            {
                result.Add("message", message);
            }

            return result;
        }

        private static void TrackException(Exception exception, IDictionary<string, string> makeDict)
        {
            (HockeyClient.Current as HockeyClient)?.HandleException(exception);
        }

        private class MessageException : Exception
        {
            public MessageException(string message)
                : base(message)
            {
                var stack = new StackTrace(2);
                StackTrace = stack.ToString();
            }

            public override string StackTrace { get; }
        }

        private class WarningException : Exception
        {
            public WarningException(string message)
                : base(message)
            {
                var stack = new StackTrace(2);
                StackTrace = stack.ToString();
            }

            public override string StackTrace { get; }
        }
    }
}