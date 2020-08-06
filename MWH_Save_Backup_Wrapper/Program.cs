using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Timers;

namespace MHW_Save_Backup_Wrapper
{


    class Program
    {
        private const int ProcessWaitCycles = 10;
        private const int ProcessWaitDelay = 2 * 1000; // 2 seconds
        private const int SaveTimerInterval = 3 * 60 * 1000; // 3 minutes

        private const string SteamRegPath = "Software\\Wow6432Node\\Valve\\Steam";
        private const string SteamRegKey = "InstallPath";
        private const string SteamGameSavesRelativePath = "\\userdata\\";

        private const string MHWExecutableName = "MonsterHunterWorld";
        private const string MHWExecRelativePath = "\\steamapps\\common\\Monster Hunter World\\" + MHWExecutableName + ".exe";
        private const string MHWGameIDRelativePath = "\\582010";
        private const string MHWSaveGameDirName = "\\remote";
        private const string MHWSaveGameFile = "SAVEDATA1000";

        private static System.Timers.Timer saveTimer;

        class SaveGameData { public DateTime LastModified; }

        private static Tuple<string, string> getSteamDirectories()
        {
            string MHWSaveDataPath = null;

            // Get Steam path first
            RegistryKey key = Registry.LocalMachine.OpenSubKey(SteamRegPath);
            if (key == null)
            {
                throw new ArgumentException("Registry key HKLM\\" + SteamRegPath + "\\" + SteamRegKey + " not found");
            }

            Object steamPathObject = key.GetValue(SteamRegKey);
            string SteamPath = steamPathObject as string;

            // Get Steam UserProfile dir
            string[] userProfileDirNames = Directory.GetDirectories(SteamPath + SteamGameSavesRelativePath);
            foreach (var dir in userProfileDirNames)
            {
                string[] gameFolders = Directory.GetDirectories(dir);
                if (gameFolders.Contains(dir + MHWGameIDRelativePath))
                {
                    MHWSaveDataPath = dir + MHWGameIDRelativePath + MHWSaveGameDirName;
                    break;
                }
            }

            if (MHWSaveDataPath == null)
            {
                throw new ArgumentException("Unable to find MHW savegame location");
            }

            return Tuple.Create(SteamPath, MHWSaveDataPath);
        }

        private static DateTime getLastModifiedDate(string path)
        {
            return System.IO.File.GetLastWriteTime(path);
        }

        private static void backupSaveGames(object source, ElapsedEventArgs e, string saveDataFile, SaveGameData data)
        {
            DateTime currentLastModified = getLastModifiedDate(saveDataFile);

            if (currentLastModified > data.LastModified)
            {
                Logger.LogInfo("Found changes in save data. Making new backup...");
                string saveDirectory = saveDataFile.Substring(0, saveDataFile.Length - (MHWSaveGameFile.Length + 1));
                string currentLastModifiedString = currentLastModified.ToString("");
                try
                {
                    ZipFile.CreateFromDirectory(saveDirectory, saveDirectory + string.Format("_{0:ddMMyyZHHmm}.zip", currentLastModified));
                }
                catch (IOException ex)
                {
                    Logger.LogError(ex.Message);
                }
                data.LastModified = currentLastModified;
            }
            else { Logger.LogInfo("No changes found in save data."); }
        }

        private static bool StartMHW()
        {
            try
            {
                var (SteamPath, MHWSaveDataPath) = getSteamDirectories();

                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = SteamPath + MHWExecRelativePath,
                        Arguments = "",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = false
                    }
                };

                process.Start();
                process.WaitForInputIdle(10000);
                process.WaitForExit(10000);
                Logger.LogInfo("Game started");

                Process MWHProcess = null;
                // Try to find MWH process
                for (int i = 0; i < ProcessWaitCycles; i++)
                {
                    Process[] p = Process.GetProcessesByName(MHWExecutableName);
                    if (p.Length == 0)
                    {
                        Thread.Sleep(ProcessWaitDelay);
                        continue;
                    }

                    MWHProcess = p[0];
                }

                if (MWHProcess == null)
                {
                    throw new ArgumentException("Unable to find Game Process");
                }

                Logger.LogInfo("Found Game Process: " + MWHProcess.Id);
                SaveGameData c = new SaveGameData { LastModified = getLastModifiedDate(MHWSaveDataPath + "\\" + MHWSaveGameFile) };
                saveTimer = new System.Timers.Timer(SaveTimerInterval);
                saveTimer.Elapsed += new ElapsedEventHandler((sender, e) => backupSaveGames(sender, e, MHWSaveDataPath + "\\" + MHWSaveGameFile, c));
                saveTimer.Enabled = true;

                MWHProcess.WaitForInputIdle();
                MWHProcess.WaitForExit();
                Logger.LogInfo("Game exited");
                saveTimer.Enabled = false;
            }
            catch (ArgumentException e)
            {
                Logger.LogError(e.Message);
                return false;
            }

            Console.ReadLine();
            return true;
        }

        static void Main(string[] args)
        {
            StartMHW();
        }
    }
}