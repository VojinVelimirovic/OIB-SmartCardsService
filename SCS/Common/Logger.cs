using System;
using System.Diagnostics;

namespace Common
{
    public static class Logger
    {

        private const string Source = "SmartCardService";
        private const string LogName = "Application";

        static Logger()
        {
            // Check if the source exists, if not, create it
            try
            {
                if (!EventLog.SourceExists(Source))
                {
                    EventLog.CreateEventSource(Source, LogName);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        public static void LogEvent(string message)
        {
            // TODO: Expand method for different EventLogEntryType levels?
            EventLog.WriteEntry(Source, message, EventLogEntryType.Information);
        }

    }
}
