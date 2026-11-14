using System.Collections.Generic;
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
                InitializeConvention(); // 初始化 Convention 表
                InitializeOpenMainWindow(); // 初始化 OpenMainWindow 表
                InitializeBlacklist(); // 初始化 Blacklist 表
                InitializeBlacklistApplication(); // 初始化 BlacklistApplication 表
                InitializeAppearance(); // 初始化 Appearance 表
            }
        }

        // 初始化 Convention 表
        private static void InitializeConvention()
        {
            using var connection = OpenConnection(); // 打开数据库连接
            using var createConventionCommand = new SQLiteCommand(SQLStatements.CreateConventionTable, connection); // 创建 SQLiteCommand 对象
            createConventionCommand.ExecuteNonQuery(); // 执行创建表的命令
            InsertDefaultConventionData(); // 插入默认数据
        }
        // 插入默认数据
        private static void InsertDefaultConventionData()
        {
            using var connection = OpenConnection(); // 打开数据库连接
            var defaults = (false, true, true, 0.0, false, 300, 50, true, false, 111, true, "pack://application:,,,/Resources/Images/Quicker_Enabled.png", "pack://application:,,,/Resources/Images/Quicker_Disabled.ico", true, false); // 使用参数元组封装默认值
            var parameters = new Dictionary<string, object>
            {
                ["@AutoStart"] = defaults.Item1,
                ["@ShowNotification"] = defaults.Item2,
                ["@ShowAddImage"] = defaults.Item3,
                ["@TotalUsageTime"] = defaults.Item4,
                ["@HideTooltip"] = defaults.Item5,
                ["@LongPressThreshold"] = defaults.Item6,
                ["@MouseMovePixels"] = defaults.Item7,
                ["@LoopPageFlipping"] = defaults.Item8,
                ["@RememberLastPage"] = defaults.Item9,
                ["@LastPage"] = defaults.Item10,
                ["@EnableMemoryOptimization"] = defaults.Item11,
                ["@TrayIconPathRunning"] = defaults.Item12,
                ["@TrayIconPathPaused"] = defaults.Item13,
                ["@UseMenuAnimation"] = defaults.Item14,
                ["@IsDarkTheme"] = defaults.Item15
            }; // 使用字典批量绑定参数
            using var command = new SQLiteCommand(SQLStatements.InsertConvention, connection); // 创建 SQLiteCommand 对象
            foreach (var param in parameters)
                command.Parameters.AddWithValue(param.Key, param.Value); // 绑定参数
            command.ExecuteNonQuery(); // 执行插入命令
        }

        // 初始化OpenMainWindow 表
        private static void InitializeOpenMainWindow()
        {
            using var connection = OpenConnection(); // 打开数据库连接
            using var createOpenMainWindowCommand = new SQLiteCommand(SQLStatements.CreateOpenMainWindowTable, connection); // 创建 SQLiteCommand 对象
            createOpenMainWindowCommand.ExecuteNonQuery(); // 执行创建表的命令
            InsertDefaultOpenMainWindowData(); // 插入默认数据
        }
        // 插入默认数据
        private static void InsertDefaultOpenMainWindowData()
        {
            using var connection = OpenConnection(); // 打开数据库连接
            var defaults = (false, true, 2); // 使用参数元组封装默认值
            var parameters = new Dictionary<string, object>
            {
                ["@OpenMainWindowByMiddleMouseClick"] = defaults.Item1,
                ["@OpenMainWindowByX1MouseClick"] = defaults.Item1,
                ["@OpenMainWindowByX2MouseClick"] = defaults.Item1,
                ["@OpenMainWindowByCtrl_MiddleMouseClick"] = defaults.Item1,
                ["@OpenMainWindowByCtrl_RightMouseClick"] = defaults.Item1,
                ["@OpenMainWindowByMiddleMouseClickLonger"] = defaults.Item1,
                ["@OpenMainWindowByRightMouseClickLonger"] = defaults.Item1,
                ["@OpenMainWindowByRightMouseClick_Move"] = defaults.Item1,
                ["@OpenMainWindowByCtrl"] = defaults.Item2,
                ["@WindowStartupLocation"] = defaults.Item3
            }; // 使用字典批量绑定参数
            using var command = new SQLiteCommand(SQLStatements.InsertOpenMainWindow, connection); // 创建 SQLiteCommand 对象
            foreach (var param in parameters)
                command.Parameters.AddWithValue(param.Key, param.Value); // 绑定参数
            command.ExecuteNonQuery(); // 执行插入命令
        }

        // 初始化 Blacklist 表
        public static void InitializeBlacklist()
        {
            using var connection = OpenConnection(); // 打开数据库连接
            using var createBlacklistCommand = new SQLiteCommand(SQLStatements.CreateBlacklistTable, connection); // 创建 SQLiteCommand 对象
            createBlacklistCommand.ExecuteNonQuery(); // 执行创建表的命令
            InsertDefaultBlacklistData(); // 插入默认数据
        }
        // 插入默认数据
        private static void InsertDefaultBlacklistData()
        {
            using var connection = OpenConnection(); // 打开数据库连接
            var defaults = (false, false); // 使用参数元组封装默认值
            var parameters = new Dictionary<string, object>
            {
                ["@IsFullScreenDisabled"] = defaults.Item1,
                ["@IsBlacklistEnabledForExtendedHotkey"] = defaults.Item2
            }; // 使用字典批量绑定参数
            using var command = new SQLiteCommand(SQLStatements.InsertBlacklist, connection); // 创建 SQLiteCommand 对象
            foreach (var param in parameters)
                command.Parameters.AddWithValue(param.Key, param.Value); // 绑定参数
            command.ExecuteNonQuery(); // 执行插入命令
        }

        // 初始化 BlacklistApplication 表
        public static void InitializeBlacklistApplication()
        {
            using var connection = OpenConnection(); // 打开数据库连接
            using var createBlacklistApplicationCommand = new SQLiteCommand(SQLStatements.CreateBlacklistApplicationTable, connection); // 创建 SQLiteCommand 对象
            createBlacklistApplicationCommand.ExecuteNonQuery(); // 执行创建表的命令
        }

        // 初始化 Appearance 表
        public static void InitializeAppearance()
        {
            using var connection = OpenConnection(); // 打开数据库连接
            using var createAppearanceCommand = new SQLiteCommand(SQLStatements.CreateAppearanceTable, connection); // 创建 SQLiteCommand 对象
            createAppearanceCommand.ExecuteNonQuery(); // 执行创建表的命令
            InsertDefaultAppearanceData(); // 插入默认数据
        }

        // 插入默认数据
        private static void InsertDefaultAppearanceData()
        {
            using var connection = OpenConnection(); // 打开数据库连接
            var lightDefaults = ("Light", 77.6, 0.2, 0.0, 0.0, "#FFF3F3F3", "#FFD3D3D3", "#22F3F3F3", "#FFA1A1A1", "#FFFFFFFF", "#FFBEE6FD", "#FFF3F3F3", "#FFEAEAEA", "#FF000000", "#FF696969", "#D0FF8C00", "#FF666666", -1, -1, 12, 4, "", 1.0, 0, 0, false, false, false, false, false); // 使用参数元组封装默认值
            var darkDefaults = ("Dark", 77.6, 0.2, 0.0, 0.0, "#FFF3F3F3", "#FFD3D3D3", "#22F3F3F3", "#FFA1A1A1", "#FFFFFFFF", "#FFBEE6FD", "#FFF3F3F3", "#FFEAEAEA", "#FF000000", "#FF696969", "#D0FF8C00", "#FF666666", -1, -1, 12, 4, "", 1.0, 0, 0, false, false, false, false, false); // 使用参数元组封装默认值
            InsertSingleAppearance(connection, lightDefaults); // 插入浅色主题
            InsertSingleAppearance(connection, darkDefaults); // 插入深色主题
        }

        // 辅助方法，简化插入逻辑
        private static void InsertSingleAppearance(SQLiteConnection connection, (string, double, double, double, double, string, string, string, string, string, string, string, string, string, string, string, string, int, int, int, int,string, double, int, int, bool, bool, bool, bool, bool) defaults)
        {
            var parameters = new Dictionary<string, object>
            {
                ["@ThemeName"] = defaults.Item1,
                ["@ButtonSize"] = defaults.Item2, // 按钮大小
                ["@ButtonGap"] = defaults.Item3, // 按钮间隙
                ["@BorderWidth"] = defaults.Item4, // 边框宽度
                ["@ButtonCornerRadius"] = defaults.Item5, // 按钮圆角
                ["@BackgroundColor"] = defaults.Item6, // 背景颜色
                ["@BorderColor"] = defaults.Item7, // 边框颜色
                ["@ToolbarColor"] = defaults.Item8, // 工具栏颜色
                ["@ToolbarIconColor"] = defaults.Item9, // 工具栏图标颜色
                ["@ActionButtonColor"] = defaults.Item10, // 动作按钮颜色
                ["@ActionButtonMouseOverColor"] = defaults.Item11, // 动作按钮鼠标悬停颜色
                ["@BlankButtonColor"] = defaults.Item12, // 空白按钮颜色
                ["@BlankButtonMouseOverColor"] = defaults.Item13, // 空白按钮鼠标悬停颜色
                ["@ButtonTextColor"] = defaults.Item14, // 按钮文字颜色
                ["@ActionIconColor"] = defaults.Item15, // 动作图标颜色
                ["@TriggerKeyTextColor"] = defaults.Item16, // 触发键文字颜色
                ["@OtherIconColor"] = defaults.Item17, // 其他位置图标颜色
                ["@Font1"] = defaults.Item18, // 字体1
                ["@Font2"] = defaults.Item19, // 字体2
                ["@FontSize"] = defaults.Item20, // 字体大小
                ["@FontWeight"] = defaults.Item21, // 字体粗细
                ["@BackgroundImagePath"] = defaults.Item22, // 背景图片路径
                ["@BackgroundImageOpacity"] = defaults.Item23, // 背景图片不透明度
                ["@Blur"] = defaults.Item24, // 模糊模式
                ["@Win11CornerRadius"] = defaults.Item25, // Win11圆角模式
                ["@AutoHideTitleBar"] = defaults.Item26, // 自动缩小动作名称文字
                ["@ShowActionButtonMouseOver"] = defaults.Item27, // 鼠标悬浮在动作按钮上时，放大显示按钮
                ["@HideActionNameAfterIcon"] = defaults.Item28, // 设置动作图标后隐藏动作名称
                ["@ShowActionIconShadow"] = defaults.Item29, // 动作图标显示阴影
                ["@EnablePreview"] = defaults.Item30 // 开启预览功能
            };
            using var command = new SQLiteCommand(SQLStatements.InsertAppearance, connection);
            foreach (var param in parameters)
                command.Parameters.AddWithValue(param.Key, param.Value);
            command.ExecuteNonQuery();
        }

        /// <summary>
        /// 更新Convention设置信息
        /// </summary>
        /// <param name="autostart"> 是否开机自启 </param>
        /// <param name="shownotification"> 是否显示通知 </param>
        /// <param name="showaddimage"> 是否显示添加图片 </param>
        /// <param name="hideTooltip"> 是否隐藏提示 </param>
        /// <param name="longPressThreshold"> 长按阈值 </param>
        /// <param name="mouseMovePixels"> 鼠标移动像素 </param>
        /// <param name="loopPageFlipping"> 是否循环翻页 </param>
        /// <param name="rememberLastPage"> 是否记住设置窗口中最后打开的页面 </param>
        /// <param name="enableMemoryOptimization"> 是否启用内存优化 </param>
        /// <param name="trayIconPathRunning"> 运行时托盘图标路径 </param>
        /// <param name="trayIconPathPaused"> 暂停时托盘图标路径 </param>
        /// <param name="useMenuAnimation"> 是否启用菜单动画 </param>
        public static void ApplyConventionSettings(bool autostart, bool shownotification, bool showaddimage, bool hideTooltip, int longPressThreshold, int mouseMovePixels, bool loopPageFlipping, bool rememberLastPage, bool enableMemoryOptimization, string trayIconPathRunning, string trayIconPathPaused, bool useMenuAnimation, bool isDarkTheme)
        {
            using var connection = OpenConnection(); // 打开数据库连接
            using var transaction = connection.BeginTransaction(); // 开启事务
            using var command = new SQLiteCommand(SQLStatements.UpdateConvention, connection); // 创建 SQLiteCommand 对象
            command.Parameters.AddWithValue("@AutoStart", autostart); // 是否开机自启
            command.Parameters.AddWithValue("@ShowNotification", shownotification); // 是否显示通知
            command.Parameters.AddWithValue("@ShowAddImage", showaddimage); // 是否显示添加图片
            command.Parameters.AddWithValue("@HideTooltip", hideTooltip); // 是否隐藏提示
            command.Parameters.AddWithValue("@LongPressThreshold", longPressThreshold); // 长按阈值
            command.Parameters.AddWithValue("@MouseMovePixels", mouseMovePixels); // 鼠标移动像素
            command.Parameters.AddWithValue("@LoopPageFlipping", loopPageFlipping); // 是否循环翻页
            command.Parameters.AddWithValue("@RememberLastPage", rememberLastPage); // 是否记住设置窗口中最后打开的页面
            command.Parameters.AddWithValue("@EnableMemoryOptimization", enableMemoryOptimization); // 是否启用内存优化
            command.Parameters.AddWithValue("@TrayIconPathRunning", trayIconPathRunning); // 运行时托盘图标路径
            command.Parameters.AddWithValue("@TrayIconPathPaused", trayIconPathPaused); // 暂停时托盘图标路径
            command.Parameters.AddWithValue("@UseMenuAnimation", useMenuAnimation); // 是否启用菜单动画
            command.Parameters.AddWithValue("@IsDarkTheme", isDarkTheme); // 是否启用暗黑主题
            command.ExecuteNonQuery(); // 执行更新命令
            transaction.Commit(); // 提交事务
        }

        /// <summary>
        /// 设置窗口中最后打开的页面
        /// </summary>
        /// <param name="lastPage"> 设置窗口中最后打开的页面 </param>
        public static void RecordLastPage(int lastPage)
        {
            using var connection = OpenConnection(); // 打开数据库连接
            using var command = new SQLiteCommand(SQLStatements.UpdateLastPage, connection); // 创建 SQLiteCommand 对象
            command.Parameters.AddWithValue("@LastPage", lastPage); // 绑定参数
            command.ExecuteNonQuery(); // 执行更新命令
        }

        /// <summary>
        /// 更新OpenMainWindow设置信息
        /// </summary>
        /// <param name="OpenMainWindowByMiddleMouseClick"> 按下中键 </param>
        /// <param name="OpenMainWindowByX1MouseClick"> 按下X1键 </param>
        /// <param name="OpenMainWindowByX2MouseClick"> 按下X2键 </param>
        /// <param name="OpenMainWindowByCtrl_MiddleMouseClick"> Ctrl+中键单击 </param>
        /// <param name="OpenMainWindowByCtrl_RightMouseClick"> Ctrl+右键单击 </param>
        /// <param name="OpenMainWindowByMiddleMouseClickLonger"> 长按中键 </param>
        /// <param name="OpenMainWindowByRightMouseClickLonger"> 长按右键 </param>
        /// <param name="OpenMainWindowByRightMouseClick_Move"> 按右键移动 </param>
        /// <param name="OpenMainWindowByCtrl"> 单击Ctrl键 </param>
        /// <param name="windowStartupLocation"> 功能面板打开位置 </param>
        public static void ApplyOpenMainWindowSettings(bool OpenMainWindowByMiddleMouseClick, bool OpenMainWindowByX1MouseClick, bool OpenMainWindowByX2MouseClick, bool OpenMainWindowByCtrl_MiddleMouseClick, bool OpenMainWindowByCtrl_RightMouseClick, bool OpenMainWindowByMiddleMouseClickLonger, bool OpenMainWindowByRightMouseClickLonger, bool OpenMainWindowByRightMouseClick_Move, bool OpenMainWindowByCtrl, int windowStartupLocation)
        {
            using var connection = OpenConnection(); // 打开数据库连接
            using var transaction = connection.BeginTransaction(); // 开启事务
            using var command = new SQLiteCommand(SQLStatements.UpdateOpenMainWindow, connection); // 创建 SQLiteCommand 对象
            command.Parameters.AddWithValue("@OpenMainWindowByMiddleMouseClick", OpenMainWindowByMiddleMouseClick); // 按下中键
            command.Parameters.AddWithValue("@OpenMainWindowByX1MouseClick", OpenMainWindowByX1MouseClick); // 按下X1键
            command.Parameters.AddWithValue("@OpenMainWindowByX2MouseClick", OpenMainWindowByX2MouseClick); // 按下X2键
            command.Parameters.AddWithValue("@OpenMainWindowByCtrl_MiddleMouseClick", OpenMainWindowByCtrl_MiddleMouseClick); // Ctrl+中键单击
            command.Parameters.AddWithValue("@OpenMainWindowByCtrl_RightMouseClick", OpenMainWindowByCtrl_RightMouseClick); // Ctrl+右键单击
            command.Parameters.AddWithValue("@OpenMainWindowByMiddleMouseClickLonger", OpenMainWindowByMiddleMouseClickLonger); // 长按中键
            command.Parameters.AddWithValue("@OpenMainWindowByRightMouseClickLonger", OpenMainWindowByRightMouseClickLonger); // 长按右键
            command.Parameters.AddWithValue("@OpenMainWindowByRightMouseClick_Move", OpenMainWindowByRightMouseClick_Move); // 按右键移动
            command.Parameters.AddWithValue("@OpenMainWindowByCtrl", OpenMainWindowByCtrl); // 单击Ctrl键
            command.Parameters.AddWithValue("@WindowStartupLocation", windowStartupLocation); // 功能面板打开位置
            command.ExecuteNonQuery(); // 执行更新命令
            transaction.Commit(); // 提交事务
        }

        /// <summary>
        /// 更新Blacklist设置信息
        /// </summary>
        /// <param name="isFullScreenDisabled"> 是否开启全屏或最大化禁用功能 </param>
        /// <param name="isBlacklistEnabledForExtendedHotkey"> 是否将黑名单与全屏禁用设置应用于扩展热键功能 </param>
        public static void ApplyBlacklistSettings(bool isFullScreenDisabled, bool isBlacklistEnabledForExtendedHotkey)
        {
            using var connection = OpenConnection(); // 打开数据库连接
            using var transaction = connection.BeginTransaction(); // 开启事务
            using var command = new SQLiteCommand(SQLStatements.UpdateBlacklist, connection); // 创建 SQLiteCommand 对象
            command.Parameters.AddWithValue("@IsFullScreenDisabled", isFullScreenDisabled); // 是否开启全屏或最大化禁用功能
            command.Parameters.AddWithValue("@IsBlacklistEnabledForExtendedHotkey", isBlacklistEnabledForExtendedHotkey); // 是否将黑名单与全屏禁用设置应用于扩展热键功能
            command.ExecuteNonQuery(); // 执行更新命令
            transaction.Commit(); // 提交事务
        }

        /// <summary>
        /// 添加黑名单应用
        /// </summary>
        /// <param name="applicationName"> 应用名称 </param>
        /// <param name="processName"> 进程名称 </param>
        /// <param name="isInBlacklist"> 是否在黑名单中 </param>
        /// <param name="isFolder"> 是否是文件夹 </param>
        public static void ApplyBlacklistApplication(string applicationName, string processName, bool isInBlacklist, bool isFolder)
        {
            using var connection = OpenConnection(); // 打开数据库连接
            using var transaction = connection.BeginTransaction(); // 开启事务
            using var command = new SQLiteCommand(SQLStatements.InsertBlacklistApplication, connection); // 创建 SQLiteCommand 对象
            command.Parameters.AddWithValue("@ApplicationName", applicationName); // 应用名称
            command.Parameters.AddWithValue("@ProcessName", processName); // 进程名称
            command.Parameters.AddWithValue("@IsInBlacklist", isInBlacklist); // 是否在黑名单中
            command.Parameters.AddWithValue("@IsFolder", isFolder); // 是否是文件夹
            command.ExecuteNonQuery(); // 执行插入命令
            transaction.Commit(); // 提交事务
        }

        /// <summary>
        /// 通过应用名称删除黑名单应用
        /// </summary>
        /// <param name="applicationName"> 应用名称 </param>
        public static void DeleteBlacklistApplication(string applicationName)
        {
            using var connection = OpenConnection(); // 打开数据库连接
            using var transaction = connection.BeginTransaction(); // 开启事务
            using var command = new SQLiteCommand(SQLStatements.DeleteBlacklistApplication, connection); // 创建 SQLiteCommand 对象
            command.Parameters.AddWithValue("@ApplicationName", applicationName); // 应用名称
            command.ExecuteNonQuery(); // 执行删除命令
            transaction.Commit(); // 提交事务
        }

        /// <summary>
        /// 保存总使用时长
        /// </summary>
        /// <param name="totalUsageTime"> 总使用时长 </param>
        public static void SaveTotalUsageTime(double totalUsageTime)
        {
            using var connection = OpenConnection(); // 打开数据库连接
            using var transaction = connection.BeginTransaction(); // 开启事务
            using var command = new SQLiteCommand(SQLStatements.UpdateTotalUsageTime, connection); // 创建 SQLiteCommand 对象
            command.Parameters.AddWithValue("@TotalUsageTime", totalUsageTime); // 设置参数
            command.ExecuteNonQuery(); // 执行更新命令
            transaction.Commit(); // 提交事务
        }

        /// <summary>
        /// 获取常规设置信息
        /// </summary>
        /// <returns> Convention 类 </returns>
        public static List<Convention> GetAllConventions()
        {
            var conventions = new List<Convention>(); // 创建一个空的 Convention 列表
            using var connection = OpenConnection(); // 打开数据库连接
            using var transaction = connection.BeginTransaction(System.Data.IsolationLevel.ReadCommitted); // 开启事务
            using var command = new SQLiteCommand(SQLStatements.GetAllConventions, connection); // 创建 SQLiteCommand 对象
            using var reader = command.ExecuteReader(); // 执行查询并获取结果
            while (reader.Read())
            {
                conventions.Add(new Convention
                {
                    ID = reader.GetInt32(0), // 主键
                    AutoStart = reader.GetBoolean(1), // 是否开机自启
                    ShowNotification = reader.GetBoolean(2), // 是否显示通知
                    ShowAddImage = reader.GetBoolean(3), // 是否显示添加图片
                    TotalUsageTime = reader.GetDouble(4), // 总使用时长
                    HideTooltip = reader.GetBoolean(5), // 是否隐藏提示
                    LongPressThreshold = reader.GetInt32(6), // 长按阈值
                    MouseMovePixels = reader.GetInt32(7), // 鼠标移动像素
                    LoopPageFlipping = reader.GetBoolean(8), // 是否循环翻页
                    RememberLastPage = reader.GetBoolean(9), // 是否记住设置窗口中最后打开的页面
                    LastPage = reader.GetInt32(10), // 设置窗口中最后打开的页面
                    EnableMemoryOptimization = reader.GetBoolean(11), // 是否启用内存优化
                    TrayIconPathRunning = reader.GetString(12), // 运行时托盘图标路径
                    TrayIconPathPaused = reader.GetString(13), // 暂停时托盘图标路径
                    UseMenuAnimation = reader.GetBoolean(14), // 是否启用菜单动画
                    IsDarkTheme = reader.GetBoolean(15)
                }); // 将读取到的数据添加到列表中
            }
            transaction.Commit(); // 提交事务
            return conventions; // 返回所有 Convention 数据
        }

        /// <summary>
        /// 获取OpenMainWindow设置信息
        /// </summary>
        public static List<OpenMainWindow> GetAllOpenMainWindowConditions()
        {
            var conditions = new List<OpenMainWindow>(); // 创建一个空的 OpenMainWindow 列表
            using var connection = OpenConnection(); // 打开数据库连接
            using var transaction = connection.BeginTransaction(System.Data.IsolationLevel.ReadCommitted); // 开启事务
            using var command = new SQLiteCommand(SQLStatements.GetAllOpenMainWindowConditions, connection); // 创建 SQLiteCommand 对象
            using var reader = command.ExecuteReader(); // 执行查询并获取结果
            while (reader.Read())
            {
                conditions.Add(new OpenMainWindow
                {
                    ID = reader.GetInt32(0),
                    OpenMainWindowByMiddleMouseClick = reader.GetBoolean(1), // 按下中键
                    OpenMainWindowByX1MouseClick = reader.GetBoolean(2), // 按下X1键
                    OpenMainWindowByX2MouseClick = reader.GetBoolean(3), // 按下X2键
                    OpenMainWindowByCtrl_MiddleMouseClick = reader.GetBoolean(4), // Ctrl+中键单击
                    OpenMainWindowByCtrl_RightMouseClick = reader.GetBoolean(5), // Ctrl+右键单击
                    OpenMainWindowByMiddleMouseClickLonger = reader.GetBoolean(6), // 长按中键
                    OpenMainWindowByRightMouseClickLonger = reader.GetBoolean(7), // 长按右键
                    OpenMainWindowByRightMouseClick_Move = reader.GetBoolean(8), // 按右键移动
                    OpenMainWindowByCtrl = reader.GetBoolean(9), // 单击Ctrl键
                    WindowStartupLocation = reader.GetInt32(10) // 功能面板打开位置
                }); // 将读取到的数据添加到列表中
            }
            transaction.Commit(); // 提交事务
            return conditions; // 返回所有 OpenMainWindow 数据
        }

        /// <summary>
        /// 获取黑名单设置
        /// </summary>
        public static List<Blacklist> GetAllBlacklistSettings()
        {
            var blacklists = new List<Blacklist>(); // 创建一个空的 Blacklist 列表
            using var connection = OpenConnection(); // 打开数据库连接
            using var transaction = connection.BeginTransaction(System.Data.IsolationLevel.ReadCommitted); // 开启事务
            using var command = new SQLiteCommand(SQLStatements.GetAllBlacklistSettings, connection); // 创建 SQLiteCommand 对象
            using var reader = command.ExecuteReader(); // 执行查询并获取结果
            while (reader.Read())
            {
                blacklists.Add(new Blacklist
                {
                    ID = reader.GetInt32(0), // 主键
                    IsFullScreenDisabled = reader.GetBoolean(1), // 是否开启全屏或最大化禁用功能
                    IsBlacklistEnabledForExtendedHotkey = reader.GetBoolean(2) // 是否将黑名单与全屏禁用设置应用于扩展热键功能
                }); // 将读取到的数据添加到列表中
            }
            transaction.Commit(); // 提交事务
            return blacklists; // 返回所有 Blacklist 数据
        }

        /// <summary>
        /// 获取黑名单应用
        /// </summary>
        public static List<BlacklistApplication> GetAllBlacklistApplications()
        {
            var applications = new List<BlacklistApplication>(); // 创建一个空的 BlacklistApplication 列表
            using var connection = OpenConnection(); // 打开数据库连接
            using var transaction = connection.BeginTransaction(System.Data.IsolationLevel.ReadCommitted); // 开启事务
            using var command = new SQLiteCommand(SQLStatements.GetAllBlacklistApplications, connection); // 创建 SQLiteCommand 对象
            using var reader = command.ExecuteReader(); // 执行查询并获取结果
            while (reader.Read())
            {
                applications.Add(new BlacklistApplication
                {
                    ID = reader.GetInt32(0), // 主键
                    ApplicationName = reader.GetString(1), // 应用名称
                    ProcessName = reader.GetString(2), // 进程名称
                    IsInBlacklist = reader.GetBoolean(3), // 是否在黑名单中
                    IsFolder = reader.GetBoolean(4) // 是否是文件夹
                }); // 将读取到的数据添加到列表中
            }
            transaction.Commit(); // 提交事务
            return applications; // 返回所有 BlacklistApplication 数据
        }

        /// <summary>
        /// 获取外观设置
        /// </summary>
        public static List<Appearance> GetAppearanceSettings()
        {
            // 获取当前主题状态：读取 Convention 表，确定 IsDarkTheme 的值
            var conventions = GetAllConventions().FirstOrDefault();
            bool isDarkTheme = conventions.IsDarkTheme;
            string themeName = isDarkTheme ? "Dark" : "Light";

            // 执行 Appearance 表的查询
            var appearances = new List<Appearance>();
            using var connection = OpenConnection(); // 打开数据库连接
            using var transaction = connection.BeginTransaction(System.Data.IsolationLevel.ReadCommitted); // 开启只读事务
            using var command = new SQLiteCommand(SQLStatements.GetAppearanceSettings, connection);
            command.Parameters.AddWithValue("@ThemeName", themeName);  // 绑定确定的主题名称
            using var reader = command.ExecuteReader(); // 执行查询并获取数据读取器
            while (reader.Read())
            {
                appearances.Add(new Appearance
                {
                    ThemeName = reader.GetString(0), // 外观ID
                    ButtonSize = reader.GetDouble(1), // 按钮大小
                    ButtonGap = reader.GetDouble(2), // 按钮间隙
                    BorderWidth = reader.GetDouble(3), // 边框宽度
                    ButtonCornerRadius = reader.GetDouble(4), // 按钮圆角
                    BackgroundColor = reader.GetString(5), // 背景颜色
                    BorderColor = reader.GetString(6), // 边框颜色
                    ToolbarColor = reader.GetString(7), // 工具栏颜色
                    ToolbarIconColor = reader.GetString(8), // 工具栏图标颜色
                    ActionButtonColor = reader.GetString(9), // 动作按钮颜色
                    ActionButtonMouseOverColor = reader.GetString(10), // 动作按钮悬浮颜色
                    BlankButtonColor = reader.GetString(11), // 空白按钮颜色
                    BlankButtonMouseOverColor = reader.GetString(12), // 空白按钮悬浮颜色
                    ButtonTextColor = reader.GetString(13), // 按钮文字颜色
                    Font1 = reader.GetInt32(14), // 字体1
                    Font2 = reader.GetInt32(15), // 字体2
                    FontSize = reader.GetDouble(16), // 字体大小
                    FontWeight = reader.GetInt32(17), // 字体粗细
                    BackgroundImagePath = reader.GetString(18), // 背景图片路径
                    BackgroundImageOpacity = reader.GetDouble(19), // 背景图片不透明度
                    Blur = reader.GetInt32(20), // 模糊模式
                    Win11CornerRadius = reader.GetInt32(21), // Win11圆角模式
                    AutoHideTitleBar = reader.GetBoolean(22), // 自动隐藏标题栏
                    ShowActionButtonMouseOver = reader.GetBoolean(23), // 动作按钮悬浮放大
                    HideActionNameAfterIcon = reader.GetBoolean(24), // 设置动作图标后隐藏名称
                    ShowActionIconShadow = reader.GetBoolean(25), // 动作图标显示阴影
                    EnablePreview = reader.GetBoolean(26) // 启用预览
                });
            }
            transaction.Commit(); // 提交事务
            return appearances; // 返回当前主题的外观设置
        }

        /// <summary>
        /// 更新外观设置
        /// </summary>
        public static void UpdateAppearance(Appearance appearance)
        {
            using var connection = OpenConnection(); // 打开数据库连接
            using var transaction = connection.BeginTransaction(); // 开启事务
            using var command = new SQLiteCommand(SQLStatements.UpdateAppearance, connection); // 创建 SQLiteCommand 对象
            command.Parameters.AddWithValue("@ThemeName", appearance.ThemeName); // 主题名称
            command.Parameters.AddWithValue("@ButtonSize", appearance.ButtonSize); // 按钮大小
            command.Parameters.AddWithValue("@ButtonGap", appearance.ButtonGap); // 按钮间隙
            command.Parameters.AddWithValue("@BorderWidth", appearance.BorderWidth); // 边框宽度
            command.Parameters.AddWithValue("@ButtonCornerRadius", appearance.ButtonCornerRadius); // 按钮圆角
            command.Parameters.AddWithValue("@BackgroundColor", appearance.BackgroundColor); // 背景颜色
            command.Parameters.AddWithValue("@BorderColor", appearance.BorderColor); // 边框颜色
            command.Parameters.AddWithValue("@ToolbarColor", appearance.ToolbarColor); // 工具栏颜色
            command.Parameters.AddWithValue("@ToolbarIconColor", appearance.ToolbarIconColor); // 工具栏图标颜色
            command.Parameters.AddWithValue("@ActionButtonColor", appearance.ActionButtonColor); // 动作按钮颜色
            command.Parameters.AddWithValue("@ActionButtonMouseOverColor", appearance.ActionButtonMouseOverColor); // 动作按钮悬浮颜色
            command.Parameters.AddWithValue("@BlankButtonColor", appearance.BlankButtonColor); // 空白按钮颜色
            command.Parameters.AddWithValue("@BlankButtonMouseOverColor", appearance.BlankButtonMouseOverColor); // 空白按钮悬浮颜色
            command.Parameters.AddWithValue("@ButtonTextColor", appearance.ButtonTextColor); // 按钮文字颜色
            command.Parameters.AddWithValue("@Font1", appearance.Font1); // 字体1
            command.Parameters.AddWithValue("@Font2", appearance.Font2); // 字体2
            command.Parameters.AddWithValue("@FontSize", appearance.FontSize); // 字体大小
            command.Parameters.AddWithValue("@FontWeight", appearance.FontWeight); // 字体粗细
            command.Parameters.AddWithValue("@BackgroundImagePath", appearance.BackgroundImagePath); // 背景图片路径
            command.Parameters.AddWithValue("@BackgroundImageOpacity", appearance.BackgroundImageOpacity); // 背景图片不透明度
            command.Parameters.AddWithValue("@Blur", appearance.Blur); // 模糊模式
            command.Parameters.AddWithValue("@Win11CornerRadius", appearance.Win11CornerRadius); // Win11圆角模式
            command.Parameters.AddWithValue("@AutoHideTitleBar", appearance.AutoHideTitleBar); // 自动隐藏标题栏
            command.Parameters.AddWithValue("@ShowActionButtonMouseOver", appearance.ShowActionButtonMouseOver); // 动作按钮悬浮放大
            command.Parameters.AddWithValue("@HideActionNameAfterIcon", appearance.HideActionNameAfterIcon); // 设置动作图标后隐藏名称
            command.Parameters.AddWithValue("@ShowActionIconShadow", appearance.ShowActionIconShadow); // 动作图标显示阴影
            command.Parameters.AddWithValue("@EnablePreview", appearance.EnablePreview); // 启用预览
            command.ExecuteNonQuery(); // 执行命令
            transaction.Commit(); // 提交事务
        }

        /// <summary>
        /// 将 Appearance 表中的 BackgroundImagePath 的旧根路径批量替换为新根路径。
        /// </summary>
        /// <param name="oldRoot">旧图片根路径（例如 C:\\Users\\LENOVO\\AppData\\Roaming\\Anonymity\\Quicker）</param>
        /// <param name="newRoot">新图片根路径（例如 C:\\Downloads）</param>
        public static void MigrateAppearanceBackgroundImagePathRoot(string oldRoot, string newRoot)
        {
            if (string.IsNullOrWhiteSpace(oldRoot) || string.IsNullOrWhiteSpace(newRoot))
            {
                return;
            }

            string normalizedOld = oldRoot.Trim();
            string normalizedNew = newRoot.Trim();
            if (string.Equals(normalizedOld, normalizedNew, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            using var connection = OpenConnection(); // 打开数据库连接
            using var transaction = connection.BeginTransaction(); // 开启事务
            string sql = "UPDATE Appearance SET " +
                         "BackgroundImagePath = REPLACE(COALESCE(BackgroundImagePath, ''), @OldRoot, @NewRoot) " +
                         "WHERE BackgroundImagePath LIKE @OldLike";
            using var cmd = new SQLiteCommand(sql, connection, transaction);
            cmd.Parameters.AddWithValue("@OldRoot", normalizedOld);
            cmd.Parameters.AddWithValue("@NewRoot", normalizedNew);
            cmd.Parameters.AddWithValue("@OldLike", normalizedOld + "%");
            cmd.ExecuteNonQuery();
            transaction.Commit();
        }

        // 数据库文件路径语句
        private static class SQLStatements
        {
            // 常规设置表
            public const string CreateConventionTable = @"
            CREATE TABLE IF NOT EXISTS Convention
            (
                ID INTEGER PRIMARY KEY AUTOINCREMENT,
                AutoStart BOOLEAN,
                ShowNotification BOOLEAN,
                ShowAddImage BOOLEAN,
                TotalUsageTime REAL,
                HideTooltip BOOLEAN,
                LongPressThreshold INTEGER,
                MouseMovePixels INTEGER,
                LoopPageFlipping BOOLEAN,
                RememberLastPage BOOLEAN,
                LastPage INTEGER,
                EnableMemoryOptimization BOOLEAN,
                TrayIconPathRunning TEXT,
                TrayIconPathPaused TEXT,
                UseMenuAnimation BOOLEAN,
                IsDarkTheme BOOLEAN
            );";
            public const string InsertConvention = @"
            INSERT INTO Convention
            (
                AutoStart, 
                ShowNotification,   ShowAddImage,
                TotalUsageTime,     HideTooltip,
                LongPressThreshold, MouseMovePixels,
                LoopPageFlipping,   RememberLastPage,
                LastPage,           EnableMemoryOptimization,
                TrayIconPathRunning, TrayIconPathPaused,
                UseMenuAnimation,   IsDarkTheme
            )
            VALUES
            (
                @AutoStart,
                @ShowNotification,  @ShowAddImage,
                @TotalUsageTime,    @HideTooltip,
                @LongPressThreshold,@MouseMovePixels,
                @LoopPageFlipping,  @RememberLastPage,
                @LastPage,          @EnableMemoryOptimization,
                @TrayIconPathRunning, @TrayIconPathPaused,
                @UseMenuAnimation,   @IsDarkTheme
            );";
            public const string UpdateConvention = @"
            UPDATE Convention SET
                AutoStart = @AutoStart,
                ShowNotification = @ShowNotification,
                ShowAddImage = @ShowAddImage,
                HideTooltip = @HideTooltip,
                LongPressThreshold = @LongPressThreshold,
                MouseMovePixels = @MouseMovePixels,
                LoopPageFlipping = @LoopPageFlipping,
                RememberLastPage = @RememberLastPage,
                EnableMemoryOptimization = @EnableMemoryOptimization,
                TrayIconPathRunning = @TrayIconPathRunning,
                TrayIconPathPaused = @TrayIconPathPaused,
                UseMenuAnimation = @UseMenuAnimation,
                IsDarkTheme = @IsDarkTheme
            WHERE ID = 1;";
            public const string GetAllConventions = "SELECT * FROM Convention;";
            // 打开主窗口设置表
            public const string CreateOpenMainWindowTable = @"
            CREATE TABLE IF NOT EXISTS OpenMainWindow
            (
                ID INTEGER PRIMARY KEY AUTOINCREMENT,
                OpenMainWindowByMiddleMouseClick BOOLEAN,
                OpenMainWindowByX1MouseClick BOOLEAN,
                OpenMainWindowByX2MouseClick BOOLEAN,
                OpenMainWindowByCtrl_MiddleMouseClick BOOLEAN,
                OpenMainWindowByCtrl_RightMouseClick BOOLEAN,
                OpenMainWindowByMiddleMouseClickLonger BOOLEAN,
                OpenMainWindowByRightMouseClickLonger BOOLEAN,
                OpenMainWindowByRightMouseClick_Move BOOLEAN,
                OpenMainWindowByCtrl BOOLEAN,
                WindowStartupLocation INTEGER
            );";
            public const string InsertOpenMainWindow = @"
            INSERT INTO OpenMainWindow
            (
                OpenMainWindowByMiddleMouseClick,
                OpenMainWindowByX1MouseClick,
                OpenMainWindowByX2MouseClick,
                OpenMainWindowByCtrl_MiddleMouseClick,
                OpenMainWindowByCtrl_RightMouseClick,
                OpenMainWindowByMiddleMouseClickLonger,
                OpenMainWindowByRightMouseClickLonger,
                OpenMainWindowByRightMouseClick_Move,
                OpenMainWindowByCtrl,
                WindowStartupLocation
            )
            VALUES
            (
                @OpenMainWindowByMiddleMouseClick,
                @OpenMainWindowByX1MouseClick,
                @OpenMainWindowByX2MouseClick,
                @OpenMainWindowByCtrl_MiddleMouseClick,
                @OpenMainWindowByCtrl_RightMouseClick,
                @OpenMainWindowByMiddleMouseClickLonger,
                @OpenMainWindowByRightMouseClickLonger,
                @OpenMainWindowByRightMouseClick_Move,
                @OpenMainWindowByCtrl,
                @WindowStartupLocation
            );";
            public const string UpdateOpenMainWindow = @"
            UPDATE OpenMainWindow SET
                OpenMainWindowByMiddleMouseClick = @OpenMainWindowByMiddleMouseClick,
                OpenMainWindowByX1MouseClick = @OpenMainWindowByX1MouseClick,
                OpenMainWindowByX2MouseClick = @OpenMainWindowByX2MouseClick,
                OpenMainWindowByCtrl_MiddleMouseClick = @OpenMainWindowByCtrl_MiddleMouseClick,
                OpenMainWindowByCtrl_RightMouseClick = @OpenMainWindowByCtrl_RightMouseClick,
                OpenMainWindowByMiddleMouseClickLonger = @OpenMainWindowByMiddleMouseClickLonger,
                OpenMainWindowByRightMouseClickLonger = @OpenMainWindowByRightMouseClickLonger,
                OpenMainWindowByRightMouseClick_Move = @OpenMainWindowByRightMouseClick_Move,
                OpenMainWindowByCtrl = @OpenMainWindowByCtrl,
                WindowStartupLocation = @WindowStartupLocation
            WHERE ID = 1;";
            public const string GetAllOpenMainWindowConditions = "SELECT * FROM OpenMainWindow;";
            // 黑名单设置表
            public const string CreateBlacklistTable = @"
            CREATE TABLE IF NOT EXISTS Blacklist
            (
                ID INTEGER PRIMARY KEY AUTOINCREMENT,
                IsFullScreenDisabled BOOLEAN,
                IsBlacklistEnabledForExtendedHotkey BOOLEAN
            );";
            public const string InsertBlacklist = @"
            INSERT INTO Blacklist
            (
                IsFullScreenDisabled,
                IsBlacklistEnabledForExtendedHotkey
            )
            VALUES
            (
                @IsFullScreenDisabled,
                @IsBlacklistEnabledForExtendedHotkey
            );";
            public const string UpdateBlacklist = @"
            UPDATE Blacklist SET
                IsFullScreenDisabled = @IsFullScreenDisabled,
                IsBlacklistEnabledForExtendedHotkey = @IsBlacklistEnabledForExtendedHotkey
            WHERE ID = 1;";
            public const string GetAllBlacklistSettings = "SELECT * FROM Blacklist;";
            // 黑名单应用表
            public const string CreateBlacklistApplicationTable = @"
            CREATE TABLE IF NOT EXISTS BlacklistApplication
            (
                ID INTEGER PRIMARY KEY AUTOINCREMENT,
                ApplicationName TEXT,
                ProcessName TEXT,
                IsInBlacklist BOOLEAN,
                IsFolder BOOLEAN
            );";
            public const string InsertBlacklistApplication = @"
            INSERT INTO BlacklistApplication
            (
                ApplicationName,
                ProcessName,
                IsInBlacklist,
                IsFolder
            )
            VALUES
            (
                @ApplicationName,
                @ProcessName,
                @IsInBlacklist,
                @IsFolder
            );";
            public const string DeleteBlacklistApplication = @"
            DELETE FROM BlacklistApplication WHERE ApplicationName = @ApplicationName;";
            public const string GetAllBlacklistApplications = "SELECT * FROM BlacklistApplication;";
            // 其他语句
            public const string UpdateTotalUsageTime = @"
            UPDATE Convention SET 
                TotalUsageTime = @TotalUsageTime 
            WHERE ID = 1;"; // 更新使用总时长
            public const string UpdateLastPage = @"
            UPDATE Convention SET 
                LastPage = @LastPage
            WHERE ID = 1;";
            // 外观设置表
            public const string CreateAppearanceTable = @"
            CREATE TABLE IF NOT EXISTS Appearance
            (
                ThemeName TEXT PRIMARY KEY NOT NULL,
                ButtonSize REAL,
                ButtonGap REAL,
                BorderWidth REAL,
                ButtonCornerRadius REAL,
                BackgroundColor TEXT,
                BorderColor TEXT,
                ToolbarColor TEXT,
                ToolbarIconColor TEXT,
                ActionButtonColor TEXT,
                ActionButtonMouseOverColor TEXT,
                BlankButtonColor TEXT,
                BlankButtonMouseOverColor TEXT,
                ButtonTextColor TEXT,
                Font1 INTEGER,
                Font2 INTEGER,
                FontSize REAL,
                FontWeight INTEGER,
                BackgroundImagePath TEXT,
                BackgroundImageOpacity REAL,
                Blur INTEGER,
                Win11CornerRadius INTEGER,
                AutoHideTitleBar BOOLEAN,
                ShowActionButtonMouseOver BOOLEAN,
                HideActionNameAfterIcon BOOLEAN,
                ShowActionIconShadow BOOLEAN,
                EnablePreview BOOLEAN
            );";
            public const string InsertAppearance = @"
            INSERT INTO Appearance
            (
                ThemeName,
                ButtonSize,
                ButtonGap,
                BorderWidth,
                ButtonCornerRadius,
                BackgroundColor,
                BorderColor,
                ToolbarColor,
                ToolbarIconColor,
                ActionButtonColor,
                ActionButtonMouseOverColor,
                BlankButtonColor,
                BlankButtonMouseOverColor,
                ButtonTextColor,
                Font1,
                Font2,
                FontSize,
                FontWeight,
                BackgroundImagePath,
                BackgroundImageOpacity,
                Blur,
                Win11CornerRadius,
                AutoHideTitleBar,
                ShowActionButtonMouseOver,
                HideActionNameAfterIcon,
                ShowActionIconShadow,
                EnablePreview
            )
            VALUES
            (
                @ThemeName,
                @ButtonSize,
                @ButtonGap,
                @BorderWidth,
                @ButtonCornerRadius,
                @BackgroundColor,
                @BorderColor,
                @ToolbarColor,
                @ToolbarIconColor,
                @ActionButtonColor,
                @ActionButtonMouseOverColor,
                @BlankButtonColor,
                @BlankButtonMouseOverColor,
                @ButtonTextColor,
                @Font1,
                @Font2,
                @FontSize,
                @FontWeight,
                @BackgroundImagePath,
                @BackgroundImageOpacity,
                @Blur,
                @Win11CornerRadius,
                @AutoHideTitleBar,
                @ShowActionButtonMouseOver,
                @HideActionNameAfterIcon,
                @ShowActionIconShadow,
                @EnablePreview
            );"; // 插入外观设置
            public const string GetAppearanceSettings = "SELECT * FROM Appearance WHERE ThemeName = @ThemeName;"; // 获取所有外观设置
            public const string UpdateAppearance = @"
            UPDATE Appearance SET
                ButtonSize = @ButtonSize,
                ButtonGap = @ButtonGap,
                BorderWidth = @BorderWidth,
                ButtonCornerRadius = @ButtonCornerRadius,
                BackgroundColor = @BackgroundColor,
                BorderColor = @BorderColor,
                ToolbarColor = @ToolbarColor,
                ToolbarIconColor = @ToolbarIconColor,
                ActionButtonColor = @ActionButtonColor,
                ActionButtonMouseOverColor = @ActionButtonMouseOverColor,
                BlankButtonColor = @BlankButtonColor,
                BlankButtonMouseOverColor = @BlankButtonMouseOverColor,
                ButtonTextColor = @ButtonTextColor,
                Font1 = @Font1,
                Font2 = @Font2,
                FontSize = @FontSize,
                FontWeight = @FontWeight,
                BackgroundImagePath = @BackgroundImagePath,
                BackgroundImageOpacity = @BackgroundImageOpacity,
                Blur = @Blur,
                Win11CornerRadius = @Win11CornerRadius,
                AutoHideTitleBar = @AutoHideTitleBar,
                ShowActionButtonMouseOver = @ShowActionButtonMouseOver,
                HideActionNameAfterIcon = @HideActionNameAfterIcon,
                ShowActionIconShadow = @ShowActionIconShadow,
                EnablePreview = @EnablePreview
            WHERE ThemeName = @ThemeName;"; // 更新外观设置
        }
    }
}