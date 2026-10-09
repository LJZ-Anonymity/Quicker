namespace Quicker.Models
{
    public enum ActionType
    {
        OpenFile,      // 打开文件/文件夹
        OpenWebsite,   // 打开网页
        OpenFiles,     // 打开多个文件
        OpenUwpApp,    // 打开UWP应用
        OpenUri,       // 执行 URI 协议命令（weixin://、steam:// 等）
        LoadExtension, // 加载扩展程序
        OpenActionPage // 打开动作页面
    }

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
    // ActionType 及其相关字段说明

    /*
     * OpenFile：打开文件/文件夹
     * Location：文件(夹)绝对路径，多个路径用分号分隔
     * Data1：是否用管理员身份运行（True/False）
     * Data2：是否打开当前存在的窗口（True/False）
     * Data3：打开窗口时设置窗口的状态（Normal：0/Minimized：1/Maximized：2）
     */

    /*
     * OpenWebsite：打开网页
     * Location：网页地址
     * Data1：使用浏览器类型（0~7）
     * Data2：自定义浏览器地址
     */

    /*
     * LoadExtension：加载扩展程序
     * Location：扩展程序的绝对路径
     */

    /*
     * OpenUri：执行 URI 协议命令
     * Location：协议命令，如 weixin://launchapplet/?app_id=xxx
     * Data1：是否用管理员身份运行（True/False）
     * Data3：窗口状态（0/1/2）
     */
}