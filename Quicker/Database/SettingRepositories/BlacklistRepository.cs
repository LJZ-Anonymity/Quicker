using Quicker.Models.Settings;
using Quicker.Database.Core;
using System.Data.SQLite;

namespace Quicker.Database.SettingRepositories
{
    /// <summary>
    /// Blacklist 与 BlacklistApplication 表数据访问：黑名单设置与黑名单应用
    /// </summary>
    public static class BlacklistRepository
    {
        /// <summary>
        /// 打开数据库连接
        /// </summary>
        public static SQLiteConnection OpenConnection() => DatabaseHelper.OpenConnection("Setting.db");

        /// <summary>
        /// 建 Blacklist 表并插入默认数据（仅在数据库文件首次创建时调用）
        /// </summary>
        public static void EnsureBlacklistInitialized()
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

        /// <summary>
        /// 建 BlacklistApplication 表（仅在数据库文件首次创建时调用）
        /// </summary>
        public static void EnsureBlacklistApplicationInitialized()
        {
            using var connection = OpenConnection(); // 打开数据库连接
            using var createBlacklistApplicationCommand = new SQLiteCommand(SQLStatements.CreateBlacklistApplicationTable, connection); // 创建 SQLiteCommand 对象
            createBlacklistApplicationCommand.ExecuteNonQuery(); // 执行创建表的命令
        }

        /// <summary>
        /// 更新Blacklist设置信息
        /// </summary>
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

        // SQL 语句
        private static class SQLStatements
        {
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
        }
    }
}