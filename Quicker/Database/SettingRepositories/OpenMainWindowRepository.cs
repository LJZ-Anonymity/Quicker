using Quicker.Models.Settings;
using Quicker.Database.Core;
using System.Data.SQLite;

namespace Quicker.Database.SettingRepositories
{
    /// <summary>
    /// OpenMainWindow 表数据访问：打开主窗口的触发方式设置
    /// </summary>
    public static class OpenMainWindowRepository
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

        /// <summary>
        /// 更新OpenMainWindow设置信息
        /// </summary>
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

        // SQL 语句
        private static class SQLStatements
        {
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
        }
    }
}