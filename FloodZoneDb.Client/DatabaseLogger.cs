using System;
using System.IO;

namespace FloodZoneDb.Client;

public static class DatabaseLogger
{
    public static void Info(string message) => Write("INFO", message, null);

    public static void Error(string message, Exception exception)
        => Write("ERROR", message, exception);

    private static void Write(string level, string message, Exception? exception)
    {
        try
        {
            var directory = Path.Combine(AppContext.BaseDirectory, "logs");
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "floodzone-" + DateTime.Now.ToString("yyyyMMdd") + ".log");
            var text = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}";
            if (exception != null)
                text += Environment.NewLine + exception;
            File.AppendAllText(path, text + Environment.NewLine);
        }
        catch
        {
            // Logging must never prevent the application from showing the original error.
        }
    }
}
