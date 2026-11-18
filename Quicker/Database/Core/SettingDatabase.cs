using Quicker.Database.SettingRepositories;
using Quicker.Models.Settings;
using System.Data.SQLite;
using Quicker.Helpers;
using System.IO;

namespace Quicker.Database.Core
{
    public static class SettingDatabase
    {
        /// <summary>
        /// 打开数据库连接
        /// </summary>
        public static SQLiteConnection OpenConnection() => DatabaseHelper.OpenConnection("Setting.db");

        static SettingDatabase()
        {
            DatabaseHelper.EnsureDatabaseDirectoryExists(); // 确保数据库目录存在
            string dbFilePath = Path.Combine(AppPathHelper.DatabaseFolder, "Setting.db"); // 设置数据库文件路径
            if (!File.Exists(dbFilePath))
            {
                SQLiteConnection.CreateFile(dbFilePath); // 创建数据库文件
                ConventionRepository.EnsureInitialized(); // 初始化 Convention 表
                OpenMainWindowRepository.EnsureInitialized(); // 初始化 OpenMainWindow 表
                BlacklistRepository.EnsureBlacklistInitialized(); // 初始化 Blacklist 表
                BlacklistRepository.EnsureBlacklistApplicationInitialized(); // 初始化 BlacklistApplication 表
                AppearanceRepository.EnsureInitialized(); // 初始化 Appearance 表
            }
            // 未来加版本迁移：读取 PRAGMA user_version，按版本号依次执行各 Repository 的迁移方法
        }

        // ===================== 转发：Convention =====================

        [Obsolete("请改用 ConventionRepository.ApplyConventionSettings")]
        public static void ApplyConventionSettings(bool autostart, bool shownotification, bool showaddimage, bool hideTooltip, int longPressThreshold, int mouseMovePixels, bool loopPageFlipping, bool rememberLastPage, bool enableMemoryOptimization, string trayIconPathRunning, string trayIconPathPaused, bool useMenuAnimation, bool isDarkTheme)
            => ConventionRepository.ApplyConventionSettings(autostart, shownotification, showaddimage, hideTooltip, longPressThreshold, mouseMovePixels, loopPageFlipping, rememberLastPage, enableMemoryOptimization, trayIconPathRunning, trayIconPathPaused, useMenuAnimation, isDarkTheme);

        [Obsolete("请改用 ConventionRepository.RecordLastPage")]
        public static void RecordLastPage(int lastPage)
            => ConventionRepository.RecordLastPage(lastPage);

        [Obsolete("请改用 ConventionRepository.SaveTotalUsageTime")]
        public static void SaveTotalUsageTime(double totalUsageTime)
            => ConventionRepository.SaveTotalUsageTime(totalUsageTime);

        [Obsolete("请改用 ConventionRepository.GetAllConventions")]
        public static List<Convention> GetAllConventions()
            => ConventionRepository.GetAllConventions();

        // ===================== 转发：OpenMainWindow =====================

        [Obsolete("请改用 OpenMainWindowRepository.ApplyOpenMainWindowSettings")]
        public static void ApplyOpenMainWindowSettings(bool OpenMainWindowByMiddleMouseClick, bool OpenMainWindowByX1MouseClick, bool OpenMainWindowByX2MouseClick, bool OpenMainWindowByCtrl_MiddleMouseClick, bool OpenMainWindowByCtrl_RightMouseClick, bool OpenMainWindowByMiddleMouseClickLonger, bool OpenMainWindowByRightMouseClickLonger, bool OpenMainWindowByRightMouseClick_Move, bool OpenMainWindowByCtrl, int windowStartupLocation)
            => OpenMainWindowRepository.ApplyOpenMainWindowSettings(OpenMainWindowByMiddleMouseClick, OpenMainWindowByX1MouseClick, OpenMainWindowByX2MouseClick, OpenMainWindowByCtrl_MiddleMouseClick, OpenMainWindowByCtrl_RightMouseClick, OpenMainWindowByMiddleMouseClickLonger, OpenMainWindowByRightMouseClickLonger, OpenMainWindowByRightMouseClick_Move, OpenMainWindowByCtrl, windowStartupLocation);

        [Obsolete("请改用 OpenMainWindowRepository.GetAllOpenMainWindowConditions")]
        public static List<OpenMainWindow> GetAllOpenMainWindowConditions()
            => OpenMainWindowRepository.GetAllOpenMainWindowConditions();

        // ===================== 转发：Blacklist / BlacklistApplication =====================

        [Obsolete("请改用 BlacklistRepository.ApplyBlacklistSettings")]
        public static void ApplyBlacklistSettings(bool isFullScreenDisabled, bool isBlacklistEnabledForExtendedHotkey)
            => BlacklistRepository.ApplyBlacklistSettings(isFullScreenDisabled, isBlacklistEnabledForExtendedHotkey);

        [Obsolete("请改用 BlacklistRepository.ApplyBlacklistApplication")]
        public static void ApplyBlacklistApplication(string applicationName, string processName, bool isInBlacklist, bool isFolder)
            => BlacklistRepository.ApplyBlacklistApplication(applicationName, processName, isInBlacklist, isFolder);

        [Obsolete("请改用 BlacklistRepository.DeleteBlacklistApplication")]
        public static void DeleteBlacklistApplication(string applicationName)
            => BlacklistRepository.DeleteBlacklistApplication(applicationName);

        [Obsolete("请改用 BlacklistRepository.GetAllBlacklistSettings")]
        public static List<Blacklist> GetAllBlacklistSettings()
            => BlacklistRepository.GetAllBlacklistSettings();

        [Obsolete("请改用 BlacklistRepository.GetAllBlacklistApplications")]
        public static List<BlacklistApplication> GetAllBlacklistApplications()
            => BlacklistRepository.GetAllBlacklistApplications();

        // 兼容：原来暴露过的初始化方法，转发给 BlacklistRepository
        [Obsolete("通常不需要手动调用；初始化由 SettingDatabase 静态构造函数统一完成")]
        public static void InitializeBlacklist()
            => BlacklistRepository.EnsureBlacklistInitialized();

        [Obsolete("通常不需要手动调用；初始化由 SettingDatabase 静态构造函数统一完成")]
        public static void InitializeBlacklistApplication()
            => BlacklistRepository.EnsureBlacklistApplicationInitialized();

        // ===================== 转发：Appearance =====================

        [Obsolete("请改用 AppearanceRepository.GetAppearanceSettings")]
        public static List<Appearance> GetAppearanceSettings()
            => AppearanceRepository.GetAppearanceSettings();

        [Obsolete("请改用 AppearanceRepository.UpdateAppearance")]
        public static void UpdateAppearance(Appearance appearance)
            => AppearanceRepository.UpdateAppearance(appearance);

        [Obsolete("请改用 AppearanceRepository.MigrateAppearanceBackgroundImagePathRoot")]
        public static void MigrateAppearanceBackgroundImagePathRoot(string oldRoot, string newRoot)
            => AppearanceRepository.MigrateAppearanceBackgroundImagePathRoot(oldRoot, newRoot);

        // 兼容：原来暴露过的初始化方法，转发给 AppearanceRepository
        [Obsolete("通常不需要手动调用；初始化由 SettingDatabase 静态构造函数统一完成")]
        public static void InitializeAppearance()
            => AppearanceRepository.EnsureInitialized();
    }
}