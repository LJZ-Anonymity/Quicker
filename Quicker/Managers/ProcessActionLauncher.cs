using Quicker.Windows.MainWindows.MainWindow;
using System.Runtime.InteropServices;
using Quicker.Contracts.Launching;
using File = System.IO.File;
using System.Diagnostics;
using IWshRuntimeLibrary;
using Microsoft.Win32;
using Quicker.Helpers;
using System.IO;

namespace Quicker.Managers
{
    public sealed class ProcessActionLauncher : IActionLauncher
    {
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr ShellExecute(
            IntPtr hwnd, string lpOperation, string lpFile,
            string lpParameters, string lpDirectory, int nShowCmd);

        public Task<LaunchResult> LaunchAsync(LaunchRequest request, CancellationToken ct = default)
        {
            return Task.Run(() =>
            {
                try
                {
                    return request.Kind switch
                    {
                        LaunchKind.OpenFile => OpenFile(request),
                        LaunchKind.OpenWebsite => OpenWebsite(request),
                        LaunchKind.OpenUri => OpenUri(request),
                        LaunchKind.OpenUwpApp => OpenUwpApp(request),
                        _ => LaunchResult.Fail($"未知的启动类型：{request.Kind}")
                    };
                }
                catch (Exception ex)
                {
                    return LaunchResult.Fail(ex.Message);
                }
            }, ct);
        }

        private LaunchResult OpenFile(LaunchRequest req)
        {
            AppPathHelper.EnsureDirectoryExists(AppPathHelper.TempDataFolder);
            ParseCommandLine(req.Location, out string actualFileName, out _);

            if (req.TryActivateExistingWindow)
            {
                string windowTitle = Path.GetFileNameWithoutExtension(actualFileName);
                WindowHelper.TryToOpenExitingWindow(windowTitle);
            }

            string ext = Path.GetExtension(actualFileName);
            if (ext.Equals(".lnk", StringComparison.OrdinalIgnoreCase) || ext.Equals(".url", StringComparison.OrdinalIgnoreCase))
            {
                return ProcessShortcutFile(req);
            }

            string dir = Path.GetDirectoryName(actualFileName);
            RunShellCommand(req.Location, req, dir ?? AppPathHelper.TempDataFolder);
            return LaunchResult.Ok();
        }

        private LaunchResult ProcessShortcutFile(LaunchRequest req)
        {
            string targetPath = req.Location;
            if (Path.GetExtension(req.Location).Equals(".lnk", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    string realTarget = GetShortcutTargetPath(req.Location);
                    if (!string.IsNullOrWhiteSpace(realTarget)) targetPath = realTarget;
                }
                catch { }
            }

            string dir = Path.GetDirectoryName(req.Location);
            RunShellCommand(targetPath, req, dir ?? AppPathHelper.TempDataFolder);
            return LaunchResult.Ok();
        }

        private LaunchResult OpenWebsite(LaunchRequest req)
        {
            string website = req.Location;
            switch (req.WebsiteMode)
            {
                case 0: LaunchDefaultBrowser(website); break;
                case 1: LaunchInternetExplorer(website); break;
                case 2: LaunchMicrosoftEdge(website); break;
                case 3: LaunchEdgeAppMode(website); break;
                case 4: LaunchEdgeInPrivateMode(website); break;
                case 5: LaunchChrome(website); break;
                case 6: LaunchChromeAppMode(website); break;
                case 7: LaunchChromeIncognitoMode(website); break;
                case 8: return LaunchCustomBrowser(website);
                default: return LaunchResult.Fail($"未知的网站打开方式：{req.WebsiteMode}");
            }
            return LaunchResult.Ok();
        }

        private LaunchResult OpenUri(LaunchRequest req)
        {
            string command = req.Location?.Trim().Trim('"');
            if (!string.IsNullOrWhiteSpace(command))
            {
                RunShellCommand(command, req);
            }
            return LaunchResult.Ok();
        }

