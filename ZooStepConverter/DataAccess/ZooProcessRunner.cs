using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;

namespace ZooStepConverter.DataAccess
{
    /// <summary>
    /// Data Access Layer: Handles discovery and execution of the local Zoo CLI process.
    /// </summary>
    public class ZooProcessRunner
    {
        // Discovers the path to zoo.exe: PATH first, then standard AppData location.
        public static string FindZooPath()
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string defaultPath = Path.Combine(localAppData, "Zoo", "zoo.exe");
            if (File.Exists(defaultPath)) return defaultPath;

            return "zoo";
        }

        // Returns exit code, or -1 if the zoo CLI could not be started at all.
        public int RunZoo(string args, string workingDir, string apiToken, out string output)
        {
            try
            {
                var psi = new ProcessStartInfo(FindZooPath(), args);
                psi.CreateNoWindow = true;
                psi.UseShellExecute = false;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                if (!string.IsNullOrEmpty(workingDir) && Directory.Exists(workingDir))
                {
                    psi.WorkingDirectory = workingDir;
                }
                if (!string.IsNullOrEmpty(apiToken)) psi.EnvironmentVariables["ZOO_API_TOKEN"] = apiToken;
                using (var p = Process.Start(psi))
                {
                    var so = p.StandardOutput.ReadToEndAsync();
                    var se = p.StandardError.ReadToEndAsync();
                    p.WaitForExit();
                    output = (so.Result + "\n" + se.Result).Trim();
                    return p.ExitCode;
                }
            }
            catch (Win32Exception)
            {
                output = "";
                return -1;
            }
        }
    }
}

