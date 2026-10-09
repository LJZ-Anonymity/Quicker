using Quicker.Windows.MainWindows.MainWindow;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Quicker.Database.Core;
using IWshRuntimeLibrary;
using System.Diagnostics;
using Quicker.Database;
using Microsoft.Win32;
using Quicker.Helpers;
using Quicker.Extend;
using Quicker.Models;
using System.Windows;
using System.IO;

namespace Quicker.Managers
{
    public class ActionManager : IDisposable
    {
        private bool isDisposed = false; // 是否释放

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr ShellExecute(IntPtr hwnd, string lpOperation, string lpFile, string lpParameters, string lpDirectory, int nShowCmd);

        /// <summary>
        /// 打开文件
        /// </summary>
        /// <param name="data"> 按钮数据 </param>
        public void OpenFile(ButtonData data)
        {
            AppPathHelper.EnsureDirectoryExists(AppPathHelper.TempDataFolder); // 确保临时目录存在
            if (data.Data2 == "true") // 如果尝试打开已存在的窗口
            {
                string windowTitle = System.IO.Path.GetFileNameWithoutExtension(data.Location);
                using var windowManager = new WindowManager(); // 创建窗口管理器
                windowManager.TryToOpenExitingWindow(windowTitle);
            }

            if (Path.GetExtension(data.Location).Equals(".lnk", StringComparison.OrdinalIgnoreCase) ||
                Path.GetExtension(data.Location).Equals(".url", StringComparison.OrdinalIgnoreCase))
            {
                ProcessShortcutFile(data); // 处理快捷方式文件
            }
            else
            {
                ProcessNormalFile(data); // 处理普通文件
            }
        }

        /// <summary>
        /// 处理快捷方式文件
        /// </summary>
        /// <param name="data">按钮数据</param>
        private void ProcessShortcutFile(ButtonData data)
        {
            try
            {
                string targetPath = data.Location;

                // 如果是 .lnk 文件，尝试解析其真实目标（可能是 URL 协议）
                if (Path.GetExtension(data.Location).Equals(".lnk", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        string realTarget = GetShortcutTargetPath(data.Location);
                        if (!string.IsNullOrWhiteSpace(realTarget))
                        {
                            targetPath = realTarget;
                        }
                    }
                    catch
                    {
                        // 解析失败就继续用原路径
                    }
                }

                string fileDirectory = Path.GetDirectoryName(data.Location);
                RunShellCommand(targetPath, data, fileDirectory ?? AppPathHelper.TempDataFolder);
            }
            catch (Exception ex)
            {
                ShowToast($"打开文件失败：{ex.Message}", ToastType.Error);
            }
        }

        /// <summary>
        /// 处理普通文件
        /// </summary>
        /// <param name="data">按钮数据</param>
        private void ProcessNormalFile(ButtonData data)
        {
            try
            {
                string fileDirectory = Path.GetDirectoryName(data.Location);
                RunShellCommand(data.Location, data, fileDirectory ?? AppPathHelper.TempDataFolder);
            }
            catch (Exception ex)
            {
                ShowToast($"打开文件失败：{ex.Message}", ToastType.Error);
            }
        }

        /// <summary>
        /// 弹出消息
        /// </summary>
        /// <param name="message"> 消息内容 </param>
        /// <param name="type"> 消息类型 </param>
        public void ShowToast(string message, ToastType type)
        {
            using var toast = new ToastManager(); // 消息提醒管理器
            toast.Show(message, type); // 弹出消息提醒
        }

        /// <summary>
        /// 获取快捷方式的目标路径
        /// </summary>
        /// <param name="shortcutFilePath"> 快捷方式文件路径 </param>
        /// <returns> 目标路径 </returns>
        private string GetShortcutTargetPath(string shortcutFilePath)
        {
            WshShell shell = new WshShell(); // 创建WshShell对象
            IWshShortcut shortcut = (IWshShortcut)shell.CreateShortcut(shortcutFilePath); // 创建快捷方式对象
            return shortcut.TargetPath; // 获取快捷方式的目标路径
        }

