using System.Windows.Input;
using System.Data.SQLite;
using Quicker.Managers;
using System.Text.Json;
using Quicker.Helpers;
using Quicker.Models;
using System.IO;

namespace Quicker.Database.Core
{
    public class ButtonDatabase : IDisposable
    {
        /// <summary>
        /// 打开数据库连接
        /// </summary>
        public SQLiteConnection OpenConnection() => DatabaseHelper.OpenConnection("Button.db");

        public ButtonDatabase()
        {
            DatabaseHelper.EnsureDatabaseDirectoryExists(); // 确保数据库目录存在
            string dbFilePath = Path.Combine(AppPathHelper.DatabaseFolder, "Button.db"); // 设置数据库文件路径
            if (!File.Exists(dbFilePath))
            {
                SQLiteConnection.CreateFile(dbFilePath); // 创建数据库文件
                CreateButtonTable("_global"); // 创建全局表格
                CreateButtonTable("common"); // 创建通用表格
            }
        }

        /// <summary>
        /// 创建Button表格
        /// </summary>
        /// <param name="tableName"> 要创建的表格名称 </param>
        public void CreateButtonTable(string tableName)
        {
            var connection = OpenConnection(); // 打开数据库连接
            string createTableQuery = @"CREATE TABLE IF NOT EXISTS [" + tableName + @"]
            (
                ButtonID INTEGER PRIMARY KEY,
                Title TEXT,
                Location TEXT,
                ImagePath TEXT,
                Data1 TEXT,
                Data2 TEXT,
                Data3 TEXT,
                Description TEXT,
                CreateTime DATETIME,
                LatestEditTime DATETIME,
                ActionType TEXT,
                UsedTimes INTEGER
            );";
            using var command = new SQLiteCommand(createTableQuery, connection); // 创建命令对象
            command.ExecuteNonQuery(); // 执行创建表格语句
        }

        /// <summary>
        /// 获取数据库中所有表名
        /// </summary>
        /// <returns> 表名列表 </returns>
        public List<string> GetAllTableNames()
        {
            var tableNames = new List<string>(); // 初始化表名列表
            using var connection = OpenConnection(); // 打开数据库连接
            using var command = new SQLiteCommand("SELECT name FROM sqlite_master WHERE type='table';", connection); // 创建命令对象
            using var reader = command.ExecuteReader(); // 执行查询语句
            while (reader.Read())
            {
                tableNames.Add(reader.GetString(0)); // 添加表名到列表
            }
            return tableNames; // 返回表名列表
        }

        /// <summary>
        /// 通过ButtonID从对应表中获取数据
        /// </summary>
        /// <param name="buttonID"> 要获取数据的ButtonID </param>
        /// <returns> ButtonData对象，如果找不到则返回null </returns>
        public ButtonData GetButtonDataByID(int buttonID, string tableName)
        {
            using var connection = OpenConnection(); // 打开数据库连接
            using var command = new SQLiteCommand($"SELECT * FROM [{tableName}] WHERE ButtonID = @ButtonID", connection); // 创建命令对象
            command.Parameters.AddWithValue("@ButtonID", buttonID); // 添加参数
            using var reader = command.ExecuteReader(); // 执行查询语句
            if (reader.Read())
            {
                return ButtonDataHelper.FromReader(reader); // 返回ButtonData对象
            }
            return null; // 如果找不到则返回null
        }

