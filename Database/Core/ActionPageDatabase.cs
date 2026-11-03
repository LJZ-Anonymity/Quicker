using System.Collections.Generic;
using System.Data.SQLite;
using Quicker.Helpers;
using Quicker.Models;
using System.IO;

namespace Quicker.Database.Core
{
    public class ActionPageDatabase
    {
        // 使用AppPathHelper获取数据库连接字符串
        private static string GetConnectionString()
        {
            return DatabaseHelper.GetConnectionString("ActionPage.db");
        }

        // 确保数据库目录存在
        private static void EnsureDatabaseDirectoryExists()
        {
            DatabaseHelper.EnsureDatabaseDirectoryExists();
        }

        // 数据库连接
        private readonly ButtonDatabase db2 = new(); // 按钮数据库

        public ActionPageDatabase()
        {
            EnsureDatabaseDirectoryExists(); // 确保数据库目录存在
            string dbFilePath = Path.Combine(AppPathHelper.DatabaseFolder, "ActionPage.db"); // 设置数据库文件路径
            if (!File.Exists(dbFilePath))
            {
                SQLiteConnection.CreateFile(dbFilePath); // 创建数据库文件
                InitializeDatabase(); // 初始化数据库表
            }
        }

        public SQLiteConnection OpenConnection()
        {
            return DatabaseHelper.OpenConnection("ActionPage.db");
        }

        // 初始化数据库
        private static void InitializeDatabase()
        {
            using var connection = new SQLiteConnection(GetConnectionString()); // 打开数据库连接
            connection.Open();
            CreateScenesTable(connection); // 创建数据表
            CreateAndInitTableStatic(connection, "_global"); // 创建数据表并初始化
            CreateAndInitTableStatic(connection, "common"); // 创建数据表并初始化
            CreateAndInitTableStatic(connection, "taskbar"); // 创建数据表并初始化
            CreateAndInitTableStatic(connection, "desktop"); // 创建数据表并初始化

            var db = new ActionPageDatabase(); // 创建实例
            db.CreateAndInitTable("_global"); // 创建动作页数据表
            db.CreateAndInitTable("common"); // 创建动作页数据表
        }

        // 创建Scenes表
        private static void CreateScenesTable(SQLiteConnection connection)
        {
            using var transaction = connection.BeginTransaction(); // 开启事务
            string createTableQuery = @"
                CREATE TABLE IF NOT EXISTS Scenes (
                    SceneName TEXT,
                    SceneIconPath TEXT,
                    SceneCount INTEGER,
                    SceneTag TEXT PRIMARY KEY,
                    AutoReturnToFirstPage BOOL,
                    SceneProcess TEXT
            );"; // 创建Scenes表的SQL语句
            using var command = new SQLiteCommand(createTableQuery, connection, transaction); // 创建SQLiteCommand对象
            command.ExecuteNonQuery(); // 执行创建表的SQL语句
            transaction.Commit(); // 提交事务
        }

        // 场景配置数据
        private static class SceneConfig
        {
            public static readonly Dictionary<string, (string Process, string ActionPageName, string IconPath, string Tag)> Configs = new()
            {
                ["_global"] = ("Default", "默认全局动作页", "/Resources/Images/GlobalSceneImage.png", "_global"),
                ["common"] = ("Default", "默认", "/Resources/Images/CommonSceneImage.png", "common"),
                ["desktop"] = ("Windows桌面", "桌面", "/Resources/Images/DesktopSceneImage.png", "desktop"),
                ["taskbar"] = ("Windows任务栏", "任务栏", "/Resources/Images/TaskbarSceneImage.png", "taskbar")
            };
        }

        /// <summary>
        /// 创建并初始化数据表
        /// </summary>
        /// <param name="connection"> 数据库连接 </param>
        /// <param name="sceneTag"> 场景标签 </param>
        private static void CreateAndInitTableStatic(SQLiteConnection connection, string sceneTag)
        {
            // 获取场景配置
            var (defaultSceneProcess, actionPageName, defaultIconPath, defaultTag) =
                SceneConfig.Configs.GetValueOrDefault(sceneTag, (string.Empty, string.Empty, "", sceneTag));

            string sceneIconPath = defaultIconPath;
            string sceneName = defaultTag;
            string sceneProcess = defaultSceneProcess;

            // 创建场景数据
            var sceneData = new SceneData
            {
                SceneName = sceneName,
                SceneIconPath = sceneIconPath,
                SceneCount = 0,
                SceneTag = sceneTag,
                AutoReturnToFirstPage = false,
                SceneProcess = sceneProcess
            };

            // 插入场景数据
            InsertSceneData(connection, sceneData);
        }

