using Quicker.Windows.MainWindows.MainWindow;
using Quicker.Contracts.Launching;
using Quicker.Contracts.Models;
using System.Threading.Tasks;
using Quicker.Database.Core;
using Quicker.Managers;
using Quicker.Extend;
using Quicker.Models;
using System.Windows;
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
        private readonly ModuleLoader _moduleLoader;

        public ActionManager() : this(new ProcessActionLauncher(), new ModuleLoader())
        {

        }

        public ActionManager(IActionLauncher launcher, ModuleLoader moduleLoader)
        {
            _launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));
            _moduleLoader = moduleLoader ?? throw new ArgumentNullException(nameof(moduleLoader));
        }

        /// <summary>
        /// 弹出消息（保留给调用方用，底层 launcher 内部不再直接弹）
        /// </summary>
        public void ShowToast(string message, ToastType type)
        {
            var toast = new ToastManager();
            toast.Show(message, type);
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
                        LoadExtension(data);
                        break;
                    case ActionType.OpenActionPage:
                        OpenActionPage(data);
                        break;
                    default:
                        ShowToast($"未知的动作类型：{data.ActionType}", ToastType.Error);
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
                ShowToast($"打开失败：{result.ErrorMessage}", ToastType.Error);
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

        private void LoadExtension(ButtonData data)
        {
            string dllPath = data.Location;
            if (string.IsNullOrEmpty(dllPath) || !System.IO.File.Exists(dllPath))
            {
                ShowToast($"扩展文件不存在: {dllPath}", ToastType.Error);
                return;
            }
            _moduleLoader.LoadModule(dllPath);
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