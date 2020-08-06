using System;
using System.IO;

namespace MHW_Save_Backup_Wrapper
{
    public class Logger
    {
        public static string filePath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\MWH_SAVE_BACKUP_LOG.txt";

        private static void BaseLog(string message)
        {
            using (StreamWriter streamWriter = File.AppendText(filePath))
            {
                streamWriter.WriteLine(message);
                streamWriter.Close();
            }
        }
        public static void LogInfo(string message)
        {
            BaseLog(DateTime.Now.ToString() + " [INFO] " + message);
        }

        public static void LogError(string message)
        {
            BaseLog(DateTime.Now.ToString() + "[ERROR] " + message);
        }
    }
}