        // 插入场景数据
        private static void InsertSceneData(SQLiteConnection connection, SceneData sceneData)
        {
            string insertQuery = @"
                INSERT OR REPLACE INTO Scenes (SceneName, SceneIconPath, SceneCount, SceneTag, AutoReturnToFirstPage, SceneProcess)
                VALUES (@SceneName, @SceneIconPath, @SceneCount, @SceneTag, @AutoReturnToFirstPage, @SceneProcess)";
            using var command = new SQLiteCommand(insertQuery, connection);
            command.Parameters.AddWithValue("@SceneName", sceneData.SceneName);
            command.Parameters.AddWithValue("@SceneIconPath", sceneData.SceneIconPath);
            command.Parameters.AddWithValue("@SceneCount", sceneData.SceneCount);
            command.Parameters.AddWithValue("@SceneTag", sceneData.SceneTag);
            command.Parameters.AddWithValue("@AutoReturnToFirstPage", sceneData.AutoReturnToFirstPage);
            command.Parameters.AddWithValue("@SceneProcess", sceneData.SceneProcess);
            command.ExecuteNonQuery();
        }

        /// <summary>
        /// 创建场景数据表并初始化
        /// </summary>
        /// <param name="sceneTag">场景标签</param>
        /// <param name="sceneIconPath">场景图标路径</param>
        /// <param name="sceneName">场景名称</param>
        /// <param name="sceneProcess">场景所属进程</param>
        public void CreateAndInitTable(string sceneTag, string sceneIconPath = "", string sceneName = "", string sceneProcess = "")
        {
            // 获取场景配置
            var (defaultSceneProcess, actionPageName, defaultIconPath, defaultTag) =
                SceneConfig.Configs.GetValueOrDefault(sceneTag, (string.Empty, string.Empty, sceneIconPath, sceneTag));
            if (new List<string> { "_global", "common", "taskbar", "desktop" }.Contains(sceneTag)) // 如果是默认场景
            {
                sceneIconPath = string.IsNullOrEmpty(sceneIconPath) ? defaultIconPath : sceneIconPath; // 场景图标路径
                sceneName = string.IsNullOrEmpty(sceneName) ? defaultTag : sceneName; // 场景名称
                sceneProcess = string.IsNullOrEmpty(sceneProcess) ? defaultSceneProcess : sceneProcess; // 场景所属应用程序名称
            }

            // 获取动作页数量
            int actionPageCount = db2.TableExists(sceneTag) ? db2.GetTotalAntionPageIndex(sceneTag) : 0;
            var sceneData = new SceneData
            {
                SceneName = sceneName, // 不带后缀的文件名
                SceneIconPath = sceneIconPath,
                SceneCount = actionPageCount,
                SceneTag = sceneTag,
                AutoReturnToFirstPage = false,
                SceneProcess = sceneProcess
            }; // 创建并初始化场景数据
            UpdateSceneTable(sceneData); // 更新场景数据表

            // 创建并初始化动作页
            for (int i = 0; i < actionPageCount; i++) // 创建动作页
            {
                CreateActionPageTable(sceneTag); // 创建动作页数据表
                string currentActionPageName = sceneTag switch // 获取动作页名称
                {
                    "_global" or "common" => actionPageName,
                    _ => $"{actionPageName}{i}"
                };

                UpdateActionPageTable(sceneTag, $"{sceneTag}{i}", currentActionPageName); // 更新动作页数据表
            }
        }

