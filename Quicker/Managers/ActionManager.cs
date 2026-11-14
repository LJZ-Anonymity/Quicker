using Quicker.Windows.MainWindows.MainWindow;
using Quicker.Contracts.Launching;
using Quicker.Contracts.Models;
using System.Threading.Tasks;
using Quicker.Database.Core;
using Quicker.Extensions;
using Quicker.Managers;
using Quicker.Models;
using System.Windows;
using System.IO;
using System;

namespace Quicker.Managers
{
    /// <summary>
    /// 动作执行的编排层。负责分发任务、UI交互，不包含底层的进程启动逻辑。
    /// </summary>
    public class ActionManager : IDisposable
    {
        private bool isDisposed = false;

        private readonly IActionLauncher _launcher;

        // 将 ModuleLoader 设为全局共享单例，确保 _hosts 字典不会丢失，防止重复启动宿主进程
        private static readonly ModuleLoader _sharedModuleLoader = new ModuleLoader();

        public ActionManager() : this(new ProcessActionLauncher(), _sharedModuleLoader)
        {
        }

        public ActionManager(IActionLauncher launcher, ModuleLoader moduleLoader)
        {
            _launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));
            _moduleLoader = moduleLoader ?? throw new ArgumentNullException(nameof(moduleLoader));
        }

        private readonly ModuleLoader _moduleLoader;

        /// <summary>
        /// 弹出消息（UI线程安全）
        /// </summary>
        private void ShowToastSafe(string message, ToastType type)
        {
            Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
            {
                var toast = new ToastManager();
                toast.Show(message, type);
            }));
        }

        public void ShowToast(string message, ToastType type)
        {
            ShowToastSafe(message, type);
        }

        /// <summary>
        /// 执行动作（编排层）
        /// </summary>
        public async Task DoActionAsync(ButtonData data, string tableName)
        {
            try
            {
                switch (data.ActionType)
                {
                    case ActionType.OpenFile:
                        await RunLauncherAsync(ToLaunchRequest(data, LaunchKind.OpenFile));
                        break;
                    case ActionType.OpenWebsite:
                        await RunLauncherAsync(ToLaunchRequest(data, LaunchKind.OpenWebsite));
                        break;
                    case ActionType.OpenFiles:
                        await OpenFilesAsync(data);
                        break;
                    case ActionType.OpenUwpApp:
                        await RunLauncherAsync(ToLaunchRequest(data, LaunchKind.OpenUwpApp));
                        break;
                    case ActionType.OpenUri:
                        await RunLauncherAsync(ToLaunchRequest(data, LaunchKind.OpenUri));
                        break;
                    case ActionType.LoadExtension:
                        await LoadExtensionAsync(data); // 改为异步加载，释放 UI 线程
                        break;
                    case ActionType.OpenActionPage:
                        OpenActionPage(data);
                        break;
                    default:
                        ShowToastSafe($"未知的动作类型：{data.ActionType}", ToastType.Error);
                        break;
                }
            }
            finally
            {
                // 在后台线程异步更新使用次数，不阻塞 UI
                _ = Task.Run(() =>
                {
                    try
                    {
                        using var db = new ButtonDatabase();
                        db.IncreaseActionUsedTimes(data.ButtonID, tableName);
                    }
                    catch { } // 忽略数据库操作异常，避免影响主流程
                });
            }
        }

        private async Task RunLauncherAsync(LaunchRequest req)
        {
            var result = await _launcher.LaunchAsync(req);
            if (!result.Success)
            {
                ShowToastSafe($"打开失败：{result.ErrorMessage}", ToastType.Error);
            }
        }

        private async Task OpenFilesAsync(ButtonData data)
        {
            foreach (string file in data.Location.Split(';'))
            {
                if (string.IsNullOrWhiteSpace(file)) continue;
                var req = ToLaunchRequest(new ButtonData { Location = file }, LaunchKind.OpenFile);
                await RunLauncherAsync(req);
            }
        }

        /// <summary>
        /// ButtonData 映射为窄契约，集中在这一处
        /// </summary>
        private static LaunchRequest ToLaunchRequest(ButtonData data, LaunchKind kind)
        {
            return new LaunchRequest
            {
                Kind = kind,
                Location = data.Location ?? string.Empty,
                WebsiteMode = int.TryParse(data.Data1, out var mode) ? mode : 0,
                RunAsAdmin = string.Equals(data.Data1, "True", StringComparison.OrdinalIgnoreCase),
                WindowStyle = int.TryParse(data.Data3, out var ws)
                    ? (WindowShowStyle)ws
                    : WindowShowStyle.Normal,
                TryActivateExistingWindow = string.Equals(data.Data2, "true", StringComparison.OrdinalIgnoreCase)
            };
        }

        // 将 LoadExtension 改为异步执行
        private async Task LoadExtensionAsync(ButtonData data)
        {
            string dllPath = data.Location;
            if (string.IsNullOrEmpty(dllPath) || !File.Exists(dllPath))
            {
                ShowToastSafe($"扩展文件不存在: {dllPath}", ToastType.Error);
                return;
            }

            // 把阻塞的 LoadModule 扔到后台线程执行
            await Task.Run(() => _moduleLoader.LoadModule(dllPath));
        }

        private void OpenActionPage(ButtonData data)
        {
            var mainWindow = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
            if (mainWindow == null)
            {
                AppStateManager.CommonState = data.Data1;
                mainWindow = new();
                mainWindow.Show();
            }
            mainWindow.OpenActionPage(data);
        }

        /// <summary>
        /// 使用默认浏览器打开网址（供外部调用）
        /// </summary>
        public void OpenUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return;

            _ = _launcher.LaunchAsync(new LaunchRequest
            {
                Kind = LaunchKind.OpenWebsite,
                Location = url,
                WebsiteMode = 0 // 0 = 默认浏览器
            });
        }

        // 实现IDisposable接口
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!isDisposed) isDisposed = true;
        }

        ~ActionManager()
        {
            Dispose(false);
        }
    }
}