        /// <summary>
        /// 更新对应表中的动作数据
        /// </summary>
        /// <param name="buttonData"> 要更新的动作数据 </param>
        /// <summary>
        public void UpdateAction(ButtonData buttonData, string tableName)
        {
            using var connection = OpenConnection(); // 打开数据库连接
            using var transaction = connection.BeginTransaction(); // 开始事务
            string query = $@"INSERT OR REPLACE INTO [{tableName}]
            (ButtonID, Title, Location, ImagePath, Data1, Data2, Data3, Description, CreateTime, LatestEditTime, ActionType, UsedTimes)
            VALUES 
            (@ButtonID, @Title, @Location, @ImagePath, @Data1, @Data2, @Data3, @Description, @CreateTime, @LatestEditTime, @ActionType, @UsedTimes)"; // 创建SQL语句
            using var command = new SQLiteCommand(query, connection); // 创建命令对象
            command.Parameters.AddWithValue("@ButtonID", buttonData.ButtonID); // 动作ID
            command.Parameters.AddWithValue("@Title", buttonData.Title); // 动作名称
            command.Parameters.AddWithValue("@Location", buttonData.Location); // 位置
            command.Parameters.AddWithValue("@ImagePath", buttonData.ImagePath); // 图片路径
            command.Parameters.AddWithValue("@Data1", buttonData.Data1 ?? ""); // 动作数据1
            command.Parameters.AddWithValue("@Data2", buttonData.Data2 ?? ""); // 动作数据2
            command.Parameters.AddWithValue("@Data3", buttonData.Data3 ?? ""); // 动作数据3
            command.Parameters.AddWithValue("@Description", buttonData.Description); // 对动作的描述
            command.Parameters.AddWithValue("@CreateTime", buttonData.CreateTime); // 创建时间
            command.Parameters.AddWithValue("@LatestEditTime", DateTime.Now); // 最近修改时间
            command.Parameters.AddWithValue("@ActionType", buttonData.ActionType); // 动作类型
            command.Parameters.AddWithValue("@UsedTimes", buttonData.UsedTimes); // 使用次数
            command.ExecuteNonQuery(); // 执行更新语句
            transaction.Commit(); // 提交事务

            ActionPageDatabase db3 = new(); // 实例化 ActionPageDatabase
            db3.UpdateActionPageLastEditTime(tableName, buttonData.ButtonID / 100); // 更新动作页面的最后编辑时间
        }

        /// <summary>
        /// 通过Button前缀读取对应表格中所有ButtonData
        /// </summary>
        /// <param name="tableName"> Button前缀 </param>
        /// <returns> ButtonData列表 </returns>
        public List<ButtonData> GetButtonDataByTableName(string tableName)
        {
            var buttonDataList = new List<ButtonData>(); // 初始化ButtonData列表
            using var connection = OpenConnection(); // 打开数据库连接
            using var command = new SQLiteCommand($"SELECT * FROM [{tableName}]", connection); // 创建命令对象
            using var reader = command.ExecuteReader(); // 执行查询语句
            while (reader.Read())
            {
                buttonDataList.Add(ButtonDataHelper.FromReader(reader)); // 添加ButtonData到列表
            }
            return buttonDataList; // 返回ButtonData列表
        }

        /// <summary>
        /// 删除按钮数据表
        /// </summary>
        /// <param name="tableName"> 要删除的表名 </param>
        public void DeleteButtonTable(string tableName)
        {
            using var connection = OpenConnection(); // 打开数据库连接
            using var command = new SQLiteCommand($"DROP TABLE IF EXISTS [{tableName}]", connection); // 创建命令对象
            command.ExecuteNonQuery(); // 执行删除表格语句
        }

        /// <summary>
        /// 删除一整页的按钮
        /// </summary>
        /// <param name="tableName"> 要删除的表名 </param>
        /// <param name="pageIndex"> 页码 </param>
        public void DeletePageOfButtons(string tableName, int pageIndex)
        {
            using var connection = OpenConnection(); // 打开数据库连接
            using var transaction = connection.BeginTransaction(); // 开始事务           
            List<ButtonData> allButtons = GetButtonDataByTableName(tableName); // 获取所有按钮数据
            var currentButtonIDs = allButtons
                .Where(b => b.ButtonID / 100 == pageIndex)
                .Select(b => b.ButtonID)
                .ToList(); // 当前页所有ButtonID
            if (currentButtonIDs.Count > 0) // 批量删除当前页的按钮
            {
                string ids = string.Join(",", currentButtonIDs); // 当前页所有ButtonID
                using var delCmd = new SQLiteCommand($"DELETE FROM [{tableName}] WHERE ButtonID IN ({ids})", connection, transaction); // 创建删除命令
                delCmd.ExecuteNonQuery(); // 执行删除语句
            }

            var subsequentButtons = allButtons
                .Where(b => b.ButtonID / 100 > pageIndex)
                .ToList(); // 后续页所有ButtonData
            foreach (var button in subsequentButtons)
            {
                int page = button.ButtonID / 100; // 原页码
                int bcPart = button.ButtonID % 100; // 目标 ID 的 B 和 C 部分
                int newButtonID = (page - 1) * 100 + bcPart; // 新的 ButtonID
                UpdateButtonID(connection, tableName, button.ButtonID, newButtonID); // 更新 ButtonID
            }
            transaction.Commit(); // 提交事务
        }

