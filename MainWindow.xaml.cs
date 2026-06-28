using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using System.Linq;
using Microsoft.Web.WebView2.Core;

namespace TiwutLauncher
{
    public partial class MainWindow : Window
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int pvAttribute, int cbAttribute);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;

        public MainWindow()
        {
            InitializeComponent();
            SourceInitialized += MainWindow_SourceInitialized;
            InitializeWebView();
        }

        private void MainWindow_SourceInitialized(object? sender, EventArgs e)
        {
            try
            {
                var helper = new WindowInteropHelper(this);
                var hwnd = helper.Handle;

                int trueValue = 1;
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref trueValue, sizeof(int));

                int backdropType = 3;
                int result = DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref backdropType, sizeof(int));
                if (result == 0)
                {
                    this.Background = System.Windows.Media.Brushes.Transparent;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Failed to set DWM attributes: " + ex.Message);
            }
        }

        private async void InitializeWebView()
        {
            try
            {
                await webView.EnsureCoreWebView2Async(null);

                string shimCode = @"
                    window.webkit = {
                        messageHandlers: {
                            rpc: {
                                postMessage: function(msg) {
                                    window.chrome.webview.postMessage(msg);
                                }
                            }
                        }
                    };
                    window.get_os = function() {
                        return Promise.resolve('windows');
                    };
                ";
                await webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(shimCode);

                webView.WebMessageReceived += WebView_WebMessageReceived;

                string exeDir = AppDomain.CurrentDomain.BaseDirectory;
                string currentDir = Directory.GetCurrentDirectory();
                string[] lookupPaths = new string[]
                {
                    Path.Combine(exeDir, "src", "index.html"),
                    Path.Combine(exeDir, "index.html"),
                    Path.Combine(currentDir, "src", "index.html"),
                    Path.Combine(currentDir, "index.html")
                };

                string htmlPath = "";
                foreach (var path in lookupPaths)
                {
                    if (File.Exists(path))
                    {
                        htmlPath = path;
                        break;
                    }
                }

                if (!string.IsNullOrEmpty(htmlPath))
                {
                    webView.Source = new Uri(htmlPath);
                }
                else
                {
                    string errorHTML = @"
                        <html>
                            <body style='background:#0f172a; color:#f3f4f6; font-family:sans-serif; text-align:center; padding:100px;'>
                                <h1>Tiwut Launcher Error</h1>
                                <p>Could not locate the frontend file: 'src/index.html'</p>
                            </body>
                        </html>";
                    webView.NavigateToString(errorHTML);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to initialize WebView2: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void WebView_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                string message = e.WebMessageAsJson;
                using (var doc = JsonDocument.Parse(message))
                {
                    var root = doc.RootElement;
                    string seq = root.GetProperty("seq").GetString() ?? "";
                    string functionName = root.GetProperty("functionName").GetString() ?? "";
                    string req = root.GetProperty("req").GetString() ?? "[]";

                    await HandleRPCCall(seq, functionName, req);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error parsing web message: " + ex.Message);
            }
        }

        private async Task HandleRPCCall(string seq, string functionName, string reqJSON)
        {
            try
            {
                using (var reqDoc = JsonDocument.Parse(reqJSON))
                {
                    var args = reqDoc.RootElement;

                    switch (functionName)
                    {
                        case "get_home_dir":
                            GetHomeDir(seq);
                            break;
                        case "save_config":
                            SaveConfig(seq, args);
                            break;
                        case "get_config":
                            GetConfig(seq, args);
                            break;
                        case "get_installed_apps":
                            GetInstalledApps(seq);
                            break;
                        case "detect_installed_app":
                            DetectInstalledApp(seq, args);
                            break;
                        case "download_file":
                            await Task.Run(() => DownloadFile(seq, args));
                            break;
                        case "build_repo":
                            await Task.Run(() => BuildRepo(seq, args));
                            break;
                        case "launch_app":
                            LaunchApp(seq, args);
                            break;
                        case "uninstall_app":
                            await Task.Run(() => UninstallApp(seq, args));
                            break;
                        case "reset_launcher_cache":
                            ResetLauncherCache(seq);
                            break;
                        case "create_desktop_shortcut":
                            CreateDesktopShortcut(seq, args);
                            break;
                        case "create_start_menu_shortcut":
                            CreateStartMenuShortcut(seq, args);
                            break;
                        case "open_url":
                            OpenUrl(seq, args);
                            break;
                        default:
                            Resolve(seq, 1, "Function not found");
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Resolve(seq, 1, ex.Message);
            }
        }

        private string GetLauncherDir()
        {
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TiwutLauncher");
            Directory.CreateDirectory(path);
            return path;
        }

        private void Resolve(string seq, int status, string result)
        {
            string js = $"window.rpc_resolve('{seq}', {status}, '{EscapeJS(result)}')";
            EvalJS(js);
        }

        private void EvalJS(string js)
        {
            Dispatcher.Invoke(async () =>
            {
                try
                {
                    await webView.CoreWebView2.ExecuteScriptAsync(js);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error executing JS: {ex.Message}");
                }
            });
        }

        private void LogToConsole(string message, string type = "system")
        {
            string js = $"window.onBuildLog('[{type.ToUpper()}] {EscapeJS(message)}')";
            EvalJS(js);
        }

        private string EscapeJS(string val)
        {
            return val.Replace("\\", "\\\\")
                      .Replace("\"", "\\\"")
                      .Replace("'", "\\'")
                      .Replace("\n", "\\n")
                      .Replace("\r", "\\r");
        }

        private void GetHomeDir(string seq)
        {
            string homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile).Replace("\\", "/");
            Resolve(seq, 0, homeDir);
        }

        private void SaveConfig(string seq, JsonElement args)
        {
            if (args.GetArrayLength() < 2)
            {
                Resolve(seq, 1, "Invalid parameters");
                return;
            }
            string key = args[0].GetString() ?? "";
            string val = args[1].GetString() ?? "";

            string filePath = Path.Combine(GetLauncherDir(), $"{key}.txt");
            try
            {
                File.WriteAllText(filePath, val);
                Resolve(seq, 0, "Success");
            }
            catch (Exception ex)
            {
                Resolve(seq, 1, "Failed to write config file: " + ex.Message);
            }
        }

        private void GetConfig(string seq, JsonElement args)
        {
            if (args.GetArrayLength() < 1)
            {
                Resolve(seq, 0, "");
                return;
            }
            string key = args[0].GetString() ?? "";
            string filePath = Path.Combine(GetLauncherDir(), $"{key}.txt");

            try
            {
                if (File.Exists(filePath))
                {
                    string content = File.ReadAllText(filePath);
                    Resolve(seq, 0, content);
                }
                else
                {
                    Resolve(seq, 0, "");
                }
            }
            catch
            {
                Resolve(seq, 0, "");
            }
        }

        private void GetInstalledApps(string seq)
        {
            string filePath = Path.Combine(GetLauncherDir(), "installed.txt");
            try
            {
                if (File.Exists(filePath))
                {
                    string content = File.ReadAllText(filePath);
                    Resolve(seq, 0, content);
                }
                else
                {
                    Resolve(seq, 0, "");
                }
            }
            catch
            {
                Resolve(seq, 0, "");
            }
        }

        private void DetectInstalledApp(string seq, JsonElement args)
        {
            if (args.GetArrayLength() < 1)
            {
                Resolve(seq, 1, "Missing app name");
                return;
            }
            string repoName = args[0].GetString() ?? "";
            string localDir = Path.Combine(GetLauncherDir(), "apps", repoName);

            string binaryPath = ScanForExecutables(localDir);
            Resolve(seq, 0, binaryPath);
        }

        private string ScanForExecutables(string dirPath)
        {
            if (!Directory.Exists(dirPath)) return "";

            string mainExe = Path.Combine(dirPath, "main.exe");
            if (File.Exists(mainExe))
            {
                return mainExe.Replace("\\", "/");
            }

            var files = Directory.GetFiles(dirPath, "*.exe");
            foreach (var file in files)
            {
                if (!file.Contains("CMakeFiles"))
                {
                    return file.Replace("\\", "/");
                }
            }

            var subdirs = Directory.GetDirectories(dirPath);
            foreach (var subdir in subdirs)
            {
                string res = ScanForExecutables(subdir);
                if (!string.IsNullOrEmpty(res)) return res;
            }

            return "";
        }

        private async Task DownloadFile(string seq, JsonElement args)
        {
            if (args.GetArrayLength() < 3)
            {
                Resolve(seq, 1, "Invalid download arguments");
                return;
            }
            string downloadUrl = args[0].GetString() ?? "";
            string downloadFileName = args[1].GetString() ?? "";
            string downloadRepoName = args[2].GetString() ?? "";

            string targetDir = Path.Combine(GetLauncherDir(), "apps", downloadRepoName);
            Directory.CreateDirectory(targetDir);
            string downloadTargetFilePath = Path.Combine(targetDir, downloadFileName);

            LogToConsole($"Starting download from {downloadUrl}...", "system");

            string tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + "_" + downloadFileName);
            bool downloadCompleted = false;

            try
            {
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
                    
                    using (var response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead))
                    {
                        response.EnsureSuccessStatusCode();
                        var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                        
                        using (var contentStream = await response.Content.ReadAsStreamAsync())
                        using (var fileStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                        {
                            var buffer = new byte[8192];
                            long totalRead = 0;
                            int read;
                            while ((read = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                            {
                                await fileStream.WriteAsync(buffer, 0, read);
                                totalRead += read;
                                if (totalBytes > 0)
                                {
                                    int percent = (int)((totalRead * 100) / totalBytes);
                                    EvalJS($"window.onDownloadProgress({percent}, {totalRead}, {totalBytes})");
                                }
                            }
                        }
                    }
                }
                downloadCompleted = true;
            }
            catch (Exception ex)
            {
                LogToConsole($"Download failed: {ex.Message}", "error");
                EvalJS($"window.onDownloadFailed('{downloadRepoName}', '{EscapeJS(ex.Message)}')");
                Resolve(seq, 1, "Download failed");
                if (File.Exists(tempFile)) File.Delete(tempFile);
                return;
            }

            if (downloadCompleted)
            {
                LogToConsole("Download completed successfully! Processing package...", "system");
                try
                {
                    string finalPath = "";
                    bool isZip = downloadFileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) || IsZipFile(tempFile);
                    if (isZip)
                    {
                        LogToConsole("ZIP archive detected. Commencing extraction...", "system");
                        
                        ZipFile.ExtractToDirectory(tempFile, targetDir, overwriteFiles: true);
                        File.Delete(tempFile);

                        finalPath = ScanForExecutables(targetDir);
                    }
                    else
                    {
                        if (File.Exists(downloadTargetFilePath))
                        {
                            File.Delete(downloadTargetFilePath);
                        }
                        Directory.CreateDirectory(Path.GetDirectoryName(downloadTargetFilePath)!);
                        File.Move(tempFile, downloadTargetFilePath);
                        finalPath = downloadTargetFilePath.Replace("\\", "/");
                    }

                    if (!string.IsNullOrEmpty(finalPath))
                    {
                        LogToConsole($"App installed successfully. Binary found: {finalPath}", "system");
                        EvalJS($"window.onDownloadComplete('{downloadRepoName}', '{EscapeJS(finalPath)}', '{EscapeJS(downloadFileName)}')");
                        Resolve(seq, 0, "Download completed");
                    }
                    else
                    {
                        LogToConsole("Failed to locate executable in downloaded package.", "error");
                        EvalJS($"window.onDownloadFailed('{downloadRepoName}', 'No executable found in package.')");
                        Resolve(seq, 1, "No executable found");
                    }
                }
                catch (Exception ex)
                {
                    LogToConsole($"Package extraction/processing failed: {ex.Message}", "error");
                    EvalJS($"window.onDownloadFailed('{downloadRepoName}', '{EscapeJS(ex.Message)}')");
                    Resolve(seq, 1, "Extraction failed: " + ex.Message);
                }
            }
        }

        private async Task BuildRepo(string seq, JsonElement args)
        {
            if (args.GetArrayLength() < 2)
            {
                Resolve(seq, 1, "Invalid build arguments");
                return;
            }
            string buildCloneUrl = args[0].GetString() ?? "";
            string buildRepoName = args[1].GetString() ?? "";

            string sourcesDir = Path.Combine(GetLauncherDir(), "sources", buildRepoName);
            string appsDir = Path.Combine(GetLauncherDir(), "apps", buildRepoName);

            try
            {
                if (Directory.Exists(sourcesDir)) Directory.Delete(sourcesDir, true);
                if (Directory.Exists(appsDir)) Directory.Delete(appsDir, true);
                Directory.CreateDirectory(sourcesDir);
                Directory.CreateDirectory(appsDir);
            }
            catch (Exception ex)
            {
                FailBuild(buildRepoName, seq, "Failed to clean/create build directories: " + ex.Message);
                return;
            }

            LogToConsole($"Cloning repository: {buildCloneUrl}...", "system");

            ProcessStartInfo gitPsi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = $"clone \"{buildCloneUrl}\" \"{sourcesDir}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            bool cloneSuccess = false;
            try
            {
                using (var process = new Process { StartInfo = gitPsi })
                {
                    process.Start();
                    await process.WaitForExitAsync();
                    cloneSuccess = (process.ExitCode == 0);
                }
            }
            catch
            {
                cloneSuccess = false;
            }

            if (cloneSuccess)
            {
                LogToConsole("Repository cloned successfully.", "system");
                await StartNextBuildStep(buildRepoName, sourcesDir, seq);
            }
            else
            {
                await RunGitFallbackZip(buildRepoName, sourcesDir, seq);
            }
        }

        private async Task RunGitFallbackZip(string repoName, string sourcesDir, string buildSeq)
        {
            LogToConsole("Git clone failed. Trying Way 2: Downloading source ZIP archive...", "warning");
            
            string zipUrl = $"https://github.com/{repoName}/archive/refs/heads/main.zip";
            string parentSourcesDir = Path.Combine(GetLauncherDir(), "sources");
            string zipPath = Path.Combine(parentSourcesDir, $"{repoName.Replace("/", "_")}_source.zip");
            
            LogToConsole($"Downloading zip from: {zipUrl}", "system");
            
            bool downloadSuccess = false;
            try
            {
                await DownloadUrlToFile(zipUrl, zipPath);
                downloadSuccess = true;
            }
            catch
            {
                LogToConsole("Main branch ZIP failed. Trying master branch ZIP...", "warning");
                zipUrl = $"https://github.com/{repoName}/archive/refs/heads/master.zip";
                try
                {
                    await DownloadUrlToFile(zipUrl, zipPath);
                    downloadSuccess = true;
                }
                catch (Exception ex)
                {
                    FailBuild(repoName, buildSeq, "Could not locate master or main branch source code ZIP archive: " + ex.Message);
                    return;
                }
            }
            
            if (downloadSuccess)
            {
                LogToConsole("Source ZIP downloaded. Commencing extraction...", "system");
                try
                {
                    if (Directory.Exists(sourcesDir))
                    {
                        Directory.Delete(sourcesDir, true);
                    }
                    
                    ZipFile.ExtractToDirectory(zipPath, parentSourcesDir);
                    File.Delete(zipPath);
                    
                    string shortRepo = repoName.Split('/').Last();
                    var extractedDirs = Directory.GetDirectories(parentSourcesDir);
                    bool foundExtracted = false;
                    foreach (var dir in extractedDirs)
                    {
                        string dirName = Path.GetFileName(dir);
                        if (dirName.StartsWith(shortRepo, StringComparison.OrdinalIgnoreCase) && dirName != shortRepo)
                        {
                            if (Directory.Exists(sourcesDir)) Directory.Delete(sourcesDir, true);
                            Directory.Move(dir, sourcesDir);
                            foundExtracted = true;
                            break;
                        }
                    }

                    if (!foundExtracted)
                    {
                        LogToConsole("Source ZIP extracted, but could not match folder name.", "warning");
                    }
                    
                    LogToConsole("Source extraction completed successfully.", "system");
                    await StartNextBuildStep(repoName, sourcesDir, buildSeq);
                }
                catch (Exception ex)
                {
                    FailBuild(repoName, buildSeq, "Extraction of zip archive failed: " + ex.Message);
                }
            }
        }

        private async Task DownloadUrlToFile(string url, string filePath)
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
                var response = await client.GetAsync(url);
                response.EnsureSuccessStatusCode();
                using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await response.Content.CopyToAsync(fs);
                }
            }
        }

        private async Task StartNextBuildStep(string repoName, string sourcesDir, string buildSeq)
        {
            string buildCmd = Path.Combine(sourcesDir, "build.cmd");
            
            if (!File.Exists(buildCmd))
            {
                var files = Directory.GetFiles(sourcesDir, "build.cmd", SearchOption.AllDirectories);
                if (files.Length > 0)
                {
                    buildCmd = files[0];
                }
            }

            if (File.Exists(buildCmd))
            {
                LogToConsole("build.cmd script detected! Compiling via script...", "system");
                await RunScriptBuild(repoName, buildCmd, sourcesDir, buildSeq);
            }
            else
            {
                FailBuild(repoName, buildSeq, "This repository does not support compilation (missing build.cmd).");
            }
        }

        private async Task RunScriptBuild(string repoName, string scriptPath, string sourcesDir, string buildSeq)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c \"{scriptPath}\"",
                    WorkingDirectory = Path.GetDirectoryName(scriptPath),
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                
                using (var process = new Process { StartInfo = psi })
                {
                    process.OutputDataReceived += (sender, e) =>
                    {
                        if (e.Data != null) LogToConsole(e.Data, "stdout");
                    };
                    process.ErrorDataReceived += (sender, e) =>
                    {
                        if (e.Data != null) LogToConsole(e.Data, "stderr");
                    };
                    
                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    
                    await process.WaitForExitAsync();
                    
                    if (process.ExitCode == 0)
                    {
                        LogToConsole("Build script execution completed successfully.", "system");
                        await FinalizeBuild(repoName, sourcesDir, buildSeq);
                    }
                    else
                    {
                        FailBuild(repoName, buildSeq, $"Build script failed with exit code: {process.ExitCode}");
                    }
                }
            }
            catch (Exception ex)
            {
                FailBuild(repoName, buildSeq, "Failed to execute build script: " + ex.Message);
            }
        }

        private Task FinalizeBuild(string repoName, string sourcesDir, string buildSeq)
        {
            LogToConsole("Locating compiled executable binary...", "system");
            string binaryPath = ScanForExecutables(sourcesDir);
            
            if (string.IsNullOrEmpty(binaryPath))
            {
                FailBuild(repoName, buildSeq, "Could not locate any compiled binary executable (.exe)!");
                return Task.CompletedTask;
            }
            
            try
            {
                string appsDir = Path.Combine(GetLauncherDir(), "apps", repoName);
                Directory.CreateDirectory(appsDir);
                
                string fileName = Path.GetFileName(binaryPath);
                string destFilePath = Path.Combine(appsDir, fileName).Replace("\\", "/");
                
                if (File.Exists(destFilePath))
                {
                    File.Delete(destFilePath);
                }
                
                File.Copy(binaryPath, destFilePath, true);
                
                LogToConsole($"App compiled and installed successfully to: {destFilePath}", "system");
                
                string js = $"window.onBuildComplete('{repoName}', '{EscapeJS(destFilePath)}')";
                EvalJS(js);
                Resolve(buildSeq, 0, "Build completed");
            }
            catch (Exception ex)
            {
                FailBuild(repoName, buildSeq, "Failed to copy compiled binary to local apps directory: " + ex.Message);
            }
            return Task.CompletedTask;
        }

        private void FailBuild(string repoName, string buildSeq, string errorMessage)
        {
            LogToConsole($"Build failed: {errorMessage}", "error");
            string js = $"window.onBuildFailed('{repoName}', '{EscapeJS(errorMessage)}')";
            EvalJS(js);
            Resolve(buildSeq, 1, "Build failed");
        }

        private void LaunchApp(string seq, JsonElement args)
        {
            if (args.GetArrayLength() < 1)
            {
                Resolve(seq, 1, "Missing launch path");
                return;
            }
            string path = args[0].GetString() ?? "";
            bool runAsAdmin = false;
            if (args.GetArrayLength() >= 2)
            {
                runAsAdmin = args[1].GetBoolean();
            }

            LogToConsole($"Launching process detached (runAsAdmin={runAsAdmin}): {path}", "system");

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true,
                    WorkingDirectory = Path.GetDirectoryName(path)
                };
                if (runAsAdmin)
                {
                    psi.Verb = "runas";
                }
                Process.Start(psi);
                Resolve(seq, 0, "Launched successfully");
            }
            catch (Exception ex)
            {
                LogToConsole($"Launch failed: {ex.Message}", "error");
                Resolve(seq, 1, "Launch failed: " + ex.Message);
            }
        }

        private void CreateDesktopShortcut(string seq, JsonElement args)
        {
            if (args.GetArrayLength() < 2)
            {
                Resolve(seq, 1, "Invalid parameters");
                return;
            }
            string repoName = args[0].GetString() ?? "";
            string binaryPath = args[1].GetString() ?? "";
            bool runAsAdmin = false;
            if (args.GetArrayLength() >= 3)
            {
                runAsAdmin = args[2].GetBoolean();
            }

            try
            {
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string cleanName = repoName.Split('/').Last();
                string shortcutPath = Path.Combine(desktopPath, $"{cleanName}.lnk");

                CreateShortcutInternal(shortcutPath, binaryPath, runAsAdmin);
                Resolve(seq, 0, "Shortcut created on Desktop");
            }
            catch (Exception ex)
            {
                Resolve(seq, 1, "Failed to create desktop shortcut: " + ex.Message);
            }
        }

        private void CreateStartMenuShortcut(string seq, JsonElement args)
        {
            if (args.GetArrayLength() < 2)
            {
                Resolve(seq, 1, "Invalid parameters");
                return;
            }
            string repoName = args[0].GetString() ?? "";
            string binaryPath = args[1].GetString() ?? "";
            bool runAsAdmin = false;
            if (args.GetArrayLength() >= 3)
            {
                runAsAdmin = args[2].GetBoolean();
            }

            try
            {
                string startMenuPath = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
                string cleanName = repoName.Split('/').Last();
                string shortcutPath = Path.Combine(startMenuPath, $"{cleanName}.lnk");

                CreateShortcutInternal(shortcutPath, binaryPath, runAsAdmin);
                Resolve(seq, 0, "Shortcut created in Start Menu");
            }
            catch (Exception ex)
            {
                Resolve(seq, 1, "Failed to create Start Menu shortcut: " + ex.Message);
            }
        }

        private void CreateShortcutInternal(string shortcutPath, string binaryPath, bool runAsAdmin)
        {
            string targetPath = binaryPath.Replace("/", "\\");

            Type? t = Type.GetTypeFromProgID("Wscript.Shell");
            if (t == null) throw new Exception("Wscript.Shell type not found");

            object? shell = Activator.CreateInstance(t);
            if (shell == null) throw new Exception("Could not create Wscript.Shell instance");

            dynamic dynamicShell = shell;
            var shortcut = dynamicShell.CreateShortcut(shortcutPath);
            shortcut.TargetPath = targetPath;
            shortcut.WorkingDirectory = Path.GetDirectoryName(targetPath);
            shortcut.IconLocation = targetPath;
            shortcut.Save();

            if (runAsAdmin)
            {
                using (var fs = new FileStream(shortcutPath, FileMode.Open, FileAccess.ReadWrite))
                {
                    fs.Seek(21, SeekOrigin.Begin);
                    int b = fs.ReadByte();
                    if (b != -1)
                    {
                        fs.Seek(21, SeekOrigin.Begin);
                        fs.WriteByte((byte)(b | 0x20));
                    }
                }
            }
        }

        private void OpenUrl(string seq, JsonElement args)
        {
            if (args.GetArrayLength() < 1)
            {
                Resolve(seq, 1, "Missing URL");
                return;
            }
            string url = args[0].GetString() ?? "";
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
                Resolve(seq, 0, "URL opened");
            }
            catch (Exception ex)
            {
                Resolve(seq, 1, "Failed to open URL: " + ex.Message);
            }
        }

        private void UninstallApp(string seq, JsonElement args)
        {
            if (args.GetArrayLength() < 2)
            {
                Resolve(seq, 1, "Invalid uninstall parameters");
                return;
            }
            string repoName = args[0].GetString() ?? "";
            string binaryPath = args[1].GetString() ?? "";

            LogToConsole($"Terminating running instances of {repoName}...", "system");

            if (!string.IsNullOrEmpty(binaryPath))
            {
                try
                {
                    string processName = Path.GetFileNameWithoutExtension(binaryPath);
                    foreach (var proc in Process.GetProcessesByName(processName))
                    {
                        try { proc.Kill(); } catch { }
                    }
                }
                catch { }
            }

            try
            {
                string repoProcessName = repoName.Split('/').Last();
                foreach (var proc in Process.GetProcessesByName(repoProcessName))
                {
                    try { proc.Kill(); } catch { }
                }
            }
            catch { }

            LogToConsole("Removing binaries and source caches...", "system");

            try
            {
                string appsDir = Path.Combine(GetLauncherDir(), "apps", repoName);
                string sourcesDir = Path.Combine(GetLauncherDir(), "sources", repoName);

                if (Directory.Exists(appsDir)) Directory.Delete(appsDir, true);
                if (Directory.Exists(sourcesDir)) Directory.Delete(sourcesDir, true);

                Resolve(seq, 0, "Successfully uninstalled completely.");
            }
            catch (Exception ex)
            {
                LogToConsole("Error during uninstall file cleanup: " + ex.Message, "warning");
                Resolve(seq, 0, "Uninstalled (with cleanup warnings: " + ex.Message + ")");
            }
        }

        private void ResetLauncherCache(string seq)
        {
            LogToConsole("Resetting launcher application database and directory caches...", "system");

            try
            {
                string launcherDir = GetLauncherDir();
                string appsDir = Path.Combine(launcherDir, "apps");
                string sourcesDir = Path.Combine(launcherDir, "sources");
                string installedFile = Path.Combine(launcherDir, "installed.txt");
                string cachedRegistryFile = Path.Combine(launcherDir, "cached_registry.txt");

                if (Directory.Exists(appsDir)) Directory.Delete(appsDir, true);
                if (Directory.Exists(sourcesDir)) Directory.Delete(sourcesDir, true);
                if (File.Exists(installedFile)) File.Delete(installedFile);
                if (File.Exists(cachedRegistryFile)) File.Delete(cachedRegistryFile);

                Resolve(seq, 0, "Launcher cache and applications database reset successfully.");
            }
            catch (Exception ex)
            {
                Resolve(seq, 1, "Reset cache failed: " + ex.Message);
            }
        }

        private bool IsZipFile(string filePath)
        {
            try
            {
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    if (fs.Length < 4) return false;
                    byte[] signature = new byte[4];
                    fs.Read(signature, 0, 4);
                    return signature[0] == 0x50 && signature[1] == 0x4B;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