        /// <summary>
        /// 更新场景数据表
        /// </summary>
        /// <param name="sceneData"> 场景数据对象 </param>
        public void UpdateSceneTable(SceneData sceneData)
        {
            using var connection = OpenConnection(); // 打开数据库连接
            using var transaction = connection.BeginTransaction(); // 开启事务
            string query = $@"INSERT OR REPLACE INTO Scenes
            (SceneName, SceneIconPath, SceneCount, SceneTag, AutoReturnToFirstPage, SceneProcess)
            VALUES
            (@SceneName, @SceneIconPath, @SceneCount, @SceneTag, @AutoReturnToFirstPage, @SceneProcess)"; // 更新场景数据表的SQL语句
            using var command = new SQLiteCommand(query, connection, transaction); // 创建SQLiteCommand对象
            command.Parameters.AddWithValue("@SceneName", sceneData.SceneName); // 场景类型
            command.Parameters.AddWithValue("@SceneIconPath", sceneData.SceneIconPath); // 场景图标路径
            command.Parameters.AddWithValue("@SceneCount", sceneData.SceneCount); // 场景数量
            command.Parameters.AddWithValue("@SceneTag", sceneData.SceneTag); // 场景标签
            command.Parameters.AddWithValue("@AutoReturnToFirstPage", sceneData.AutoReturnToFirstPage); // 是否自动返回到第一个页面
            command.Parameters.AddWithValue("@SceneProcess", sceneData.SceneProcess); // 动作页所属应用程序名称
            command.ExecuteNonQuery(); // 执行更新表的SQL语句
            transaction.Commit(); // 提交事务
        }

        /// <summary>
        /// 设置是否自动返回第一页
        /// </summary>
        /// <param name="sceneTag"> 场景标签 </param>
        /// <param name="autoReturnToFirstPage"> 是否自动返回到第一个页面 </param>
        public void SetAutoReturnToFirstPage(string sceneTag, bool autoReturnToFirstPage)
        {
            using var connection = OpenConnection(); // 打开数据库连接
            using var transaction = connection.BeginTransaction(); // 开启事务
            string query = $@"UPDATE Scenes SET AutoReturnToFirstPage = @AutoReturnToFirstPage WHERE SceneTag = @SceneTag"; // 更新场景数据表的SQL语句
            using var command = new SQLiteCommand(query, connection, transaction); // 创建SQLiteCommand对象
            command.Parameters.AddWithValue("@AutoReturnToFirstPage", autoReturnToFirstPage); // 设置参数
            command.Parameters.AddWithValue("@SceneTag", sceneTag); // 设置场景类型
            command.ExecuteNonQuery(); // 执行更新表的SQL语句
            transaction.Commit(); // 提交事务
        }

        /// <summary>
        /// 获取是否自动返回第一页
        /// </summary>
        /// <param name="sceneTag"> 场景标签 </param>
        /// <returns> 是否自动返回到第一个页面 </returns>
        public bool GetAutoReturnToFirstPage(string sceneTag)
        {
            using var connection = OpenConnection(); // 打开数据库连接
            using var transaction = connection.BeginTransaction(); // 开启事务
            string query = $@"SELECT AutoReturnToFirstPage FROM Scenes WHERE SceneTag = @SceneTag"; // 获取场景数据表的SQL语句
            using var command = new SQLiteCommand(query, connection, transaction); // 创建SQLiteCommand对象
            command.Parameters.AddWithValue("@SceneTag", sceneTag); // 添加参数
            using var reader = command.ExecuteReader(); // 执行查询SQL语句
            bool autoReturnToFirstPage = false; // 自动返回到第一个页面
            while (reader.Read())
            {
                autoReturnToFirstPage = reader.GetBoolean(0); // 获取自动返回到第一个页面
            }
            return autoReturnToFirstPage; // 返回自动返回到第一个页面
        }

        /// <summary>
        /// 更新场景数量
        /// </summary>
        /// <param name="sceneTag"> 场景标签 </param>
        /// <param name="sceneCount"> 场景数量 </param>
        public void UpdateSceneCount(string sceneTag, int sceneCount)
        {
            using var connection = OpenConnection(); // 打开数据库连接
            using var transaction = connection.BeginTransaction(); // 开启事务
            string query = $@"UPDATE Scenes SET SceneCount = @SceneCount WHERE SceneTag = @SceneTag"; // 更新场景数据表的SQL语句
            using var command = new SQLiteCommand(query, connection, transaction); // 创建SQLiteCommand对象
            command.Parameters.AddWithValue("@SceneCount", sceneCount); // 设置参数
            command.Parameters.AddWithValue("@SceneTag", sceneTag); // 设置场景类型
            command.ExecuteNonQuery(); // 执行更新表的SQL语句
            transaction.Commit(); // 提交事务
        }