        /// <summary>
        /// 更新按钮的页码编号
        /// </summary>
        /// <param name="connection">数据库连接</param>
        /// <param name="buttonID">原 ButtonID </param>
        /// <param name="pageIndex">原页码</param>
        /// <returns>新的 ButtonID</returns>
        private int UpdateButtonPageNumber(SQLiteConnection connection, int buttonID, int pageIndex)
        {
            int page = buttonID / 100; // 原页码
            int bcPart = buttonID % 100; // 目标 ID 的 B 和 C 部分
            if (page > pageIndex) page--; // 如果页码大于原页码，则页码减一
            int newButtonID = page * 100 + bcPart; // 新的 ButtonID
            return newButtonID; // 返回新的 ButtonID
        }

        /// <summary>
        /// 删除动作
        /// </summary>
        /// <param name="buttonID">要删除的动作ID</param>
        public void DeleteAction(int buttonID, string tableName, SQLiteConnection connection = null)
        { 
            if (connection == null) connection = OpenConnection(); // 打开数据库连接
            using var transaction = connection.BeginTransaction(); // 开始事务
            using var command = new SQLiteCommand($@"DELETE FROM [{tableName}] WHERE ButtonID = @ButtonID", connection); // 创建命令
            command.Parameters.AddWithValue("@ButtonID", buttonID); // 绑定参数
            command.ExecuteNonQuery(); // 执行命令
            transaction.Commit(); // 提交事务

            ActionPageDatabase db3 = new(); // 实例化 ActionPageDatabase
            db3.UpdateActionPageLastEditTime(tableName, buttonID / 100); // 更新动作页面的最后编辑时间
        }

        /// <summary>
        /// 根据不同情况更改 Button 数据库
        /// </summary>
        /// <param name="buttonID1"> ButtonID1 </param>
        /// <param name="buttonID2"> ButtonID2 </param>
        public void ExchangeButtonID(int buttonID1, int buttonID2, string tableName1, string tableName2)
        {
            bool isCtrlPressed = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl); // 判断是否按下了 Ctrl 键
            using var connection = OpenConnection(); // 打开连接
            var data1 = GetButtonDataByID(buttonID1, tableName1); // 获取 ButtonID1 的数据
            var data2 = GetButtonDataByID(buttonID2, tableName2); // 获取 ButtonID2 的数据
            ButtonData newButtonData1 = new ButtonData
            {
                ButtonID = buttonID2,
                Title = data1.Title,
                Location = data1.Location,
                ImagePath = data1.ImagePath,
                Data1 = data1.Data1,
                Data2 = data1.Data2,
                Data3 = data1.Data3,
                Description = data1.Description,
                CreateTime = data1.CreateTime,
                LatestEditTime = data1.LatestEditTime,
                ActionType = data1.ActionType,
                UsedTimes = data1.UsedTimes
            }; // 构造新的 ButtonData
            ButtonData newButtonData2 = new(); // 构造新的 ButtonData
            if (!isCtrlPressed)
                DeleteAction(buttonID1, tableName1); // 删除 ButtonID1
            if (data2 != null)
            {
                if (isCtrlPressed)
                {
                    using var toast = new ToastManager(); // 实例化 ToastManager
                    toast.Show("目标位置已有动作，不可再添加新动作。", ToastType.Error); // 显示提示
                    return; // 退出函数
                }
                else
                {
                    newButtonData2.ButtonID = buttonID1; // 新的 ButtonID
                    newButtonData2.Title = data2.Title;
                    newButtonData2.Location = data2.Location;
                    newButtonData2.ImagePath = data2.ImagePath;
                    newButtonData2.Data1 = data2.Data1;
                    newButtonData2.Data2 = data2.Data2;
                    newButtonData2.Data3 = data2.Data3;
                    newButtonData2.Description = data2.Description;
                    newButtonData2.CreateTime = data2.CreateTime;
                    newButtonData2.LatestEditTime = data2.LatestEditTime;
                    newButtonData2.ActionType = data2.ActionType;
                    newButtonData2.UsedTimes = data2.UsedTimes;

                    DeleteAction(buttonID2, tableName2); // 删除 ButtonID2
                    UpdateAction(newButtonData2, tableName1); // 更新 ButtonID2 到 ButtonID1
                }
            }
            UpdateAction(newButtonData1, tableName2); // 更新 ButtonID1 到 ButtonID2
        }

