using Quicker.Windows.MainWindows.MainWindow;
using System.Collections.Concurrent;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Quicker.Models.Settings;
using Quicker.Database.Core;
using Quicker.Managers;
using System.Windows;
using System.IO;

namespace Quicker.Managers
{
    public static class AppStateManager
    {
        #region 内部缓存

        private static readonly ConcurrentDictionary<string, object> _cache = new(); // 缓存
        private static readonly object _cacheLock = new(); // 缓存锁

        #endregion

        #region 设置缓存与访问器

        private static List<BlacklistApplication> _blacklistApplications; // 黑名单应用
        private static OpenMainWindow _openMainWindowConditions; // 打开主窗口条件
        private static Blacklist _blacklistSettings; // 黑名单设置
        private static Convention _conventions; // 基础设置

        public static List<BlacklistApplication> BlacklistApplications
        {
            get => _blacklistApplications ??= SettingDatabase.GetAllBlacklistApplications(); // 获取黑名单应用
            set => _blacklistApplications = value; // 设置黑名单应用
        }

        public static OpenMainWindow OpenMainWindowConditions
        {
            get => _openMainWindowConditions ??= SettingDatabase.GetAllOpenMainWindowConditions().FirstOrDefault(); // 获取打开主窗口条件
            set => _openMainWindowConditions = value; // 设置打开主窗口条件
        }

        public static Blacklist BlacklistSettings
        {
            get => _blacklistSettings ??= SettingDatabase.GetAllBlacklistSettings().FirstOrDefault(); // 获取黑名单设置
            set => _blacklistSettings = value; // 设置黑名单设置
        }

        public static Convention Conventions
        {
            get => _conventions ??= SettingDatabase.GetAllConventions().FirstOrDefault(); // 获取基础设置
            set => _conventions = value; // 设置基础设置
        }

        #endregion

        #region 图标资源

        // 支持外部传入路径的托盘图标获取
        public static BitmapImage GetTrayIcon(string runningPath = null, string pausedPath = null, bool? pause = null)
        {
            string path;
            bool isPaused = pause ?? Pause;
            if (isPaused)
                path = !string.IsNullOrWhiteSpace(pausedPath) ? pausedPath : (Conventions?.TrayIconPathPaused ?? "");
            else
                path = !string.IsNullOrWhiteSpace(runningPath) ? runningPath : (Conventions?.TrayIconPathRunning ?? "");
            if (!string.IsNullOrWhiteSpace(path))
            {
                try
                {
                    if (path.StartsWith("pack://")) // 支持pack://资源
                        return new BitmapImage(new Uri(path, UriKind.Absolute));
                    if (File.Exists(path)) // 支持本地文件
                        return new BitmapImage(new Uri(path, UriKind.Absolute));
                }
                catch { }
            }
            return null;
        }

        public static BitmapImage TrayIcon => GetTrayIcon(); // 当前托盘图标

        #endregion

        #region 核心状态属性

        // 窗口操作对象
        public static MainWindow? MainWindowInstance { get; set; } = null; // 常驻主窗口实例（唯一）
        public static MainWindow? PreLoadMainWindow { get; set; } = null; // 预加载主窗口

        // 窗口状态
        public static bool Locked { get; set; } = false; // 锁定状态
        public static bool MainWindowPinned { get; set; } = false; // 订住状态
        public static bool SearchWindowPinned { get; set; } = false; // 搜索窗口订住状态
        public static bool Pause { get; set; } = false; // 暂停状态

        // 其他状态
        public static bool EnableMemoryOptimization { get; set; } = false; // 启用内存优化
        public static string CommonState { get; set; } = "common"; // 普通状态
        public static bool HasNewVersion { get; set; } = false; // 是否有新版本
        public static float Left { get; set; } = 0; // 左
        public static float Top { get; set; } = 0; // 上

        // 时间记录
        public static DateTime RecordedTime { get; set; } = DateTime.Now; // 记录时间
        public static DateTime StartTime { get; set; } = DateTime.Now; // 开始时间

        // 鼠标与按键操作变量
        public static System.Windows.Point StartPosition { get; set; } = new(); // 开始位置
        public static DateTime? MousePressStartTime { get; set; } = null; // 鼠标按下时间
        public static DateTime? KeyPressStartTime { get; set; } = null; // 按键按下时间

        // 定时器
        public static DispatcherTimer PressTimer { get; } = new(); // 按键按下定时器
        public static DispatcherTimer Timer { get; } = new(); // 定时器

        #endregion

        #region 事件与通知

        // 托盘图标改变事件
        public static event Action<string, string> TrayIconChanged;

        // UI 变更事件，供 MainWindow 打脏位，避免事件回调中直接改 UI（可能在后台线程触发）
        public static event Action<Quicker.Models.Settings.Appearance> AppearanceChanged; // 外观变更（颜色/字号/圆角/背景图/Blur/尺寸）
        public static event Action ConventionsChanged; // 行为设置变更（循环翻页/隐藏提示等）
        public static event Action<string> ActionPageChanged; // 页面数据变更，参数为表名
        public static event Action<string> SceneListChanged; // 场景页数变更，参数为表名
        public static event Action LockStateChanged; // 锁定状态变更

        public static void NotifyTrayIconChanged(string runningPath = null, string pausedPath = null)
        {
            TrayIconChanged?.Invoke(runningPath, pausedPath);
        }

        // 修改通知方法，支持传入外观对象
        public static void NotifyAppearanceChanged(Quicker.Models.Settings.Appearance appearance = null)
            => AppearanceChanged?.Invoke(appearance);
        public static void NotifyConventionsChanged() => ConventionsChanged?.Invoke();
        public static void NotifyActionPageChanged(string tableName) => ActionPageChanged?.Invoke(tableName);
        public static void NotifySceneListChanged(string tableName) => SceneListChanged?.Invoke(tableName);
        public static void NotifyLockStateChanged() => LockStateChanged?.Invoke();

        #endregion

        #region 生命周期与初始化

        static AppStateManager()
        {
            LoadSettings(); // 加载设置
        }

        public static void LoadSettings()
        {
            Conventions = SettingDatabase.GetAllConventions().FirstOrDefault(); // 获取基础设置
            OpenMainWindowConditions = SettingDatabase.GetAllOpenMainWindowConditions().FirstOrDefault(); // 获取打开主窗口条件
            BlacklistSettings = SettingDatabase.GetAllBlacklistSettings().FirstOrDefault(); // 获取黑名单设置
            BlacklistApplications = SettingDatabase.GetAllBlacklistApplications(); // 获取黑名单应用
            EnableMemoryOptimization = Conventions?.EnableMemoryOptimization ?? false; // 启用内存优化
            if (EnableMemoryOptimization) // 如果启用内存优化
            {
                ClearCachedData(); // 清除缓存数据
            }
        }

        // 清除缓存数据
        private static void ClearCachedData()
        {
            _conventions = null; // 基础设置
            _openMainWindowConditions = null; // 打开主窗口条件
            _blacklistSettings = null; // 黑名单设置
            _blacklistApplications = null; // 黑名单应用
            _cache.Clear();
        }

        // 释放资源
        public static void Dispose()
        {
            // 停止所有定时器
            PressTimer?.Stop();
            Timer?.Stop();

            // 关闭并清空常驻主窗口
            try { MainWindowInstance?.Close(); } catch { }
            MainWindowInstance = null;

            // 清除缓存数据
            ClearCachedData();
            _cache.Clear();
        }

        #endregion
    }
}