        /// <summary>
        /// 用指定方式打开指定网站
        /// </summary>
        /// <param name="data"> 按钮数据 </param>
        public void OpenWebsite(ButtonData data)
        {
            try
            {
                switch (int.Parse(data.Data1))
                {
                    case 0:
                        LaunchDefaultBrowser(data.Location);
                        break; // 打开默认浏览器
                    case 1:
                        LaunchInternetExplorer(data.Location);
                        break; // 打开IE浏览器
                    case 2:
                        LaunchMicrosoftEdge(data.Location);
                        break; // 打开Edge浏览器
                    case 3:
                        LaunchEdgeAppMode(data.Location);
                        break; // 打开Edge浏览器，并以App模式打开
                    case 4:
                        LaunchEdgeInPrivateMode(data.Location);
                        break; // 打开Edge浏览器，并以InPrivate模式打开
                    case 5:
                        LaunchChrome(data.Location);
                        break; // 打开Chrome浏览器
                    case 6:
                        LaunchChromeAppMode(data.Location);
                        break; // 打开Chrome浏览器，并以APP模式打开
                    case 7:
                        LaunchChromeIncognitoMode(data.Location);
                        break; // 打开Chrome浏览器,并以无痕模式打开
                    case 8:
                        LaunchCustomBrowser(data.Location);
                        break; // 通过用户指定浏览器打开
                }
            }
            catch (Exception ex)
            {
                using var toast = new ToastManager(); // 消息提醒管理器
                toast.Show($"打开网站失败：{ex.Message}", ToastType.Error); // 弹出消息提醒
            }
        }

