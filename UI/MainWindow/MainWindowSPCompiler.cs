﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using MahApps.Metro.Controls.Dialogs;
using SPCode.Interop;
using SPCode.Utils;

namespace SPCode.UI
{
    public partial class MainWindow
    {
        private List<string> ScriptsCompiled;

        private readonly List<string> CompiledFileNames = new();
        private readonly List<string> CompiledFiles = new();
        private readonly List<string> NonUploadedFiles = new();

        private bool InCompiling;
        private Thread ServerCheckThread;

        private ProgressDialogController ProgressTask;

        private bool ServerIsRunning;
        private Process ServerProcess;

        /// <summary>
        /// Compiles the specified scripts.
        /// </summary>
        /// <param name="compileAll"></param>
        private async void Compile_SPScripts(bool compileAll = true)
        {
            // Checks if the program is compiling to avoid doing it again, and checks if the editor is from the templates window
            var ee = GetCurrentEditorElement();
            if (InCompiling || (ee != null && ee.IsTemplateEditor))
            {
                return;
            }

            // Saves all editors, sets InCompiling flag, clears all fields
            Command_SaveAll();
            InCompiling = true;
            CompiledFiles.Clear();
            CompiledFileNames.Clear();
            NonUploadedFiles.Clear();

            // Grabs current config
            var currentConfig = Program.Configs[Program.SelectedConfig];

            // Creates flags
            FileInfo spCompInfo = null;
            var SpCompFound = false;
            var PressedEscape = false;
            var hadError = false;
            var warnings = 0;

            // Searches for the spcomp.exe compiler
            foreach (var dir in currentConfig.SMDirectories)
            {
                spCompInfo = new FileInfo(Path.Combine(dir, "spcomp.exe"));
                if (spCompInfo.Exists)
                {
                    SpCompFound = true;
                    break;
                }
            }

            if (!SpCompFound)
            {
                LoggingControl.LogAction($"No compiler found, aborting.");
                await this.ShowMessageAsync(Program.Translations.Get("Error"),
                    Program.Translations.Get("SPCompNotFound"), MessageDialogStyle.Affirmative,
                    MetroDialogOptions);
                InCompiling = false;
                return;
            }

            // If the compiler was found, it starts adding to a list all of the files to compile
            ScriptsCompiled = new();
            if (compileAll)
            {
                var editors = GetAllEditorElements();
                if (editors == null)
                {
                    InCompiling = false;
                    return;
                }

                foreach (var editor in editors)
                {
                    var compileBoxIsChecked = editor.CompileBox.IsChecked;
                    if (compileBoxIsChecked != null && compileBoxIsChecked.Value)
                    {
                        ScriptsCompiled.Add(editor.FullFilePath);
                    }
                    else
                    {
                        LoggingControl.LogAction($"{new FileInfo(editor.FullFilePath).Name} (omitted)");
                    }
                }
            }
            else
            {
                if (ee == null)
                {
                    InCompiling = false;
                    return;
                }

                if (ee.FullFilePath.EndsWith(".sp"))
                {
                    ScriptsCompiled.Add(ee.FullFilePath);
                }
            }

            var compileCount = ScriptsCompiled.Count;
            if (compileCount > 0)
            {
                // Shows the 'Compiling...' window
                ErrorResultGrid.Items.Clear();

                ProgressTask = await this.ShowProgressAsync(Program.Translations.Get("Compiling"), "",
                    false, MetroDialogOptions);
                ProgressTask.SetProgress(0.0);

                var stringOutput = new StringBuilder();
                var regexOptions = RegexOptions.Compiled | RegexOptions.ExplicitCapture | RegexOptions.Multiline;
                var errorFilterRegex = new Regex(Constants.ErrorFilterRegex, regexOptions);

                var compiledSuccess = 0;

                // Loops through all files to compile
                for (var i = 0; i < compileCount; ++i)
                {
                    if (!InCompiling) //pressed escape
                    {
                        PressedEscape = true;
                        break;
                    }

                    var file = ScriptsCompiled[i];
                    ProgressTask.SetMessage($"{file} ({i}/{compileCount}) ");
                    ProcessUITasks();
                    var fileInfo = new FileInfo(file);
                    if (fileInfo.Exists)
                    {
                        var process = new Process();
                        process.StartInfo.WorkingDirectory =
                            fileInfo.DirectoryName ?? throw new NullReferenceException();
                        process.StartInfo.UseShellExecute = true;
                        process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                        process.StartInfo.CreateNoWindow = true;
                        process.StartInfo.FileName = spCompInfo.FullName;

                        var destinationFileName = ShortenScriptFileName(fileInfo.Name) + ".smx";
                        var outFile = Path.Combine(fileInfo.DirectoryName, destinationFileName);
                        if (File.Exists(outFile))
                        {
                            File.Delete(outFile);
                        }

                        var errorFile = $@"{fileInfo.DirectoryName}\error_{Environment.TickCount}_{file.GetHashCode():X}_{i}.txt";
                        if (File.Exists(errorFile))
                        {
                            File.Delete(errorFile);
                        }

                        var includeDirectories = new StringBuilder();
                        foreach (var dir in currentConfig.SMDirectories)
                        {
                            includeDirectories.Append(" -i=\"" + dir + "\"");
                        }

                        var includeStr = includeDirectories.ToString();

                        process.StartInfo.Arguments =
                            "\"" + fileInfo.FullName + "\" -o=\"" + outFile + "\" -e=\"" + errorFile + "\"" +
                            includeStr + " -O=" + currentConfig.OptimizeLevel + " -v=" + currentConfig.VerboseLevel;
                        ProgressTask.SetProgress((i + 1 - 0.5d) / compileCount);
                        var execResult = ExecuteCommandLine(currentConfig.PreCmd, fileInfo.DirectoryName, currentConfig.CopyDirectory,
                            fileInfo.FullName, fileInfo.Name, outFile, destinationFileName);

                        ProcessUITasks();

                        try
                        {
                            process.Start();
                            process.WaitForExit();

                            if (process.ExitCode != 1 && process.ExitCode != 0)
                            {
                                await ProgressTask.CloseAsync();
                                await this.ShowMessageAsync(Program.Translations.Get("Error"),
                                    "The SourcePawn compiler has crashed.\n" +
                                    "Try again, or file an issue at the SourcePawn GitHub repository describing your steps that lead to this instance in detail.\n" +
                                    $"Exit code: {process.ExitCode:X}", MessageDialogStyle.Affirmative,
                                    MetroDialogOptions);
                                LoggingControl.LogAction($"Compiler crash detected, file: {fileInfo.Name}", 2);
                                InCompiling = false;
                                return;
                            }
                        }
                        catch (Exception)
                        {
                            await ProgressTask.CloseAsync();
                            await this.ShowMessageAsync(Program.Translations.Get("SPCompNotStarted"),
                                Program.Translations.Get("Error"), MessageDialogStyle.Affirmative,
                                MetroDialogOptions);
                            InCompiling = false;
                            return;
                        }

                        if (File.Exists(errorFile))
                        {
                            warnings = 0;
                            hadError = false;
                            var errorStr = File.ReadAllText(errorFile);
                            stringOutput.AppendLine(errorStr.Trim('\n', '\r'));
                            var mc = errorFilterRegex.Matches(errorStr);
                            for (var j = 0; j < mc.Count; ++j)
                            {
                                ErrorResultGrid.Items.Add(new ErrorDataGridRow
                                {
                                    File = mc[j].Groups["File"].Value.Trim(),
                                    Line = mc[j].Groups["Line"].Value.Trim(),
                                    Type = mc[j].Groups["Type"].Value.Trim(),
                                    Details = mc[j].Groups["Details"].Value.Trim()
                                });
                                if (mc[j].Groups["Type"].Value.Contains("error"))
                                {
                                    hadError = true;
                                }
                                if (mc[j].Groups["Type"].Value.Contains("warning"))
                                {
                                    warnings++;
                                }
                            }
                            File.Delete(errorFile);
                        }

                        if (hadError)
                        {
                            LoggingControl.LogAction(fileInfo.Name + " (error)");
                        }
                        else
                        {
                            LoggingControl.LogAction($"{fileInfo.Name}{(warnings > 0 ? $" ({warnings} warnings)" : "")}");
                            compiledSuccess++;
                        }

                        if (File.Exists(outFile))
                        {
                            CompiledFiles.Add(outFile);
                            NonUploadedFiles.Add(outFile);
                            CompiledFileNames.Add(destinationFileName);
                        }

                        var execResult_Post = ExecuteCommandLine(currentConfig.PostCmd, fileInfo.DirectoryName,
                            currentConfig.CopyDirectory, fileInfo.FullName, fileInfo.Name, outFile, destinationFileName);

                        ProgressTask.SetProgress((double)(i + 1) / compileCount);
                        ProcessUITasks();
                    }
                }

                if (compiledSuccess > 0)
                {
                    LoggingControl.LogAction($"Compiled {compiledSuccess} {(compiledSuccess > 1 ? "plugins" : "plugin")}.", 2);
                }

                if (!PressedEscape)
                {
                    ProgressTask.SetProgress(1.0);
                    if (currentConfig.AutoCopy)
                    {
                        ProgressTask.SetTitle(Program.Translations.Get("CopyingFiles") + "...");
                        ProgressTask.SetIndeterminate();
                        await Task.Run(() => Copy_Plugins());
                        ProgressTask.SetProgress(1.0);
                    }

                    if (currentConfig.AutoUpload)
                    {
                        ProgressTask.SetTitle(Program.Translations.Get("FTPUploading") + "...");
                        ProgressTask.SetIndeterminate();
                        await Task.Run(FTPUpload_Plugins);
                        ProgressTask.SetProgress(1.0);
                    }

                    if (currentConfig.AutoRCON)
                    {
                        ProgressTask.SetTitle(Program.Translations.Get("RCONCommand") + "...");
                        ProgressTask.SetIndeterminate();
                        await Task.Run(Server_Query);
                        ProgressTask.SetProgress(1.0);
                    }

                    if (CompileOutputRow.Height.Value < 11.0)
                    {
                        CompileOutputRow.Height = new GridLength(200.0);
                    }
                }

                await ProgressTask.CloseAsync();
            }
            InCompiling = false;
        }

