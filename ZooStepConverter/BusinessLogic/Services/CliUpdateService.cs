using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using ZooStepConverter.BusinessLogic.Common;
using ZooStepConverter.DataAccess;

namespace ZooStepConverter.BusinessLogic.Services
{
    /// <summary>
    /// Business Logic Layer: Handles updating the Zoo CLI binary with lock-bypassing atomic replacement and fallback support.
    /// </summary>
    public class CliUpdateService
    {
        private readonly ZooProcessRunner _processRunner;

        public CliUpdateService(ZooProcessRunner processRunner)
        {
            _processRunner = processRunner ?? new ZooProcessRunner();
        }

        public void UpdateCli(string apiToken, Action<string> log, Action onPreflightNeeded)
        {
            log("");
            log("Updating Zoo CLI...");
            try
            {
                // Kill any running zoo instances that might lock the file
                foreach (var proc in Process.GetProcessesByName("zoo"))
                {
                    try { proc.Kill(); proc.WaitForExit(1500); } catch { }
                }

                string zooPath = ZooProcessRunner.FindZooPath();
                if (string.IsNullOrEmpty(zooPath) || zooPath == "zoo")
                {
                    string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    zooPath = Path.Combine(localAppData, "Zoo", "zoo.exe");
                }

                string zooDir = Path.GetDirectoryName(zooPath);
                if (!Directory.Exists(zooDir)) Directory.CreateDirectory(zooDir);

                string downloadUrl = "https://github.com/KittyCAD/cli/releases/download/v0.2.204/zoo-x86_64-pc-windows-gnu";
                try
                {
                    using (var wcApi = new WebClient())
                    {
                        wcApi.Headers["User-Agent"] = "ZooStepConverter";
                        string releaseJson = wcApi.DownloadString("https://api.github.com/repos/KittyCAD/cli/releases/latest");
                        string tag = TextHelpers.JsonStr(releaseJson, "tag_name");
                        if (!string.IsNullOrEmpty(tag))
                        {
                            downloadUrl = "https://github.com/KittyCAD/cli/releases/download/" + tag + "/zoo-x86_64-pc-windows-gnu";
                            log("  Found latest release on GitHub: " + tag);
                        }
                    }
                }
                catch { }

                string tempDownload = Path.Combine(zooDir, "zoo.download.exe");
                string oldBackup = Path.Combine(zooDir, "zoo.old.exe");

                if (File.Exists(tempDownload))
                {
                    try { File.Delete(tempDownload); } catch { }
                }

                log("  [1/3] Downloading latest binary...");
                using (var wc = new WebClient())
                {
                    wc.Headers["User-Agent"] = "ZooStepConverter";
                    wc.DownloadFile(downloadUrl, tempDownload);
                }

                log("  [2/3] Safely replacing zoo.exe (bypassing Windows file lock)...");
                if (File.Exists(oldBackup))
                {
                    try { File.Delete(oldBackup); } catch { }
                }

                if (File.Exists(zooPath))
                {
                    File.Move(zooPath, oldBackup);
                }

                File.Move(tempDownload, zooPath);

                try
                {
                    if (File.Exists(oldBackup)) File.Delete(oldBackup);
                }
                catch { }

                log("  [3/3] Zoo CLI updated successfully!");
                if (onPreflightNeeded != null) onPreflightNeeded();
            }
            catch (Exception ex)
            {
                log("  [!] Direct download error: " + ex.Message);
                log("      Attempting CLI self-update command as fallback...");
                string outp;
                int code = _processRunner.RunZoo("update", null, apiToken, out outp);
                log("      " + outp);
                if (code == 0 && onPreflightNeeded != null) onPreflightNeeded();
            }
        }
    }
}