        /// <summary>
        /// 更新 ButtonID
        /// </summary>
        /// <param name="connection"> 数据库连接 </param>
        /// <param name="tableName"> 数据库表名 </param>
        /// <param name="oldButtonID"> 要更改的 ButtonID </param>
        /// <param name="newButtonID"> 目标 ButtonID </param>
        private void UpdateButtonID(SQLiteConnection connection, string tableName, int oldButtonID, int newButtonID)
        {
            using var command = new SQLiteCommand($@"UPDATE [{tableName}] SET ButtonID = @NewButtonID WHERE ButtonID = @OldButtonID", connection);
            command.Parameters.AddWithValue("@NewButtonID", newButtonID); // 绑定参数
            command.Parameters.AddWithValue("@OldButtonID", oldButtonID); // 绑定参数
            command.ExecuteNonQuery(); // 执行更新语句
        }

        /// <summary>
        /// 将按钮数据迁移到新表
        /// </summary>
        /// <param name="buttonID">按钮ID</param>
        /// <param name="sourceTable">源表名</param>
        /// <param name="targetTable">目标表名</param>
        private void MoveButtonDataToNewTable(SQLiteConnection connection, int buttonID, ButtonData buttonData, string sourceTable, string targetTable)
        {
            using var transaction = connection.BeginTransaction(); // 开始事务
            string query = $@"INSERT INTO {targetTable} 
            (ButtonID, Title, Location, ImagePath, Data1, Data2, Data3, Description, CreateTime, LatestEditTime, ActionType, UsedTimes)
            VALUES 
            (@ButtonID, @Title, @Location, @ImagePath, @Data1, @Data2, @Data3, @Description, @CreateTime, @LatestEditTime, @ActionType, @UsedTimes)"; // 创建SQL语句
            using var command = new SQLiteCommand(query, connection);
            command.Parameters.AddWithValue("@ButtonID", buttonID); // 动作ID
            command.Parameters.AddWithValue("@Title", buttonData.Title); // 动作名称
            command.Parameters.AddWithValue("@Location", buttonData.Location); // 位置
            command.Parameters.AddWithValue("@ImagePath", buttonData.ImagePath); // 图片路径
            command.Parameters.AddWithValue("@Data1", buttonData.Data1); // 动作数据1
            command.Parameters.AddWithValue("@Data2", buttonData.Data2); // 动作数据2
            command.Parameters.AddWithValue("@Data3", buttonData.Data3); // 动作数据3
            command.Parameters.AddWithValue("@Description", buttonData.Description); // 对动作的描述
            command.Parameters.AddWithValue("@CreateTime", buttonData.CreateTime); // 创建时间
            command.Parameters.AddWithValue("@LatestEditTime", buttonData.LatestEditTime); // 最近修改时间
            command.Parameters.AddWithValue("@ActionType", buttonData.ActionType); // 动作类型
            command.Parameters.AddWithValue("@UsedTimes", buttonData.UsedTimes); // 使用次数
            command.ExecuteNonQuery(); // 执行插入语句
            transaction.Commit(); // 提交事务

            DeleteAction(buttonID, sourceTable); // 删除源表数据
        }

        /// <summary>
        /// 增加动作使用次数
        /// </summary>
        /// <param name="buttonID"> 要增加的动作ID </param>
        public void IncreaseActionUsedTimes(int buttonID, string tableName)
        {
            using var connection = OpenConnection(); // 打开数据库连接
            using var command = new SQLiteCommand($@"UPDATE [{tableName}] SET UsedTimes = UsedTimes + 1 WHERE ButtonID = @ButtonID", connection);
            command.Parameters.AddWithValue("@ButtonID", buttonID); // 绑定参数
            command.ExecuteNonQuery(); // 执行更新语句
        }

