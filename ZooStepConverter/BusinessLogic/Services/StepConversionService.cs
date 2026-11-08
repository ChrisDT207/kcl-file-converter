using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using ZooStepConverter.BusinessLogic.Common;
using ZooStepConverter.DataAccess;

namespace ZooStepConverter.BusinessLogic.Services
{
    /// <summary>
    /// Business Logic Layer: Coordinates batch KCL-to-STEP conversion, mode filtering, and KCL 3.0 auto-fallback compatibility.
    /// </summary>
    public class StepConversionService
    {
        private readonly ZooProcessRunner _processRunner;

        public StepConversionService(ZooProcessRunner processRunner)
        {
            _processRunner = processRunner ?? new ZooProcessRunner();
        }

        public void ConvertFolder(
            string folder,
            ConversionMode mode,
            bool autoFallback,
            string apiToken,
            Func<bool> isCancelRequested,
            Action<string> log,
            Action<int, int> updateProgress,
            Action<string> onOutputDirectoryReady,
            Action onStepFilesProduced)
        {
            var allFiles = Directory.GetFiles(folder, "*.kcl");
            if (allFiles.Length == 0)
            {
                log("[INFO] No .kcl files found in " + folder);
                return;
            }

            // Filter files based on selected mode
            var fileList = new List<string>();
            foreach (string f in allFiles)
            {
                string fname = Path.GetFileName(f);
                if (mode == ConversionMode.AssemblyOnly)
                {
                    if (string.Equals(fname, "main.kcl", StringComparison.OrdinalIgnoreCase))
                        fileList.Add(f);
                }
                else if (mode == ConversionMode.PartsOnly)
                {
                    if (!string.Equals(fname, "main.kcl", StringComparison.OrdinalIgnoreCase))
                        fileList.Add(f);
                }
                else // All files
                {
                    fileList.Add(f);
                }
            }

            if (fileList.Count == 0)
            {
                log("[INFO] No matching .kcl files for selected mode in " + folder);
                return;
            }

            string outDir = Path.Combine(folder, "STEP_Exports");
            string tmpDir = Path.Combine(outDir, "_tmp");
            Directory.CreateDirectory(outDir);
            Directory.CreateDirectory(tmpDir);

            if (onOutputDirectoryReady != null) onOutputDirectoryReady(outDir);
            updateProgress(0, fileList.Count);

            log("");
            log(string.Format("Converting {0} file(s) from: {1}", fileList.Count, folder));
            int ok = 0, fail = 0;
            bool sawAuthError = false;

            for (int i = 0; i < fileList.Count; i++)
            {
                if (isCancelRequested != null && isCancelRequested())
                {
                    log("[!] Conversion aborted by user.");
                    break;
                }

                string file = fileList[i];
                string name = Path.GetFileNameWithoutExtension(file);
                log(string.Format("[{0}/{1}] {2} ...", i + 1, fileList.Count, Path.GetFileName(file)));

                foreach (string old in Directory.GetFiles(tmpDir, "*.step"))
                {
                    try { File.Delete(old); } catch { }
                }

                string outp;
                // Always set working directory to folder so relative imports resolve
                int code = _processRunner.RunZoo(string.Format("kcl export --output-format=step \"{0}\" \"{1}\"", file, tmpDir), folder, apiToken, out outp);

                // Handle KCL 3.0 incompatibility on older CLI versions via auto-fallback
                if (code != 0 && autoFallback && outp.Contains("Unrecognized version") && File.Exists(file))
                {
                    string originalText = File.ReadAllText(file);
                    if (Regex.IsMatch(originalText, @"kclVersion\s*=\s*3(?:\.0)?"))
                    {
                        log("    [i] Detected KCL 3.0 incompatibility with local CLI. Retrying with compatibility patch...");
                        string tempPatchedFile = Path.Combine(tmpDir, "_compat_" + Path.GetFileName(file));
                        try
                        {
                            // Replace 3.0 with "3.0-preview" which older CLIs accept
                            string patchedText = Regex.Replace(originalText, @"kclVersion\s*=\s*3(?:\.0)?", "kclVersion = \"3.0-preview\"");
                            File.WriteAllText(tempPatchedFile, patchedText, Encoding.UTF8);

                            code = _processRunner.RunZoo(string.Format("kcl export --output-format=step \"{0}\" \"{1}\"", tempPatchedFile, tmpDir), folder, apiToken, out outp);
                            if (code == 0)
                            {
                                log("    [+] Compatibility patch succeeded!");
                            }
                        }
                        catch (Exception ex)
                        {
                            log("    [!] Compatibility retry error: " + ex.Message);
                        }
                        finally
                        {
                            if (File.Exists(tempPatchedFile))
                            {
                                try { File.Delete(tempPatchedFile); } catch { }
                            }
                        }
                    }
                }

                if (code == 0)
                {
                    var produced = Directory.GetFiles(tmpDir, "*.step");
                    if (produced.Length > 0)
                    {
                        string dest = Path.Combine(outDir, name + ".step");
                        if (File.Exists(dest)) File.Delete(dest);
                        File.Move(produced[0], dest);
                        log("    [+] Saved " + name + ".step");
                        ok++;
                    }
                    else
                    {
                        // Parameters / dimensions files contain constants but no geometry
                        log("    [!] CLI succeeded but produced no .step file (file may only define constants/parameters).");
                        fail++;
                    }
                }
                else
                {
                    fail++;
                    string errSummary = TextHelpers.CleanErrorSummary(outp);
                    log("    [!] FAILED: " + errSummary);
                    if (outp.Contains("Authorization") && outp.Contains("websocket")) sawAuthError = true;
                }

                updateProgress(i + 1, fileList.Count);
            }

            try { Directory.Delete(tmpDir, true); } catch { }

            log("");
            log(string.Format("Done: {0} converted, {1} failed.", ok, fail));
            if (ok > 0)
            {
                log("STEP files are in: " + outDir);
                if (onStepFilesProduced != null) onStepFilesProduced();
            }

            if (sawAuthError)
            {
                log("");
                log("The \"Bearer <token>\" websocket error usually means the account is OUT OF");
                log("CREDITS, not that the login is wrong (the CLI reports it misleadingly).");
                log("Click \"Check Account\" to confirm, and see https://zoo.dev/account");
            }
        }
    }
}
