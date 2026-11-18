using Quicker.Database.Core;
using Quicker.Models.Settings;
using System.Data.SQLite;

namespace Quicker.Database.SettingRepositories
{
    /// <summary>
    /// Convention 表数据访问：常规设置、LastPage、TotalUsageTime
    /// </summary>
    public static class ConventionRepository
    {
        /// <summary>
        /// 打开数据库连接
        /// </summary>
        public static SQLiteConnection OpenConnection() => DatabaseHelper.OpenConnection("Setting.db");

        /// <summary>
        /// 建表并插入默认数据（仅在数据库文件首次创建时调用）
        /// </summary>
        public static void EnsureInitialized()
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

        /// <summary>
        /// 更新Convention设置信息
        /// </summary>
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
        public static void RecordLastPage(int lastPage)
        {
            using var connection = OpenConnection(); // 打开数据库连接
            using var command = new SQLiteCommand(SQLStatements.UpdateLastPage, connection); // 创建 SQLiteCommand 对象
            command.Parameters.AddWithValue("@LastPage", lastPage); // 绑定参数
            command.ExecuteNonQuery(); // 执行更新命令
        }

        /// <summary>
        /// 保存总使用时长
        /// </summary>
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

        // SQL 语句
        private static class SQLStatements
        {
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
            public const string UpdateTotalUsageTime = @"
            UPDATE Convention SET
                TotalUsageTime = @TotalUsageTime
            WHERE ID = 1;"; // 更新使用总时长
            public const string UpdateLastPage = @"
            UPDATE Convention SET
                LastPage = @LastPage
            WHERE ID = 1;";
        }
    }
}