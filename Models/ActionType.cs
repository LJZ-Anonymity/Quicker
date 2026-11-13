namespace Quicker.Models
{
    /// <summary>
    /// 动作类型。
    /// </summary>
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

    /*
     * 各 ActionType 对应的 ButtonData 字段语义约定：
     *
     * OpenFile：
     *   Location：文件(夹)绝对路径，多个路径用分号分隔
     *   Data1：是否用管理员身份运行（True/False）
     *   Data2：是否打开当前存在的窗口（True/False）
     *   Data3：打开窗口时设置窗口的状态（0=Normal / 1=Minimized / 2=Maximized）
     *
     * OpenWebsite：
     *   Location：网页地址
     *   Data1：使用浏览器类型（0~7）
     *   Data2：自定义浏览器地址
     *
     * LoadExtension：
     *   Location：扩展程序的绝对路径
     *
     * OpenUri：
     *   Location：协议命令，如 weixin://launchapplet/?app_id=xxx
     *   Data1：是否用管理员身份运行（True/False）
     *   Data3：窗口状态（0/1/2）
     */
}