        /// <summary>
        /// 打开默认浏览器
        /// </summary>
        /// <param name="website"> 网站地址 </param>
        public void LaunchDefaultBrowser(string website)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = website,
                UseShellExecute = true
            }); // 启动默认浏览器
        }

        /// <summary>
        /// 打开IE浏览器
        /// </summary>
        /// <param name="website"> 网站地址 </param>
        private void LaunchInternetExplorer(string website)
        {
            Process.Start("ieplore.exe", website); // 启动IE浏览器
        }

        /// <summary>
        /// 打开Microsoft Edge浏览器
        /// </summary>
        /// <param name="website"> 网站地址 </param>
        private void LaunchMicrosoftEdge(string website)
        {
            Process.Start("microsoft-edge:" + website); // 启动Edge浏览器
        }

        /// <summary>
        /// 打开Edge浏览器，并以App模式打开指定网站
        /// </summary>
        /// <param name="website"> 网站地址 </param>
        private void LaunchEdgeAppMode(string website)
        {
            string edgePath = GetEdgeBrowserPath(); // 获取Edge浏览器路径
            Process.Start(new ProcessStartInfo
            {
                FileName = edgePath,
                Arguments = $"--app={website}",
                UseShellExecute = true
            }); // 启动Edge浏览器，并以App模式打开
        }

        /// <summary>
        /// 打开Edge浏览器，并以InPrivate模式打开指定网站
        /// </summary>
        /// <param name="website"> 网站地址 </param>
        private void LaunchEdgeInPrivateMode(string website)
        {
            string edgePath = GetEdgeBrowserPath(); // 获取Edge浏览器路径
            Process.Start(new ProcessStartInfo
            {
                FileName = edgePath,
                Arguments = $"--inprivate {website}",
                UseShellExecute = true
            }); // 启动Edge浏览器，并以InPrivate模式打开
        }

        /// <summary>
        /// 打开Chrome浏览器
        /// </summary>
        /// <param name="website"> 网站地址 </param>
        private void LaunchChrome(string website)
        {
            string chromePath = GetChromeBrowserPath(); // 获取Chrome浏览器路径
            Process.Start(new ProcessStartInfo
            {
                FileName = chromePath,
                Arguments = website,
                UseShellExecute = true
            }); // 启动Chrome浏览器
        }

        /// <summary>
        /// 打开Chrome浏览器，并以App模式打开指定网站
        /// </summary>
        /// <param name="website"> 网站地址 </param>
        private void LaunchChromeAppMode(string website)
        {
            string chromePath = GetChromeBrowserPath(); // 获取Chrome浏览器路径
            Process.Start(new ProcessStartInfo
            {
                FileName = chromePath,
                Arguments = $"--app={website}",
                UseShellExecute = true
            }); // 启动Chrome浏览器，并以App模式打开
        }

        /// <summary>
        /// 打开Chrome浏览器，并以无痕模式打开指定网站
        /// </summary>
        /// <param name="website"> 网站地址 </param>
        private void LaunchChromeIncognitoMode(string website)
        {
            string chromePath = GetChromeBrowserPath(); // 获取Chrome浏览器路径
            Process.Start(new ProcessStartInfo
            {
                FileName = chromePath,
                Arguments = $"-incognito {website}",
                UseShellExecute = true
            }); // 启动Chrome浏览器，并以无痕模式打开
        }

        /// <summary>
        /// 通过用户指定浏览器打开网站
        /// </summary>
        /// <param name="website"> 网站地址 </param>
        private void LaunchCustomBrowser(string location)
        {
            string[] processNames = location.Split(';'); // 将文本内容按照分号分隔
            string website = processNames[0]; // 获取网站地址
            string browserPath = processNames[1]; // 获取浏览器路径
            Process.Start(new ProcessStartInfo
            {
                FileName = browserPath,
                Arguments = website,
                UseShellExecute = true
            }); // 启动用户指定浏览器打开网站
        }

        // 获取Edge浏览器路径
        private string GetEdgeBrowserPath()
        {
            try // 通过注册表获取Edge浏览器路径
            {
                RegistryKey key = Registry.ClassesRoot.OpenSubKey(@"Local Settings\Software\Microsoft\Windows\CurrentVersion\App Model}");

                if (key != null)
                {
                    object value = key.GetValue("Edge"); // 获取Edge浏览器路径
                    if (value != null)
                        return value.ToString(); // 返回Edge浏览器路径
                }
            }
            catch { }
            return @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"; // 如果注册表中找不到，使用默认路径
        }

        // 获取Chrome浏览器路径
        private string GetChromeBrowserPath()
        {
            string chromePath = @"C:\Program Files\Google\Chrome\Application\chrome.exe"; // 默认路径
            if (!System.IO.File.Exists(chromePath))
            {
                chromePath = @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe"; // 32位路径
            }
            return chromePath; // 返回Chrome浏览器路径
        }

        /// <summary>
        /// 打开多个文件
        /// </summary>
        /// <param name="data"> 按钮数据 </param>
        public void OpenFiles(ButtonData data)
        {
            string[] files = data.Location.Split(';'); // 将文本内容按照分号分隔
            foreach (string file in files)
            {
                OpenFile(new ButtonData { Location = file, Data2 = false.ToString() }); // 打开文件
            }
        }

        /// <summary>
        /// 打开UWP应用
        /// </summary>
        /// <param name="data"> 按钮数据 </param>
        public void OpenUwpApp(ButtonData data)
        {
            try
            {
                // 使用shell:AppsFolder协议启动UWP应用
                string appPath = $"shell:AppsFolder\\{data.Location}";
                ShellExecute(IntPtr.Zero, "open", appPath, null, null, 1);
            }
            catch (Exception ex)
            {
                using var toast = new ToastManager(); // 消息提醒管理器
                toast.Show($"打开UWP应用失败：{ex.Message}", ToastType.Error); // 弹出消息提醒
            }
        }

        /// <summary>
        /// 加载扩展
        /// </summary>
        /// <param name="data"> 按钮数据 </param>
        public void LoadExtension(ButtonData data)
        {
            string dllPath = data.Location; // 直接获取DLL文件路径
            if (!System.IO.File.Exists(dllPath)) // 判断DLL文件是否存在
            {
                using var toast = new ToastManager();
                toast.Show($"扩展文件不存在: {dllPath}", ToastType.Error);
                return;
            }
            else
            {
                ModuleLoader moduleLoader = new(); // 创建模块加载器
                moduleLoader.LoadModule(dllPath); // 加载扩展
            }
        }

        /// <summary>
        /// 执行动作
        /// </summary>
        /// <param name="data"> 按钮数据 </param>
        /// /// <param name="tableName"> 数据库表名 </param>
        public async Task DoActionAsync(ButtonData data, string tableName)
        {
            switch (data.ActionType)
            {
                case ActionType.OpenFile:
                    OpenFile(data);
                    break;
                case ActionType.OpenWebsite:
                    OpenWebsite(data);
                    break;
                case ActionType.OpenFiles:
                    OpenFiles(data);
                    break;
                case ActionType.OpenUwpApp:
                    OpenUwpApp(data);
                    break;
                case ActionType.LoadExtension:
                    LoadExtension(data);
                    break;
                case ActionType.OpenActionPage:
                    OpenActionPage(data);
                    break;
                case ActionType.OpenUri:
                    OpenUriCommand(data);
                    break;
                default:
                    ShowToast($"未知的动作类型：{data.ActionType}", ToastType.Error);
                    break;
            }

            // 在后台线程异步更新使用次数，避免阻塞 UI
            _ = Task.Run(() =>
            {
                try
                {
                    using var buttonDatabase = new ButtonDatabase();
                    buttonDatabase.IncreaseActionUsedTimes(data.ButtonID, tableName);
                }
                catch { } // 忽略数据库操作异常，避免影响主流程
            });
        }

        /// <summary>
        /// 打开动作页面
        /// </summary>
        /// <param name="data"> 按钮数据 </param>
        private void OpenActionPage(ButtonData data)
        {
            var mainWindow = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault(); // 获取主窗口
            if (mainWindow == null)
            {
                AppStateManager.CommonState = data.Data1; // 将窗口类型设置到 AppStateManager，以便 MainWindow 构造函数可以获取
                mainWindow = new();
                mainWindow.Show();
            }
            mainWindow.OpenActionPage(data);
        }

        /// <summary>
        /// 执行 URI 协议命令
        /// </summary>
        /// <param name="data">按钮数据</param>
        public void OpenUriCommand(ButtonData data)
        {
            try
            {
                string command = data.Location?.Trim().Trim('"');
                if (string.IsNullOrWhiteSpace(command))
                    return;

                RunShellCommand(command, data); // URI 没有工作目录，传 null
            }
            catch (Exception ex)
            {
                ShowToast($"打开协议命令失败：{ex.Message}", ToastType.Error);
            }
        }

        /// <summary>
        /// 通过 ShellExecute 执行命令，失败时用 Process.Start 兜底
        /// </summary>
        /// <param name="fileName">要执行的文件/路径/URI 命令</param>
        /// <param name="data">按钮数据，用于取 Data1(管理员) 和 Data3(窗口状态)</param>
        /// <param name="workingDirectory">工作目录，可为 null</param>
        private void RunShellCommand(string fileName, ButtonData data, string workingDirectory = null)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return;

            string operation = data.Data1 == "True" ? "runas" : "open";

            int nShowCmd = int.TryParse(data.Data3, out int windowStyle) ? windowStyle switch
            {
                0 => 1, // SW_SHOWNORMAL
                1 => 7, // SW_MINIMIZE
                2 => 3, // SW_MAXIMIZE
                _ => 1
            } : 1;

            IntPtr result = ShellExecute(
                IntPtr.Zero,
                operation,
                fileName,
                null,
                workingDirectory,
                nShowCmd
            );

            if ((int)result <= 32)
            {
                // 备用方案
                var startInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Verb = operation,
                    UseShellExecute = true,
                    WindowStyle = int.TryParse(data.Data3, out int style) ? style switch
                    {
                        0 => ProcessWindowStyle.Normal,
                        1 => ProcessWindowStyle.Minimized,
                        2 => ProcessWindowStyle.Maximized,
                        _ => ProcessWindowStyle.Normal
                    } : ProcessWindowStyle.Normal
                };

                if (!string.IsNullOrWhiteSpace(workingDirectory))
                    startInfo.WorkingDirectory = workingDirectory;

                Process.Start(startInfo);
            }
        }

        // 实现IDisposable接口
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this); // 告知垃圾回收器不需要调用终结器
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        /// <param name="disposing"> 是否释放托管资源 </param>
        protected virtual void Dispose(bool disposing)
        {
            if (!isDisposed) isDisposed = true;
        }

        // 析构函数
        ~ActionManager()
        {
            Dispose(false);
        }
    }
}