        /// <summary>
        /// 删除场景数据
        /// </summary>
        /// <param name="sceneTag"> 场景标签 </param>
        public void DeleteScene(string sceneTag)
        {
            using var connection = OpenConnection(); // 打开数据库连接
            using var transaction = connection.BeginTransaction(); // 开启事务
            string query = $@"DELETE FROM Scenes WHERE SceneTag = @SceneTag"; // 删除场景数据表的SQL语句
            using var command1 = new SQLiteCommand(query, connection, transaction); // 创建SQLiteCommand对象
            command1.Parameters.AddWithValue("@SceneTag", sceneTag); // 设置参数
            command1.ExecuteNonQuery(); // 执行删除表的SQL语句
            transaction.Commit(); // 提交事务
        }

        /// <summary>
        /// 判断Scenes表中是否存在指定SceneTag的数据
        /// </summary>
        /// <param name="sceneTag">场景标签</param>
        /// <returns>是否存在</returns>
        public bool SceneExists(string sceneTag)
        {
            using var connection = OpenConnection(); // 打开数据库连接
            string query = "SELECT 1 FROM Scenes WHERE SceneTag = @SceneTag;";
            using var command = new SQLiteCommand(query, connection); // 创建SQLiteCommand对象
            command.Parameters.AddWithValue("@SceneTag", sceneTag);
            using var reader = command.ExecuteReader();
            return reader.HasRows;
        }

        /// <summary>
        /// 获取场景数据
        /// </summary>
        /// <param name="sceneTag"> 场景标签 </param>
        /// <returns> 场景数据 </returns>
        public SceneData GetSceneData(string sceneTag)
        {
            using var connection = OpenConnection(); // 打开数据库连接
            string selectQuery = $"SELECT * FROM Scenes WHERE SceneTag = @SceneTag;"; // 获取场景数据表的SQL语句
            using var command = new SQLiteCommand(selectQuery, connection); // 创建SQLiteCommand对象
            command.Parameters.AddWithValue("@SceneTag", sceneTag); // 添加参数
            using var reader = command.ExecuteReader(); // 执行查询SQL语句
            if (reader.Read())
            {
                return new SceneData
                {
                    SceneName = reader.GetString(0), // 场景类型
                    SceneIconPath = reader.GetString(1), // 场景图标路径
                    SceneCount = reader.GetInt32(2), // 场景数量
                    SceneTag = reader.GetString(3), // 场景标签
                    AutoReturnToFirstPage = reader.GetBoolean(4), // 是否自动返回第一个页面
                    SceneProcess = reader.GetString(5), // 动作页所属应用程序名称
                }; // 添加场景数据
            }
            return null;
        }

        /// <summary>
        /// 获取所有场景数据
        /// </summary>
        /// <returns> 所有场景数据 </returns>
        public List<SceneData> GetAllSceneData()
        {
            var conditions = new List<SceneData>(); // 场景数据表
            using var connection = OpenConnection(); // 打开数据库连接
            string selectQuery = "SELECT * FROM Scenes"; // 获取所有场景数据表的SQL语句
            using var command = new SQLiteCommand(selectQuery, connection); // 创建SQLiteCommand对象
            using var reader = command.ExecuteReader(); // 执行查询SQL语句
            while (reader.Read()) // 获取场景数据
            {
                conditions.Add(new SceneData
                {
                    SceneName = reader.GetString(0), // 场景类型
                    SceneIconPath = reader.GetString(1), // 场景图标路径
                    SceneCount = reader.GetInt32(2), // 场景数量
                    SceneTag = reader.GetString(3), // 场景标签
                    AutoReturnToFirstPage = reader.GetBoolean(4), // 是否自动返回第一个页面
                    SceneProcess = reader.GetString(5), // 动作页所属应用程序名称
                }); // 添加场景数据
            }
            return conditions; // 返回所有场景数据
        }

        /// <summary>
        /// 创建动作页数据表
        /// </summary>
        /// <param name="tableName"> 动作页名称 </param>
        public void CreateActionPageTable(string tableName)
        {
            using var connection = OpenConnection(); // 打开数据库连接
            using var transaction = connection.BeginTransaction(); // 开启事务
            string createTableQuery = $@"CREATE TABLE IF NOT EXISTS [{tableName + "ActionPage"}]
            (
                DefaultActionPageName TEXT PRIMARY KEY,
                ActionPageName TEXT,
                LastEditTime DATETIME
            );"; // 创建场景数据表的SQL语句
            using var command = new SQLiteCommand(createTableQuery, connection); // 创建SQLiteCommand对象
            command.ExecuteNonQuery(); // 执行创建表的SQL语句
            transaction.Commit(); // 提交事务
        }

