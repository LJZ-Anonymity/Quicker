using Quicker.Database.Upgrade.Versions;
using Quicker.Windows.ToolWindows;
using Quicker.Database.Core;
using System.Data.SQLite;
using Quicker.Managers;
using Quicker.Models;

namespace Quicker.Database.Upgrade
{
    /// <summary>
    /// 数据库升级管理器（基于 SchemaVersion 的结构版本控制）
    /// </summary>
    public class DatabaseUpdateManager : IDisposable
    {
        private readonly List<IDatabaseUpgradeStep> _upgradeSteps; // 升级步骤列表
        private readonly int _latestSchemaVersion; // 当前程序支持的最新结构版本
        public readonly ButtonDatabase _db2 = new(); // 按钮数据库对象
        private bool _disposed = false; // 标记是否已释放资源

        public DatabaseUpdateManager()
        {
            _upgradeSteps = new List<IDatabaseUpgradeStep>
            {
                // 初始化所有从低到高的结构升级步骤
                new UpgradeSchema_1_to_2()
            }; // 升级步骤列表

            // 计算当前最新结构版本号
            _latestSchemaVersion = _upgradeSteps.Count > 0
                ? _upgradeSteps.Max(s => s.ToSchemaVersion)
                : 1;
        }

        // 检查并升级数据库
        public void CheckAndUpgradeDatabase()
        {
            // 确保 SchemaVersion 表存在并初始化
            EnsureSchemaVersionInitialized();

            int currentSchemaVersion = GetCurrentSchemaVersion(); // 获取当前数据库结构版本号
            if (currentSchemaVersion >= _latestSchemaVersion) return; // 已是最新结构版本，无需升级

            LoadingWindow loadingWindow = new(); // 创建加载窗口
            loadingWindow.Show(); // 显示加载窗口
            try
            {
                while (true)
                {
                    var step = _upgradeSteps.FirstOrDefault(s => s.FromSchemaVersion == currentSchemaVersion);
                    if (step == null) break;
                    using (var conn = SettingDatabase.OpenConnection())
                    {
                        step.Upgrade(conn, this);
                    }
                    SetCurrentSchemaVersion(step.ToSchemaVersion);
                    currentSchemaVersion = step.ToSchemaVersion;
                }
            }
            catch
            {
                var toast = new ToastManager(); // 创建 Toast 管理器
                toast.Show("数据库更新失败，请删除数据库文件后重试。", ToastType.Error); // 显示 Toast 通知
            }
            finally
            {
                loadingWindow.Close(); // 关闭加载窗口
            }
        }

        // 释放资源
        public void Dispose()
        {
            Dispose(true); // 释放托管资源
            GC.SuppressFinalize(this); // 调用终结器（析构器）以释放非托管资源
        }

        /// <summary>
        /// 释放资源的受保护实现
        /// </summary>
        /// <param name="disposing"> 是否释放托管资源 </param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed) _disposed = true; // 防止重复释放
        }

        #region SchemaVersion 帮助方法

        /// <summary>
        /// 确保 SchemaVersion 表存在并有一条初始记录
        /// </summary>
        private void EnsureSchemaVersionInitialized()
        {
            using var connection = SettingDatabase.OpenConnection();

            // 创建 SchemaVersion 表（如果不存在）
            string createTableSql = @"
            CREATE TABLE IF NOT EXISTS SchemaVersion
            (
                Id INTEGER PRIMARY KEY CHECK (Id = 1),
                Version INTEGER NOT NULL
            );";
            using (var createCmd = new SQLiteCommand(createTableSql, connection))
            {
                createCmd.ExecuteNonQuery();
            }

            // 确保有一条记录
            string selectSql = "SELECT Version FROM SchemaVersion WHERE Id = 1;";
            using var selectCmd = new SQLiteCommand(selectSql, connection);
            var result = selectCmd.ExecuteScalar();
            if (result == null || result == DBNull.Value)
            {
                // 默认结构版本设为当前程序支持的最新结构版本
                string insertSql = "INSERT INTO SchemaVersion (Id, Version) VALUES (1, @Version);";
                using var insertCmd = new SQLiteCommand(insertSql, connection);
                insertCmd.Parameters.AddWithValue("@Version", _latestSchemaVersion);
                insertCmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// 获取当前数据库结构版本号（SchemaVersion）
        /// </summary>
        private int GetCurrentSchemaVersion()
        {
            using var connection = SettingDatabase.OpenConnection();
            string selectSql = "SELECT Version FROM SchemaVersion WHERE Id = 1;";
            using var cmd = new SQLiteCommand(selectSql, connection);
            var result = cmd.ExecuteScalar();
            if (result == null || result == DBNull.Value)
            {
                return 1;
            }
            return Convert.ToInt32(result);
        }

        /// <summary>
        /// 设置当前数据库结构版本号（SchemaVersion）
        /// </summary>
        private void SetCurrentSchemaVersion(int version)
        {
            using var connection = SettingDatabase.OpenConnection();
            string upsertSql = "INSERT OR REPLACE INTO SchemaVersion (Id, Version) VALUES (1, @Version);";
            using var cmd = new SQLiteCommand(upsertSql, connection);
            cmd.Parameters.AddWithValue("@Version", version);
            cmd.ExecuteNonQuery();
        }

        #endregion
    }
}