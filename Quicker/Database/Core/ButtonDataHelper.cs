using Quicker.Contracts.Models;
using Quicker.Models;

namespace Quicker.Database.Core
{
    public static class ButtonDataHelper
    {
        /// <summary>
        /// 从 SQLiteDataReader 中读取数据并构造 ButtonData 对象
        /// </summary>
        /// <param name="reader"> SQLiteDataReader </param>
        /// <returns> 按钮数据 </returns>
        public static ButtonData FromReader(System.Data.SQLite.SQLiteDataReader reader)
        {
            return new ButtonData
            {
                ButtonID = reader.GetInt32(0), // 动作ID
                Title = reader.GetString(1), // 动作名称
                Location = reader.GetString(2), // 位置
                ImagePath = reader.GetString(3), // 图片路径
                Data1 = reader.IsDBNull(4) ? null : reader.GetString(4), // 动作数据1
                Data2 = reader.IsDBNull(5) ? null : reader.GetString(5), // 动作数据2
                Data3 = reader.IsDBNull(6) ? null : reader.GetString(6), // 动作数据3
                Description = reader.GetString(7), // 对动作的描述
                CreateTime = reader.GetDateTime(8), // 创建时间
                LatestEditTime = reader.GetDateTime(9), // 最近修改时间
                ActionType = reader.IsDBNull(10) ? null : (ActionType)Enum.Parse(typeof(ActionType), reader.GetString(10)), // 动作类型
                UsedTimes = reader.GetInt32(11) // 使用次数
            }; // 返回 ButtonData 对象
        }
    }
}