        /// <summary>
        /// 更新动作页数据
        /// </summary>
        /// <param name="tableName"> 动作页名称 </param>
        /// <param name="defaultActionPageName"> 动作页默认名称 </param>
        /// <param name="actionPageName"> 动作页名称 </param>
        public void UpdateActionPageTable(string tableName, string defaultActionPageName, string actionPageName)
        {
            using var connection = OpenConnection(); // 打开数据库连接
            using var transaction = connection.BeginTransaction(); // 开启事务
            string query = $@"INSERT OR REPLACE INTO [{tableName + "ActionPage"}]
            (DefaultActionPageName, ActionPageName, LastEditTime)
            VALUES
            (@DefaultActionPageName, @ActionPageName, @LastEditTime)"; // 更新场景数据表的SQL语句
            using var command = new SQLiteCommand(query, connection, transaction); // 创建SQLiteCommand对象
            command.Parameters.AddWithValue("@DefaultActionPageName", defaultActionPageName); // 动作页内置默认名称
            command.Parameters.AddWithValue("@ActionPageName", actionPageName); // 场景图标路径
            command.Parameters.AddWithValue("@LastEditTime", DateTime.Now); // 场景数量
            command.ExecuteNonQuery(); // 执行更新表的SQL语句
            transaction.Commit(); // 提交事务
        }

        /// <summary>
        /// 删除动作页数据
        /// </summary>
        /// <param name="tableName"> 动作页数据表名称 </param>
        /// <param name="actionPageIndex"> 动作页索引 </param>
        public void DeleteActionPage(string tableName, int actionPageIndex)
        {
            using var connection = OpenConnection(); // 打开数据库连接
            using var transaction = connection.BeginTransaction(); // 开启事务
            string query = $@"DELETE FROM [{tableName + "ActionPage"}] WHERE DefaultActionPageName = @DefaultActionPageName"; // 删除动作页数据表的SQL语句
            using var command1 = new SQLiteCommand(query, connection, transaction); // 创建SQLiteCommand对象
            command1.Parameters.AddWithValue("@DefaultActionPageName", tableName + actionPageIndex.ToString()); // 添加参数
            command1.ExecuteNonQuery(); // 执行删除表的SQL语句

            string selectQuery = $@"SELECT DefaultActionPageName FROM [{tableName + "ActionPage"}] 
                    ORDER BY CAST(SUBSTR(DefaultActionPageName, LENGTH(@TableName) + 1) AS INTEGER)"; // 获取所有剩余动作页的 SQL语句
            using var selectCommand = new SQLiteCommand(selectQuery, connection, transaction); // 创建SQLiteCommand对象
            selectCommand.Parameters.AddWithValue("@TableName", tableName); // 设置参数
            using var reader = selectCommand.ExecuteReader(); // 执行查询SQL语句
            List<string> defaultNames = new List<string>(); // 所有剩余动作页的 DefaultActionPageName
            while (reader.Read())
            {
                defaultNames.Add(reader.GetString(0)); // 获取所有剩余动作页的 DefaultActionPageName
            }

            for (int i = 0; i < defaultNames.Count; i++) // 重新编号并更新每个动作页的 DefaultActionPageName
            {
                string oldDefaultActionPageName = defaultNames[i]; // 获取旧名称
                string newDefaultActionPageName = $"{tableName}{i}"; // 设置新名称
                if (oldDefaultActionPageName != newDefaultActionPageName)
                {
                    string updateQuery = $@"UPDATE [{tableName + "ActionPage"}]
                            SET DefaultActionPageName = @NewDefaultActionPageName
                            WHERE DefaultActionPageName = @OldDefaultActionPageName"; // 更新动作页数据表的SQL语句
                    using var updateCommand = new SQLiteCommand(updateQuery, connection, transaction); // 创建SQLiteCommand对象
                    updateCommand.Parameters.AddWithValue("@NewDefaultActionPageName", newDefaultActionPageName); // 设置新名称
                    updateCommand.Parameters.AddWithValue("@OldDefaultActionPageName", oldDefaultActionPageName); // 设置旧名称
                    updateCommand.ExecuteNonQuery(); // 执行更新表的SQL语句
                }
            }
            query = $@"UPDATE Scenes SET SceneCount = SceneCount - 1 WHERE SceneTag = @SceneTag"; // 更新场景数据表的SQL语句
            using var command2 = new SQLiteCommand(query, connection, transaction); // 创建SQLiteCommand对象
            command2.Parameters.AddWithValue("@SceneTag", tableName); // 设置场景类型
            command2.ExecuteNonQuery(); // 执行更新表的SQL语句
            transaction.Commit(); // 提交事务
        }

