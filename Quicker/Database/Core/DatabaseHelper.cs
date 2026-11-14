using System.Data.SQLite;
using Quicker.Helpers;
using System.IO;

namespace Quicker.Database.Core
{
    /// <summary>
    /// 数据库工具类，提供通用的数据库操作方法
    /// </summary>
    public static class DatabaseHelper
    {
        /// <summary>
        /// 获取数据库连接字符串
        /// </summary>
        /// <param name="databaseFileName">数据库文件名</param>
        /// <returns>连接字符串</returns>
        public static string GetConnectionString(string databaseFileName)
        {
            string dbPath = Path.Combine(AppPathHelper.DatabaseFolder, databaseFileName);
            return $"Data Source={dbPath};Pooling=true;Max Pool Size=100;Journal Mode=Wal;";
        }

        /// <summary>
        /// 确保数据库目录存在
        /// </summary>
        public static void EnsureDatabaseDirectoryExists() => AppPathHelper.EnsureDirectoryExists(AppPathHelper.DatabaseFolder);

        /// <summary>
        /// 打开数据库连接
        /// </summary>
        /// <param name="databaseFileName">数据库文件名</param>
        /// <returns>数据库连接</returns>
        public static SQLiteConnection OpenConnection(string databaseFileName)
        {
            var connection = new SQLiteConnection(GetConnectionString(databaseFileName));
            connection.BusyTimeout = 30000; // 设置超时时间为 30 秒
            connection.Open();
            return connection;
        }
    }
} 