        /// <summary>
        /// 根据输入的字符串和数字 A，获取符合条件的 ButtonData
        /// </summary>
        /// <param name="tableName"> Button前缀 </param>
        /// <param name="a"> 数字 A </param>
        /// <returns> ButtonData列表 </returns>
        public List<ButtonData> GetPagesOfButtons(string tableName, int a)
        {
            List<ButtonData> buttonDatas = GetButtonDataByTableName(tableName); // 获取所有以 pfefix 开头的 ButtonData
            var matchedButtons = buttonDatas
                .Where(b => b.ButtonID / 100 == a)
                .ToList(); // 返回符合条件的 ButtonData 列表
            return matchedButtons; // 返回符合条件的 ButtonData 列表
        }

        /// <summary>
        /// 根据输入的字符串和数字 A1、A2，交换符合条件的 ButtonID 的 A 部分
        /// </summary>
        /// <param name="prefix"> Button的字符串索引 </param>
        /// <param name="a1"> A1 部分 </param>
        /// <param name="a2"> A2 部分 </param>
        public void SwapButtonAValues(string prefix, int a1, int a2)
        {
            var a1ButtonDatas = GetPagesOfButtons(prefix, a1); // 获取 A1 部分的 ButtonData
            var a2ButtonDatas = GetPagesOfButtons(prefix, a2); // 获取 A2 部分的 ButtonData
            if (a1ButtonDatas.Count == 0 && a2ButtonDatas.Count == 0) return; // 两个部分没有 ButtonData，直接返回

            using var connection = OpenConnection(); // 打开数据库连接
            using var transaction = connection.BeginTransaction(); // 开始事务
            foreach (var buttonData in a1ButtonDatas.ToList()) // 更新 A1 部分的 ButtonID 为临时标识符
            {
                int newButtonID = -buttonData.ButtonID; // 新 ButtonID
                UpdateButtonID(connection, prefix, buttonData.ButtonID, newButtonID); // 更新 ButtonID

                var index = a1ButtonDatas.IndexOf(buttonData); // 获取索引
                if (index != -1) a1ButtonDatas[index].ButtonID = newButtonID; // 更新字典中的 ButtonID
            }

            foreach (var buttonData in a2ButtonDatas.ToList()) // 更新 A2 部分的 ButtonID 为目标 ID
            {
                int bcPart = buttonData.ButtonID % 100; // 目标 ID 的 B 和 C 部分
                int newButtonID = a1 * 100 + bcPart; // 新 ButtonID
                UpdateButtonID(connection, prefix, buttonData.ButtonID, newButtonID); // 更新 ButtonID
            }

            foreach (var buttonData in a1ButtonDatas.ToList()) // 更新临时标识符的 ButtonID 为目标 ID
            {
                int bcPart = -buttonData.ButtonID % 100; // 目标 ID 的 B 和 C 部分
                int newButtonID = a2 * 100 + bcPart; // 新 ButtonID
                UpdateButtonID(connection, prefix, buttonData.ButtonID, newButtonID); // 更新 ButtonID
            }
            transaction.Commit(); // 提交事务

            ActionPageDatabase db3 = new(); // 实例化 ActionPageDatabase
            db3.UpdateActionPageLastEditTime(prefix, a1); // 更新动作页面的最后编辑时间
            db3.UpdateActionPageLastEditTime(prefix, a2); // 更新动作页面的最后编辑时间
        }

        /// <summary>
        /// 获取总的页面数
        /// </summary>
        /// <param name="tableName"> 目标样式名称 </param>
        /// <returns> 总页面数 </returns>
        public int GetTotalAntionPageIndex(string tableName)
        {
            using var connection = OpenConnection();
            using var command = new SQLiteCommand($"SELECT MAX(ButtonID) FROM [{tableName}]", connection);
            var result = command.ExecuteScalar();

            if (result == null || result == DBNull.Value) return 1; // 空表返回 1

            int maxId = Convert.ToInt32(result);
            return (maxId / 100) + 1;
        }

