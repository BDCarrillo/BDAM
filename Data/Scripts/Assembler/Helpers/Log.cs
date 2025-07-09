using Sandbox.ModAPI;
using System;
using System.IO;
using VRage.Utils;

namespace BDAM
{
    internal static class Log
    {
        internal const string LOG_PREFIX = "BDAM";
        internal const string LOG_SUFFIX = ".log";
        internal const int LOGS_TO_KEEP = 2;

        internal static TextWriter TextWriter;

        internal static void InitLogs()
        {
            int last = LOGS_TO_KEEP - 1;
            string lastName = LOG_PREFIX + last + LOG_SUFFIX;
            if (MyAPIGateway.Utilities.FileExistsInLocalStorage(lastName, typeof(Log)))
                MyAPIGateway.Utilities.DeleteFileInLocalStorage(lastName, typeof(Log));

            if (last > 0)
            {
                for (int i = last; i > 0; i--)
                {
                    string oldName = LOG_PREFIX + (i - 1) + LOG_SUFFIX;
                    string newName = LOG_PREFIX + i + LOG_SUFFIX;
                    RenameFileInLocalStorage(oldName, newName, typeof(Log));
                }
            }
            string fileName = LOG_PREFIX + 0 + LOG_SUFFIX;
            TextWriter = MyAPIGateway.Utilities.WriteFileInLocalStorage(fileName, typeof(Log));
            var message = $"{DateTime.Now:dd-MM-yy HH-mm-ss} - Logging Started";
            TextWriter.WriteLine(message);
            TextWriter.Flush();
        }

        internal static void RenameFileInLocalStorage(string oldName, string newName, Type anyObjectInYourMod)
        {
            if (!MyAPIGateway.Utilities.FileExistsInLocalStorage(oldName, anyObjectInYourMod))
                return;

            if (MyAPIGateway.Utilities.FileExistsInLocalStorage(newName, anyObjectInYourMod))
                return;

            using (var read = MyAPIGateway.Utilities.ReadFileInLocalStorage(oldName, anyObjectInYourMod))
            {
                using (var write = MyAPIGateway.Utilities.WriteFileInLocalStorage(newName, anyObjectInYourMod))
                {
                    write.Write(read.ReadToEnd());
                    write.Flush();
                    write.Dispose();
                }
            }

            MyAPIGateway.Utilities.DeleteFileInLocalStorage(oldName, anyObjectInYourMod);
        }

        internal static void WriteLine(string text)
        {
            var message = $"{DateTime.Now:MM-dd-yy_HH-mm-ss-fff} {text}";

            lock (TextWriter)
            {
                TextWriter.WriteLine(message);
                TextWriter.Flush();
            }
        }

        internal static void Close()
        {
            try
            {

                var message = $"{DateTime.Now:dd-MM-yy HH-mm-ss} - Logging Stopped";
                TextWriter.WriteLine(message);

                TextWriter.Flush();
                TextWriter.Close();
                TextWriter.Dispose();
            }
            catch (Exception e)
            {
                MyLog.Default.WriteLine($"{Session.modName} error closing log: \n {e}");
            }
        }

        internal static void LogException(Exception ex)
        {
            var hasInner = ex.InnerException != null;
            var text = !hasInner ? $"{ex.Message}\n{ex.StackTrace}" : ex.Message;
            WriteLine(text);
            if (hasInner)
                LogException(ex.InnerException);
        }
    }
}