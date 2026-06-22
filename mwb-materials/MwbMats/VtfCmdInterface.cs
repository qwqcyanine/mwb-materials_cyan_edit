using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace mwb_materials
{
    class VtfCmdInterface
    {
        public static readonly string FormatDXT1 = "DXT1";
        public static readonly string FormatDXT5 = "DXT5";
        public static readonly string FormatRGBA8888 = "RGBA8888";

        private static void AddProcessArgument(ProcessStartInfo processInfo, string key, string val)
        {
            processInfo.Arguments += "-" + key + " ";
            processInfo.Arguments += "\"" + val + "\" ";
        }

        private static void AddProcessArgument(ProcessStartInfo processInfo, string key)
        {
            processInfo.Arguments += "-" + key + " ";
        }

        public static async Task ExportFile(string file, string outputFolder, string format, bool bNoMips, string moveOutputPath, Action<string> logFunc = null)
        {
            ProcessStartInfo programInfo = new ProcessStartInfo();
            programInfo.WindowStyle = ProcessWindowStyle.Hidden;
            programInfo.CreateNoWindow = true;
            programInfo.UseShellExecute = false;
            programInfo.RedirectStandardOutput = true;
            programInfo.RedirectStandardError = true;
            programInfo.FileName = Path.Combine("vtfcmd", "VTFCmd.exe");

            programInfo.WorkingDirectory = Path.GetDirectoryName(file);
            programInfo.Arguments = string.Empty;
            AddProcessArgument(programInfo, "file", Path.GetFileName(file));
            AddProcessArgument(programInfo, "output", outputFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            AddProcessArgument(programInfo, "format", format);
            AddProcessArgument(programInfo, "alphaformat", format);
            
            if (bNoMips)
            {
                AddProcessArgument(programInfo, "nomipmaps");
            }

            logFunc?.Invoke("VTFCmd: " + programInfo.FileName + " " + programInfo.Arguments.Trim());

            string output;
            string error;

            using (Process runProgram = new Process())
            {
                runProgram.StartInfo = programInfo;
                runProgram.Start();

                Task<string> outputTask = runProgram.StandardOutput.ReadToEndAsync();
                Task<string> errorTask = runProgram.StandardError.ReadToEndAsync();
                await Task.Run(() => runProgram.WaitForExit());

                output = await outputTask;
                error = await errorTask;
            }

            LogProcessText(output, logFunc);
            LogProcessText(error, logFunc);

            string exportedFile = Path.Combine(outputFolder, Path.GetFileNameWithoutExtension(file) + ".vtf");

            if (!File.Exists(exportedFile))
            {
                string message = "VTFCmd did not create " + Path.GetFileName(exportedFile) + ".";

                if (!string.IsNullOrWhiteSpace(output))
                {
                    message += "\n\nOutput:\n" + output.Trim();
                }

                if (!string.IsNullOrWhiteSpace(error))
                {
                    message += "\n\nError:\n" + error.Trim();
                }

                throw new InvalidOperationException(message);
            }

            File.Delete(file);

            if (moveOutputPath != string.Empty)
            {
                string fileDest = Path.Combine(moveOutputPath, Path.GetFileName(exportedFile));

                Directory.CreateDirectory(moveOutputPath);

                if (File.Exists(fileDest))
                {
                    File.Delete(fileDest);
                }

                File.Move(exportedFile, fileDest);
                logFunc?.Invoke("Moved " + Path.GetFileName(exportedFile) + " -> " + fileDest);
            }
            else
            {
                logFunc?.Invoke("Wrote " + exportedFile);
            }
        }

        private static void LogProcessText(string text, Action<string> logFunc)
        {
            if (logFunc == null || string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            using (StringReader reader = new StringReader(text))
            {
                string line;

                while ((line = reader.ReadLine()) != null)
                {
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        logFunc(line.Trim());
                    }
                }
            }
        }
    }
}
