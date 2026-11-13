namespace Quicker.Models
{
    /// <summary>
    /// 按钮数据模型
    /// </summary>
    public class ButtonData
    {
        public int ButtonID { get; set; } // 动作ID，通常为Button的名称
        public string Title { get; set; } // 动作名称
        public string Location { get; set; } // 位置
        public string ImagePath { get; set; } // 图片路径
        public string Data1 { get; set; } // 动作数据1
        public string Data2 { get; set; } // 动作数据2
        public string Data3 { get; set; } // 动作数据3
        public string Description { get; set; } // 对动作的描述
        public DateTime CreateTime { get; set; } // 创建时间
        public DateTime LatestEditTime { get; set; } // 最近修改时间
        public ActionType? ActionType{ get; set; } // 动作类型
        public int UsedTimes { get; set; } // 使用次数
    }
}