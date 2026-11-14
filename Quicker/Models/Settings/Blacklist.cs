namespace Quicker.Models.Settings
{
    /// <summary>
    /// 黑名单设置模型
    /// </summary>
    public class Blacklist
    {
        public int ID { get; set; } // 主键
        public bool IsFullScreenDisabled { get; set; } // 是否开启全屏或最大化禁用功能
        public bool IsBlacklistEnabledForExtendedHotkey { get; set; } // 是否将黑名单与全屏禁用设置应用于扩展热键功能
    }

    /// <summary>
    /// 黑名单应用模型
    /// </summary>
    public class BlacklistApplication
    {
        /* ApplicationName 与 ProcessName 字段的含义如下：
         * ApplicationName: 黑名单列表显示的文字，可以是文件夹路径，也可以是应用程序名称。
         * ProcessName: 确切的应用程序进程名称，应用程序的可执行文件名称。
         * 
         * 一个 ApplicationName 可以对应多个 ProcessName，例如，一个文件夹路径可以对应多个应用程序的进程名称。
         * 但是一个 ProcessName 只能对应一个 ApplicationName。
         */
        public int ID { get; set; } // 主键
        public string ApplicationName { get; set; } // 应用程序名称
        public string ProcessName { get; set; } // 进程名称
        public bool IsInBlacklist { get; set; } // 是否在黑名单中
        public bool IsFolder { get; set; } // 是否是文件夹
    }
}