        /// <summary>
        /// 删除动作页数据表
        /// </summary>
        /// <param name="tableName"> 动作页数据表名称 </param>
        public void DeleteActionPageTable(string tableName)
        {
            using var connection = OpenConnection(); // 打开数据库连接
            using var command = new SQLiteCommand($"DROP TABLE IF EXISTS [{tableName + "ActionPage"}]", connection); // 创建命令对象
            command.ExecuteNonQuery(); // 执行删除表格语句
        }

        /// <summary>
        /// 获取动作页数据
        /// </summary>
        /// <param name="tableName"> 动作页数据表名称 </param>
        /// <param name="actionPageIndex"> 动作页索引 </param>
        /// <returns> 动作页数据 </returns>
        public ActionPageData GetActionPageData(string tableName, int actionPageIndex)
        {
            using var connection = OpenConnection();
            string table = tableName + "ActionPage";

            using (var check = new SQLiteCommand(
                "SELECT 1 FROM sqlite_master WHERE type='table' AND name=@n", connection))
            {
                check.Parameters.AddWithValue("@n", table);
                if (check.ExecuteScalar() == null) return null; // 表不存在当作没有数据
            }

            string selectQuery = $"SELECT * FROM [{table}] WHERE DefaultActionPageName = @DefaultActionPageName";
            using var command = new SQLiteCommand(selectQuery, connection); // 创建SQLiteCommand对象
            command.Parameters.AddWithValue("@DefaultActionPageName", tableName + actionPageIndex.ToString()); // 添加参数
            using var reader = command.ExecuteReader(); // 执行查询SQL语句
            if (reader.Read())
            {
                return new ActionPageData
                {
                    DefaultActionPageName = reader.GetString(0), // 内部默认的动作页名称，例如"Global0"
                    ActionPageName = reader.GetString(1), // 动作页名称
                    LastEditTime = reader.GetDateTime(2), // 最后编辑时间
                }; // 返回动作页数据
            }
            return null; // 返回空
        }

        /// <summary>
        /// 交换两个动作页的数据
        /// </summary>
        /// <param name="tableName"> 动作页数据表名称 </param>
        /// <param name="index1"> 动作页索引1 </param>
        /// <param name="index2"> 动作页索引2 </param>
        public void SwapActionPage(string tableName, int index1, int index2)
        {
            using var connection = OpenConnection(); // 打开数据库连接
            using var transaction = connection.BeginTransaction(); // 开启事务
            string actionPage1Name = $"{tableName}{index1}"; // 获取第一个动作页的名称
            string actionPage2Name = $"{tableName}{index2}"; // 获取第二个动作页的名称
            string tempName = $"{tableName}_temp_swap"; // 生成临时名称

            // 更新第一个动作页为临时名称
            string updateQuery = $@"UPDATE [{tableName + "ActionPage"}]
                                SET DefaultActionPageName = @TempName
                                WHERE DefaultActionPageName = @ActionPage1Name"; // 更新第一个动作页的SQL语句
            using var updateCommand1 = new SQLiteCommand(updateQuery, connection, transaction); // 创建SQLiteCommand对象
            updateCommand1.Parameters.AddWithValue("@TempName", tempName); // 设置临时名称
            updateCommand1.Parameters.AddWithValue("@ActionPage1Name", actionPage1Name); // 设置第一个动作页名称
            updateCommand1.ExecuteNonQuery(); // 执行更新表的SQL语句

            // 更新第二个动作页为第一个动作页的原始名称
            updateQuery = $@"UPDATE [{tableName + "ActionPage"}]
                        SET DefaultActionPageName = @ActionPage1Name
                        WHERE DefaultActionPageName = @ActionPage2Name"; // 更新第二个动作页的SQL语句
            using var updateCommand2 = new SQLiteCommand(updateQuery, connection, transaction); // 创建SQLiteCommand对象
            updateCommand2.Parameters.AddWithValue("@ActionPage1Name", actionPage1Name); // 设置第一个动作页名称
            updateCommand2.Parameters.AddWithValue("@ActionPage2Name", actionPage2Name); // 设置第二个动作页名称
            updateCommand2.ExecuteNonQuery(); // 执行更新表的SQL语句

            // 更新第一个动作页为第二个动作页的原始名称
            updateQuery = $@"UPDATE [{tableName + "ActionPage"}]
                        SET DefaultActionPageName = @ActionPage2Name
                        WHERE DefaultActionPageName = @TempName"; // 更新第一个动作页的SQL语句
            using var updateCommand3 = new SQLiteCommand(updateQuery, connection, transaction); // 创建SQLiteCommand对象
            updateCommand3.Parameters.AddWithValue("@ActionPage2Name", actionPage2Name); // 设置第二个动作页名称
            updateCommand3.Parameters.AddWithValue("@TempName", tempName); // 设置临时名称
            updateCommand3.ExecuteNonQuery(); // 执行更新表的SQL语句
            transaction.Commit(); // 提交事务
        }