        /// <summary>
        /// Copies the compiled plugins to the specified destination.
        /// </summary>
        private void Copy_Plugins()
        {
            var output = new List<string>();
            if (CompiledFiles.Count <= 0)
            {
                LoggingControl.LogAction("No plugins found to copy.", 2);
                return;
            }
            var copyCount = 0;
            var c = Program.Configs[Program.SelectedConfig];
            if (string.IsNullOrWhiteSpace(c.CopyDirectory))
            {
                output.Add($"Copy directory is empty.");
                goto Dispatcher;
            }
            if (!Directory.Exists(c.CopyDirectory))
            {
                output.Add("The specified Copy Directory was not found.");
                goto Dispatcher;
            }
            output.Add($"Copying plugin(s)...");
            NonUploadedFiles.Clear();
            var stringOutput = new StringBuilder();
            foreach (var file in CompiledFiles)
            {
                var destFile = new FileInfo(file);
                try
                {
                    if (destFile.Exists)
                    {
                        var destinationFileName = destFile.Name;
                        var copyFileDestination = Path.Combine(c.CopyDirectory, destinationFileName);
                        File.Copy(file, copyFileDestination, true);
                        NonUploadedFiles.Add(copyFileDestination);
                        output.Add($"{Program.Translations.Get("Copied")}: {copyFileDestination}");
                        ++copyCount;
                        if (c.DeleteAfterCopy)
                        {
                            File.Delete(file);
                            output.Add($"{Program.Translations.Get("Deleted")}: {copyFileDestination}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    output.Add($"{Program.Translations.Get("FailCopy")}: {destFile.Name}");
                    output.Add(ex.Message);
                }
            }

            if (copyCount == 0)
            {
                output.Add($"{Program.Translations.Get("NoFilesCopy")}");
            }

        Dispatcher:

            Dispatcher.Invoke(() =>
            {
                output.ForEach(x => LoggingControl.LogAction(x));
                if (CompileOutputRow.Height.Value < 11.0)
                {
                    CompileOutputRow.Height = new GridLength(200.0);
                }
            });
        }

        /// <summary>
        /// Uploads the compiled plugins via FTP to the specified destination.
        /// </summary>
        private void FTPUpload_Plugins()
        {
            var output = new List<string>();
            if (NonUploadedFiles.Count <= 0)
            {
                LoggingControl.LogAction("No plugins found to upload.", 2);
                return;
            }

            var c = Program.Configs[Program.SelectedConfig];
            if (string.IsNullOrWhiteSpace(c.FTPHost) || string.IsNullOrWhiteSpace(c.FTPUser))
            {
                output.Add("FTP Host or User fields are empty.");
                goto Dispatcher;
            }

            output.Add("Uploading plugin(s)...");

            try
            {
                var ftp = new FTP(c.FTPHost, c.FTPUser, c.FTPPassword);
                foreach (var file in NonUploadedFiles)
                {
                    var fileInfo = new FileInfo(file);
                    if (fileInfo.Exists)
                    {
                        string uploadDir;
                        if (string.IsNullOrWhiteSpace(c.FTPDir))
                        {
                            uploadDir = fileInfo.Name;
                        }
                        else
                        {
                            uploadDir = c.FTPDir.TrimEnd('/') + "/" + fileInfo.Name;
                        }

                        try
                        {
                            ftp.Upload(uploadDir, file);
                            output.Add($"{Program.Translations.Get("Uploaded")}: {fileInfo.Name}");
                        }
                        catch (Exception e)
                        {
                            output.Add(string.Format(Program.Translations.Get("ErrorUploadFile"),
                                fileInfo.Name, uploadDir));
                            output.Add($"{Program.Translations.Get("Details")}: {e.Message}");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                output.Add(Program.Translations.Get("ErrorUpload"));
                output.Add($"{Program.Translations.Get("Details")}: " + e.Message);
            }

        Dispatcher:

            Dispatcher.Invoke(() =>
            {
                output.ForEach(x => LoggingControl.LogAction(x));
                if (CompileOutputRow.Height.Value < 11.0)
                {
                    CompileOutputRow.Height = new GridLength(200.0);
                }
            });
        }

        /// <summary>
        /// Starts the server with the specified parameters.
        /// </summary>
        private void Server_Start()
        {
            if (ServerIsRunning)
            {
                LoggingControl.LogAction("The server is already running!", 2);
                return;
            }
            var c = Program.Configs[Program.SelectedConfig];
            var serverOptionsPath = c.ServerFile;
            if (string.IsNullOrWhiteSpace(serverOptionsPath))
            {
                LoggingControl.LogAction("No executable specified to start server.", 2);
                return;
            }

            var serverExec = new FileInfo(serverOptionsPath);
            if (!serverExec.Exists)
            {
                LoggingControl.LogAction("The specified server executable file doesn't exist.", 2);
                return;
            }

            LoggingControl.LogAction("Starting server...");
            try
            {
                ServerProcess = new Process
                {
                    StartInfo =
                    {
                        UseShellExecute = true,
                        FileName = serverExec.FullName,
                        WorkingDirectory = serverExec.DirectoryName ?? throw new NullReferenceException(),
                        Arguments = c.ServerArgs
                    }
                };
                ServerCheckThread = new Thread(ProcessCheckWorker);
                ServerCheckThread.Start();
            }
            catch (Exception)
            {
                ServerProcess?.Dispose();
            }
        }

        private void ProcessCheckWorker()
        {
            try
            {
                ServerProcess.Start();
                ServerIsRunning = true;
                Program.MainWindow.Dispatcher?.Invoke(() =>
                {
                    EnableServerAnim.Begin();
                    UpdateWindowTitle();
                    LoggingControl.LogAction("Server started.", 2);
                });
                ServerProcess.WaitForExit();
                ServerProcess.Dispose();
                ServerIsRunning = false;
                Program.MainWindow.Dispatcher?.Invoke(() =>
                {
                    if (Program.MainWindow.IsLoaded)
                    {
                        DisableServerAnim.Begin();
                        UpdateWindowTitle();
                        LoggingControl.LogAction("Server stopped.", 2);
                    }
                });
            }
            catch (Exception)
            {
                return;
            }
        }

        private string ShortenScriptFileName(string fileName)
        {
            if (fileName.EndsWith(".sp", StringComparison.InvariantCultureIgnoreCase))
            {
                return fileName.Substring(0, fileName.Length - 3);
            }

            return fileName;
        }

        /// <summary>
        /// Executes the commands from the pre and post compile commands boxes.
        /// </summary>
        private string ExecuteCommandLine(string code, string directory, string copyDir, string scriptFile,
            string scriptName, string pluginFile, string pluginName)
        {
            code = ReplaceCMDVariables(code, directory, copyDir, scriptFile, scriptName, pluginFile, pluginName);
            if (string.IsNullOrWhiteSpace(code))
            {
                return null;
            }

            var batchFile = new FileInfo(@$"{Paths.GetTempDirectory()}\{Environment.TickCount}_{(uint)code.GetHashCode() ^ (uint)directory.GetHashCode()}_temp.bat").FullName;
            File.WriteAllText(batchFile, code);
            string result;
            using (var process = new Process())
            {
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.FileName = "cmd.exe";
                process.StartInfo.WorkingDirectory = directory;
                process.StartInfo.Arguments = "/c \"" + batchFile + "\"";
                process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.RedirectStandardOutput = true;
                process.Start();
                process.WaitForExit();
                using var reader = process.StandardOutput;
                result = reader.ReadToEnd();
            }

            File.Delete(batchFile);
            return result;
        }

        /// <summary>
        /// Replaces the placeholders from the pre and post compile commands boxes with the corresponding content.
        /// </summary>
        private string ReplaceCMDVariables(string CMD, string scriptDir, string copyDir, string scriptFile,
            string scriptName, string pluginFile, string pluginName)
        {
            CMD = CMD.Replace("{editordir}", Environment.CurrentDirectory.Trim('\\'));
            CMD = CMD.Replace("{scriptdir}", scriptDir);
            CMD = CMD.Replace("{copydir}", copyDir);
            CMD = CMD.Replace("{scriptfile}", scriptFile);
            CMD = CMD.Replace("{scriptname}", scriptName);
            CMD = CMD.Replace("{pluginfile}", pluginFile);
            CMD = CMD.Replace("{pluginname}", pluginName);
            return CMD;
        }
    }
}