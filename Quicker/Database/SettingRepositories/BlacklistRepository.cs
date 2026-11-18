using Quicker.Models.Settings;
using Quicker.Database.Core;
using System.Data.SQLite;
using Quicker.Helpers;

namespace Quicker.Database.SettingRepositories
{
    /// <summary>
    /// 添加规则的结果
    /// </summary>
    public enum AddRuleResult
    {
        /// <summary> 已添加 </summary>
        Added,

        /// <summary> 该目标已在同一个名单中 </summary>
        AlreadyExists,

        /// <summary> 该目标已在另一个名单中（同一目标只能属于一个名单，需要的话调用 MoveRule） </summary>
        ExistsInOtherList,

        /// <summary> 目标为空或格式非法 </summary>
        InvalidTarget
    }

    /// <summary>
    /// 黑名单/白名单的数据访问：BlacklistRule（规则）与 BlacklistSetting（设置，单行）两张表
    /// </summary>
    public static class BlacklistRepository
    {
        #region 初始化

        /// <summary>
        /// 创建表并插入默认设置行（可重复调用）。
        /// 在 SettingDatabase 的静态构造函数里、数据库文件不存在时调用。
        /// </summary>
        public static void EnsureBlacklistInitialized()
        {
            // 通过 SettingDatabase.OpenConnection() 打开连接，保证 SettingDatabase 的静态构造函数先执行（建库文件）
            using var connection = SettingDatabase.OpenConnection();
            using var transaction = connection.BeginTransaction();
            EnsureBlacklistApplicationInitialized(connection, transaction, SQL.CreateRuleTable);
            EnsureBlacklistApplicationInitialized(connection, transaction, SQL.CreateSettingTable);
            EnsureBlacklistApplicationInitialized(connection, transaction, SQL.InsertDefaultSetting);
            transaction.Commit();
        }

        private static void EnsureBlacklistApplicationInitialized(SQLiteConnection connection, SQLiteTransaction transaction, string sql)
        {
            using var command = new SQLiteCommand(sql, connection, transaction);
            command.ExecuteNonQuery();
        }

        #endregion

        #region 设置

        /// <summary>
        /// 读取设置（唯一一行）；若不存在则返回默认值
        /// </summary>
        public static BlacklistSetting GetSetting()
        {
            using var connection = SettingDatabase.OpenConnection();
            using var command = new SQLiteCommand(SQL.GetSetting, connection);
            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                return new BlacklistSetting
                {
                    DisableOnFullScreen = reader.GetInt64(reader.GetOrdinal("DisableOnFullScreen")) != 0,
                    ApplyToExtendedHotkey = reader.GetInt64(reader.GetOrdinal("ApplyToExtendedHotkey")) != 0
                };
            }
            return new BlacklistSetting();
        }

        /// <summary>
        /// 保存设置
        /// </summary>
        public static void UpdateSetting(BlacklistSetting setting)
        {
            using var connection = SettingDatabase.OpenConnection();
            using var transaction = connection.BeginTransaction();
            using var command = new SQLiteCommand(SQL.UpdateSetting, connection, transaction);
            command.Parameters.AddWithValue("@DisableOnFullScreen", setting.DisableOnFullScreen ? 1 : 0);
            command.Parameters.AddWithValue("@ApplyToExtendedHotkey", setting.ApplyToExtendedHotkey ? 1 : 0);
            command.ExecuteNonQuery();
            transaction.Commit();
        }

        #endregion

        #region 规则

        /// <summary>
        /// 读取所有规则（按添加顺序）
        /// </summary>
        public static List<BlacklistRule> GetAllRules()
        {
            var rules = new List<BlacklistRule>();
            using var connection = SettingDatabase.OpenConnection();
            using var command = new SQLiteCommand(SQL.GetAllRules, connection);
            using var reader = command.ExecuteReader();

            int idIndex = reader.GetOrdinal("ID");
            int listTypeIndex = reader.GetOrdinal("ListType");
            int matchTypeIndex = reader.GetOrdinal("MatchType");
            int targetIndex = reader.GetOrdinal("Target");

            while (reader.Read())
            {
                rules.Add(new BlacklistRule
                {
                    ID = reader.GetInt32(idIndex),
                    ListType = (BlacklistListType)reader.GetInt32(listTypeIndex),
                    MatchType = (BlacklistMatchType)reader.GetInt32(matchTypeIndex),
                    Target = reader.GetString(targetIndex)
                });
            }
            return rules;
        }