        /// <summary>
        /// 将JSON文件中的数据导入数据库
        /// </summary>
        /// <param name="tableName"> 要导入数据的表名 </param>
        /// <param name="jsonFilePath"> JSON文件的路径 </param>
        public void ImportJsonDataToList(string tableName, string jsonFilePath, int buttonID)
        {
            string json = File.ReadAllText(jsonFilePath); // 读取 JSON 文件
            ButtonData data = JsonSerializer.Deserialize<ButtonData>(json); // 反序列化 JSON 数据
            using var connection = OpenConnection(); // 打开数据库连接
            ButtonData newData = new ButtonData
            {
                ButtonID = buttonID,
                Title = data.Title,
                Location = data.Location,
                ImagePath = data.ImagePath,
                Data1 = data.Data1,
                Data2 = data.Data2,
                Data3 = data.Data3,
                Description = data.Description,
                CreateTime = data.CreateTime,
                LatestEditTime = DateTime.Now,
                ActionType = data.ActionType,
                UsedTimes = 0
            }; // 构造新的 ButtonData
            UpdateAction(newData, tableName); // 更新动作数据到数据库
        }

        /// <summary>
        /// 将动作数据导出为 JSON 文件，并在导出后在文件资源管理器中选中文件
        /// </summary>
        /// <param name="tableName">动作所在表名</param>
        /// <param name="buttonID">动作ID</param>
        /// <param name="outputPath">输出文件夹路径</param>
        public void ExportActionDataToJson(string tableName, int buttonID, string outputPath)
        {
            var data = GetButtonDataByID(buttonID, tableName); // 获取动作数据
            string fileName = $"{data.Title}_{DateTime.Now.ToString("yyyyMMdd_HHmmss")}.json"; // 构造文件名，日期与时间部分用下划线分隔
            string fullOutputPath = Path.Combine(outputPath, fileName); // 构造完整的输出路径
            string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }); // 序列化数据为 JSON 格式
            File.WriteAllText(fullOutputPath, json); // 写入 JSON 文件

            // 在文件资源管理器中选中文件
            string command = "/select, \"" + fullOutputPath + "\""; // 构造命令
            System.Diagnostics.Process.Start("explorer.exe", command); // 打开文件资源管理器并选中文件
        }

        /// <summary>
        /// 获取动作大小
        /// </summary>
        /// <param name="tableName"> 动作所在表名 </param>
        /// <param name="buttonID"> 动作ID </param>
        /// <returns> 动作大小 </returns>
        public int GetActionSize(string tableName, int buttonID)
        {
            var data = GetButtonDataByID(buttonID, tableName); // 获取动作数据
            int size = 0; // 动作大小
            size += data.Title?.Length * 2 ?? 0; // 标题字符串长度 * 2 (UTF-16)
            size += data.Location?.Length * 2 ?? 0; // 位置字符串长度 * 2
            size += data.ImagePath?.Length * 2 ?? 0; // 图片路径字符串长度 * 2
            size += data.Data1?.Length * 2 ?? 0; // 数据1字符串长度 * 2
            size += data.Data2?.Length * 2 ?? 0; // 数据2字符串长度 * 2
            size += data.Data3?.Length * 2 ?? 0; // 数据3字符串长度 * 2
            size += data.Description?.Length * 2 ?? 0; // 描述字符串长度 * 2
            size += 4; // ActionType (enum)
            size += 4; // ButtonID (int)
            size += 8; // CreateTime (DateTime)
            size += 8; // LatestEditTime (DateTime)
            size += 4; // UsedTimes (int)
            return size; // 返回动作大小
        }

        /// <summary>
        /// 通过动作名称获取动作数据，遍历所有表
        /// </summary>
        /// <param name="buttonName"> 动作名称 </param>
        /// <returns> 动作数据 </returns>
        public (List<ButtonData> buttonDataList, List<string> tableNames) GetButtonbyName(string buttonName)
        {
            var buttonDataList = new List<ButtonData>(); // 实例化列表
            var tableNames = new List<string>(); // 实例化列表
            var allTableNames = GetAllTableNames(); // 获取所有表名
            foreach (var tableName in allTableNames)
            {
                using var connection = OpenConnection(); // 打开数据库连接
                using var command = new SQLiteCommand($"SELECT * FROM [{tableName}] WHERE Title LIKE @Title", connection); // 创建命令
                command.Parameters.AddWithValue("@Title", $"%{buttonName}%"); // 绑定参数
                using var reader = command.ExecuteReader(); // 执行查询语句
                while (reader.Read())
                {
                    var title = reader["Title"].ToString(); // 标题
                    if (title.Contains(buttonName, StringComparison.OrdinalIgnoreCase)) // 如果标题包含动作名称
                    {
                        buttonDataList.Add(ButtonDataHelper.FromReader(reader)); // 构造 ButtonData 并添加到列表
                        tableNames.Add(tableName); // 记录表名
                    }
                }
            }
            return (buttonDataList, tableNames); // 返回动作数据列表和表名列表
        }

        /// <summary>
        /// 检查表是否存在
        /// </summary>
        /// <param name="tableName"> 要检查的表名 </param>
        /// <returns> 表是否存在 </returns>
        public bool TableExists(string tableName)
        {
            using var connection = OpenConnection(); // 打开数据库连接
            using var command = new SQLiteCommand("SELECT name FROM sqlite_master WHERE type='table' AND name = @TableName;",connection);
            command.Parameters.AddWithValue("@TableName", tableName); // 绑定参数
            using var reader = command.ExecuteReader(); // 执行查询语句
            return reader.Read(); // 返回是否存在
        }

        /// <summary>
        /// 一次性获取某一页的所有按钮数据（避免循环查库）
        /// </summary>
        public Dictionary<int, ButtonData> GetButtonDataByPage(string tableName, int pageIndex)
        {
            var result = new Dictionary<int, ButtonData>();
            using var connection = OpenConnection();
            // 利用 ButtonID 的规律，直接用 BETWEEN 或数学运算过滤，避免全表扫描
            int minId = pageIndex * 100;
            int maxId = (pageIndex + 1) * 100;

            using var command = new SQLiteCommand($"SELECT * FROM [{tableName}] WHERE ButtonID >= @MinId AND ButtonID < @MaxId", connection);
            command.Parameters.AddWithValue("@MinId", minId);
            command.Parameters.AddWithValue("@MaxId", maxId);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var data = ButtonDataHelper.FromReader(reader);
                result[data.ButtonID] = data;
            }
            return result;
        }

        /// <summary>
        /// 遍历所有表，将 ImagePath 中的旧根路径替换为新根路径。
        /// </summary>
        /// <param name="oldRoot">旧图片根路径（例如 C:\\Users\\LENOVO\\AppData\\Roaming\\Anonymity\\Quicker）</param>
        /// <param name="newRoot">新图片根路径（例如 C:\\Downloads）</param>
        public void MigrateImagePathRoot(string oldRoot, string newRoot)
        {
            if (string.IsNullOrWhiteSpace(oldRoot) || string.IsNullOrWhiteSpace(newRoot))
            {
                return;
            }

            // 规范化为完整路径，尽量统一比较
            string normalizedOld = oldRoot.Trim();
            string normalizedNew = newRoot.Trim();
            if (string.Equals(normalizedOld, normalizedNew, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var tableNames = GetAllTableNames();
            if (tableNames == null || tableNames.Count == 0)
            {
                return;
            }

            using var connection = OpenConnection();
            using var transaction = connection.BeginTransaction();
            foreach (var table in tableNames)
            {
                // 跳过 SQLite 内部表
                if (table.StartsWith("sqlite_", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string sql = "UPDATE [" + table + 
                    "] SET ImagePath = REPLACE(COALESCE(ImagePath, ''), @OldRoot, @NewRoot) " +
                    "WHERE ImagePath LIKE @OldLike";

                using var cmd = new SQLiteCommand(sql, connection, transaction);
                cmd.Parameters.AddWithValue("@OldRoot", normalizedOld);
                cmd.Parameters.AddWithValue("@NewRoot", normalizedNew);
                cmd.Parameters.AddWithValue("@OldLike", normalizedOld + "%");

                cmd.ExecuteNonQuery();
            }
            transaction.Commit();
        }

        public void Dispose()
        {
            // 释放资源
        }
    }
}