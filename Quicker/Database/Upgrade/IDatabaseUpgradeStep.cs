using System.Data.SQLite;

namespace Quicker.Database.Upgrade
{
    public interface IDatabaseUpgradeStep
    {
        /// <summary>
        /// 起始数据库结构版本号（SchemaVersion）
        /// </summary>
        int FromSchemaVersion { get; }

        /// <summary>
        /// 目标数据库结构版本号（SchemaVersion）
        /// </summary>
        int ToSchemaVersion { get; }

        /// <summary>
        /// 执行从 FromSchemaVersion 到 ToSchemaVersion 的结构和数据迁移
        /// </summary>
        void Upgrade(SQLiteConnection connection, DatabaseUpdateManager manager);
    }
}