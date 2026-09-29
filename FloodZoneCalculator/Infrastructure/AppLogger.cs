using System;
using System.IO;

namespace FloodZoneCalculator.Infrastructure
{
    internal static class AppLogger
    {
        public static void Info(string message) => Write("INFO", message, null);

        public static void Error(string message, Exception exception)
            => Write("ERROR", message, exception);

        private static void Write(string level, string message, Exception exception)
        {
            try
            {
                var directory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
                Directory.CreateDirectory(directory);
                var path = Path.Combine(directory, "floodzone-" + DateTime.Now.ToString("yyyyMMdd") + ".log");
                var text = string.Format("{0:yyyy-MM-dd HH:mm:ss.fff} [{1}] {2}",
                    DateTime.Now, level, message);
                if (exception != null)
                    text += Environment.NewLine + exception;
                File.AppendAllText(path, text + Environment.NewLine);
            }
            catch
            {
                // Logging must never hide the original application error.
            }
        }
    }
}
