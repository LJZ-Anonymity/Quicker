using Quicker.Database.Core;
using System.Data.SQLite;
using Quicker.Helpers;
using System.IO;

namespace Quicker.Database.Upgrade.Versions
{
    /// <summary>
    /// 数据库结构从版本 1 升级到版本 2 的迁移步骤。
    /// </summary>
    internal class UpgradeSchema_1_to_2 : IDatabaseUpgradeStep
    {
        public int FromSchemaVersion => 1; // 升级前的结构版本号
        public int ToSchemaVersion => 2;   // 升级后的结构版本号

        /// <summary>
        /// 升级数据库
        /// </summary>
        public void Upgrade(SQLiteConnection connection, DatabaseUpdateManager manager)
        {

        }
    }
}