        private LaunchResult OpenUwpApp(LaunchRequest req)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"shell:AppsFolder\\{req.Location}",
                UseShellExecute = true
            });
            return LaunchResult.Ok();
        }

        private void RunShellCommand(string fileName, LaunchRequest req, string? workingDirectory = null)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return;

            ParseCommandLine(fileName, out string actualFileName, out string arguments);
            string operation = req.RunAsAdmin ? "runas" : "open";
            int nShowCmd = req.WindowStyle switch
            {
                WindowShowStyle.Minimized => 7,
                WindowShowStyle.Maximized => 3,
                _ => 1
            };

            IntPtr result = ShellExecute(IntPtr.Zero, operation, actualFileName, arguments, workingDirectory, nShowCmd);

            if ((int)result <= 32)
            {
                var psi = new ProcessStartInfo
                {
                    FileName = actualFileName,
                    Arguments = arguments ?? string.Empty,
                    Verb = operation,
                    UseShellExecute = true,
                    WindowStyle = req.WindowStyle switch
                    {
                        WindowShowStyle.Minimized => ProcessWindowStyle.Minimized,
                        WindowShowStyle.Maximized => ProcessWindowStyle.Maximized,
                        _ => ProcessWindowStyle.Normal
                    }
                };
                if (!string.IsNullOrWhiteSpace(workingDirectory)) psi.WorkingDirectory = workingDirectory;
                Process.Start(psi);
            }
        }

        private static void ParseCommandLine(string commandLine, out string fileName, out string? arguments)
        {
            fileName = commandLine;
            arguments = null;
            if (string.IsNullOrWhiteSpace(commandLine)) return;
            commandLine = commandLine.Trim();

            if (commandLine.StartsWith("\""))
            {
                int endQuote = commandLine.IndexOf('"', 1);
                if (endQuote > 0)
                {
                    fileName = commandLine.Substring(1, endQuote - 1);
                    if (commandLine.Length > endQuote + 1) arguments = commandLine.Substring(endQuote + 1).Trim();
                    return;
                }
            }

            if (File.Exists(commandLine)) { fileName = commandLine; return; }

            int exeIndex = commandLine.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
            if (exeIndex > 0)
            {
                int split = exeIndex + 4;
                if (commandLine.Length > split && commandLine[split] == ' ')
                {
                    fileName = commandLine.Substring(0, split);
                    arguments = commandLine.Substring(split + 1).Trim();
                    return;
                }
            }

            int spaceIndex = commandLine.IndexOf(' ');
            if (spaceIndex > 0)
            {
                fileName = commandLine.Substring(0, spaceIndex);
                arguments = commandLine.Substring(spaceIndex + 1).Trim();
            }
        }

        private static string GetShortcutTargetPath(string shortcutFilePath)
        {
            WshShell shell = new WshShell();
            IWshShortcut shortcut = (IWshShortcut)shell.CreateShortcut(shortcutFilePath);
            string target = shortcut.TargetPath;
            string args = shortcut.Arguments;
            Marshal.ReleaseComObject(shortcut);
            Marshal.ReleaseComObject(shell);

            if (string.IsNullOrWhiteSpace(target)) return string.Empty;
            string quoted = target.Contains(" ") && !target.StartsWith("\"") ? $"\"{target}\"" : target;
            return string.IsNullOrWhiteSpace(args) ? quoted : $"{quoted} {args.Trim()}";
        }

        private void LaunchDefaultBrowser(string website) => Process.Start(new ProcessStartInfo { FileName = website, UseShellExecute = true });

        private void LaunchInternetExplorer(string website)
        {
            string iePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Internet Explorer\iexplore.exe");
            if (File.Exists(iePath)) Process.Start(new ProcessStartInfo { FileName = iePath, Arguments = website, UseShellExecute = true });
            else LaunchDefaultBrowser(website);
        }

        private void LaunchMicrosoftEdge(string website) => Process.Start("microsoft-edge:" + website);

        private void LaunchEdgeAppMode(string website) => Process.Start(new ProcessStartInfo { FileName = GetEdgeBrowserPath(), Arguments = $"--app={website}", UseShellExecute = true });

        private void LaunchEdgeInPrivateMode(string website) => Process.Start(new ProcessStartInfo { FileName = GetEdgeBrowserPath(), Arguments = $"--inprivate {website}", UseShellExecute = true });

        private void LaunchChrome(string website) => Process.Start(new ProcessStartInfo { FileName = GetChromeBrowserPath(), Arguments = website, UseShellExecute = true });

        private void LaunchChromeAppMode(string website) => Process.Start(new ProcessStartInfo { FileName = GetChromeBrowserPath(), Arguments = $"--app={website}", UseShellExecute = true });

        private void LaunchChromeIncognitoMode(string website) => Process.Start(new ProcessStartInfo { FileName = GetChromeBrowserPath(), Arguments = $"-incognito {website}", UseShellExecute = true });

        private LaunchResult LaunchCustomBrowser(string location)
        {
            if (string.IsNullOrWhiteSpace(location)) return LaunchResult.Fail("自定义浏览器配置为空");
            string[] parts = location.Split(';');
            if (parts.Length < 2) return LaunchResult.Fail("自定义浏览器配置格式错误：应为 \"网址;浏览器路径\"");

            string website = parts[0];
            string browserPath = parts[1];
            if (!File.Exists(browserPath)) return LaunchResult.Fail($"指定的浏览器不存在：{browserPath}");

            Process.Start(new ProcessStartInfo { FileName = browserPath, Arguments = website, UseShellExecute = true });
            return LaunchResult.Ok();
        }

        private static string GetEdgeBrowserPath()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\msedge.exe");
                if (key?.GetValue("") is string fromReg && File.Exists(fromReg)) return fromReg;
            }
            catch { }

            string[] candidates =
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Microsoft\Edge\Application\msedge.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Microsoft\Edge\Application\msedge.exe"),
            };
            foreach (var p in candidates) if (File.Exists(p)) return p;
            return "msedge.exe";
        }

        private static string GetChromeBrowserPath()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\chrome.exe");
                if (key?.GetValue("") is string fromReg && File.Exists(fromReg)) return fromReg;
            }
            catch { }

            string[] candidates =
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Google\Chrome\Application\chrome.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Google\Chrome\Application\chrome.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Google\Chrome\Application\chrome.exe"),
            };
            foreach (var p in candidates) if (File.Exists(p)) return p;
            return "chrome.exe";
        }
    }
}