        /// <summary>
        /// 生成场景标题
        /// </summary>
        /// <param name="sceneData"> 场景数据 </param>
        /// <returns> 场景标题 </returns>
        public string GetSceneTitle(SceneData sceneData)
        {
            switch (sceneData.SceneName)
            {
                case "_global":
                    return "全局"; // 全局场景
                case "common":
                    return "通用"; // 通用场景
                case "taskbar":
                    return "任务栏"; // 任务栏场景
                case "desktop":
                    return "桌面"; // 桌面场景
                default:
                    return sceneData.SceneName; // 其他场景
            }
        }

        /// <summary>
        /// 更新动作页最后编辑时间
        /// </summary>
        /// <param name="tableName"> 动作页数据表名称 </param>
        /// <param name="actionPageIndex"> 动作页索引 </param>
        public void UpdateActionPageLastEditTime(string tableName, int actionPageIndex)
        {
            using var connection = OpenConnection(); // 打开数据库连接
            string query = $@"UPDATE [{tableName + "ActionPage"}]
            SET LastEditTime = @LastEditTime
            WHERE DefaultActionPageName = @DefaultActionPageName"; // 更新动作页最后编辑时间的SQL语句
            using var command = new SQLiteCommand(query, connection); // 创建SQLiteCommand对象
            command.Parameters.AddWithValue("@LastEditTime", DateTime.Now); // 设置最后编辑时间
            command.Parameters.AddWithValue("@DefaultActionPageName", tableName + actionPageIndex.ToString()); // 设置动作页名称
            command.ExecuteNonQuery(); // 执行更新表的SQL语句
        }

        /// <summary>
        /// 统计动作页大小
        /// </summary>
        /// <param name="tableName"> 动作页数据表名称 </param>
        /// <param name="actionPageIndex"> 动作页索引 </param>
        /// <returns> 动作页大小 </returns>
        public string GetActionPageSize(string tableName, int actionPageIndex)
        {
            using var connection = OpenConnection(); // 打开数据库连接
            var buttonDatas = db2.GetPagesOfButtons(tableName, actionPageIndex); // 获取动作页按钮数据
            int size = 0; // 动作页大小
            foreach (var buttonData in buttonDatas)
            {
                // 计算每个按钮占用的内存大小
                size += buttonData.Title?.Length * 2 ?? 0; // 标题字符串长度 * 2 (UTF-16)
                size += buttonData.Location?.Length * 2 ?? 0; // 位置字符串长度 * 2
                size += buttonData.ImagePath?.Length * 2 ?? 0; // 图片路径字符串长度 * 2
                size += buttonData.Data1?.Length * 2 ?? 0; // 数据1字符串长度 * 2
                size += buttonData.Data2?.Length * 2 ?? 0; // 数据2字符串长度 * 2
                size += buttonData.Data3?.Length * 2 ?? 0; // 数据3字符串长度 * 2
                size += buttonData.Description?.Length * 2 ?? 0; // 描述字符串长度 * 2
                size += 4; // ActionType (enum)
                size += 4; // ButtonID (int)
                size += 8; // CreateTime (DateTime)
                size += 8; // LatestEditTime (DateTime)
                size += 4; // UsedTimes (int)
            }
            using var convertion = new DataSizeHelper(); // 数据转换管理器
            size = convertion.ConversionData(size); // 转换数据
            string sizeString = convertion.ConversionUnits(size); // 转换单位
            return $"{size} {sizeString}"; // 返回动作页大小
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            // 释放资源
        }
    }
}