        /// <summary>
        /// 添加规则。目标会先经过统一的规范化；同一目标（匹配方式 + 目标）只能存在一条。
        /// </summary>
        /// <param name="listType">所属名单</param>
        /// <param name="matchType">匹配方式</param>
        /// <param name="target">原始输入（进程名、exe 路径、文件夹路径等）</param>
        /// <param name="rule">新增或已存在的那条规则（InvalidTarget 时为 null）</param>
        public static AddRuleResult TryAddRule(BlacklistListType listType, BlacklistMatchType matchType, string target, out BlacklistRule? rule)
        {
            string normalized = BlacklistRuleNormalizer.Normalize(matchType, target);
            if (matchType != BlacklistMatchType.Unknown && normalized.Length == 0)
            {
                rule = null;
                return AddRuleResult.InvalidTarget;
            }

            using var connection = SettingDatabase.OpenConnection();
            using var transaction = connection.BeginTransaction();

            // 先查是否已存在（唯一键：MatchType + Target，Target 忽略大小写）
            using (var find = new SQLiteCommand(SQL.FindRule, connection, transaction))
            {
                find.Parameters.AddWithValue("@MatchType", (int)matchType);
                find.Parameters.AddWithValue("@Target", normalized);
                using var reader = find.ExecuteReader();
                if (reader.Read())
                {
                    var existing = new BlacklistRule
                    {
                        ID = reader.GetInt32(reader.GetOrdinal("ID")),
                        ListType = (BlacklistListType)reader.GetInt32(reader.GetOrdinal("ListType")),
                        MatchType = matchType,
                        Target = reader.GetString(reader.GetOrdinal("Target"))
                    };
                    rule = existing;
                    return existing.ListType == listType ? AddRuleResult.AlreadyExists : AddRuleResult.ExistsInOtherList;
                }
            }

            using (var insert = new SQLiteCommand(SQL.InsertRule, connection, transaction))
            {
                insert.Parameters.AddWithValue("@ListType", (int)listType);
                insert.Parameters.AddWithValue("@MatchType", (int)matchType);
                insert.Parameters.AddWithValue("@Target", normalized);
                insert.ExecuteNonQuery();
            }

            rule = new BlacklistRule
            {
                ID = (int)connection.LastInsertRowId,
                ListType = listType,
                MatchType = matchType,
                Target = normalized
            };
            transaction.Commit();
            return AddRuleResult.Added;
        }

        /// <summary>
        /// 把规则移到另一个名单（黑名单 ⇄ 白名单）
        /// </summary>
        public static void MoveRule(int id, BlacklistListType newListType)
        {
            using var connection = SettingDatabase.OpenConnection();
            using var transaction = connection.BeginTransaction();
            using var command = new SQLiteCommand(SQL.MoveRule, connection, transaction);
            command.Parameters.AddWithValue("@ID", id);
            command.Parameters.AddWithValue("@ListType", (int)newListType);
            command.ExecuteNonQuery();
            transaction.Commit();
        }

        /// <summary>
        /// 按 ID 删除规则
        /// </summary>
        public static void RemoveRule(int id)
        {
            using var connection = SettingDatabase.OpenConnection();
            using var transaction = connection.BeginTransaction();
            using var command = new SQLiteCommand(SQL.RemoveRule, connection, transaction);
            command.Parameters.AddWithValue("@ID", id);
            command.ExecuteNonQuery();
            transaction.Commit();
        }

        /// <summary>
        /// 按匹配方式和目标删除规则（目标会先规范化）。返回是否删除了记录。
        /// </summary>
        public static bool RemoveRule(BlacklistMatchType matchType, string target)
        {
            string normalized = BlacklistRuleNormalizer.Normalize(matchType, target);

            using var connection = SettingDatabase.OpenConnection();
            using var transaction = connection.BeginTransaction();
            using var command = new SQLiteCommand(SQL.RemoveRuleByTarget, connection, transaction);
            command.Parameters.AddWithValue("@MatchType", (int)matchType);
            command.Parameters.AddWithValue("@Target", normalized);
            int affected = command.ExecuteNonQuery();
            transaction.Commit();
            return affected > 0;
        }

        #endregion

        #region SQL 语句

        private static class SQL
        {
            // 规则表：同一目标（MatchType + Target）只能出现一次，Target 忽略大小写
            public const string CreateRuleTable = @"
            CREATE TABLE IF NOT EXISTS BlacklistRule
            (
                ID INTEGER PRIMARY KEY AUTOINCREMENT,
                ListType INTEGER NOT NULL CHECK (ListType IN (0, 1)),
                MatchType INTEGER NOT NULL CHECK (MatchType IN (0, 1, 2)),
                Target TEXT NOT NULL COLLATE NOCASE,
                UNIQUE (MatchType, Target)
            );";

            // 设置表：CHECK (ID = 1) 保证只有一行
            public const string CreateSettingTable = @"
            CREATE TABLE IF NOT EXISTS BlacklistSetting
            (
                ID INTEGER PRIMARY KEY CHECK (ID = 1),
                DisableOnFullScreen INTEGER NOT NULL DEFAULT 0,
                ApplyToExtendedHotkey INTEGER NOT NULL DEFAULT 0
            );";

            public const string InsertDefaultSetting = "INSERT OR IGNORE INTO BlacklistSetting (ID) VALUES (1);";

            public const string GetSetting = "SELECT DisableOnFullScreen, ApplyToExtendedHotkey FROM BlacklistSetting WHERE ID = 1;";

            public const string UpdateSetting = @"
            UPDATE BlacklistSetting SET
                DisableOnFullScreen = @DisableOnFullScreen,
                ApplyToExtendedHotkey = @ApplyToExtendedHotkey
            WHERE ID = 1;";

            public const string GetAllRules = "SELECT ID, ListType, MatchType, Target FROM BlacklistRule ORDER BY ID;";

            public const string FindRule = "SELECT ID, ListType, Target FROM BlacklistRule WHERE MatchType = @MatchType AND Target = @Target;";

            public const string InsertRule = "INSERT INTO BlacklistRule (ListType, MatchType, Target) VALUES (@ListType, @MatchType, @Target);";

            public const string MoveRule = "UPDATE BlacklistRule SET ListType = @ListType WHERE ID = @ID;";

            public const string RemoveRule = "DELETE FROM BlacklistRule WHERE ID = @ID;";

            public const string RemoveRuleByTarget = "DELETE FROM BlacklistRule WHERE MatchType = @MatchType AND Target = @Target;";
        }

        #endregion
    }
}