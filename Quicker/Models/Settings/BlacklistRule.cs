namespace Quicker.Models.Settings
{
    /// <summary>
    /// 规则所属名单
    /// </summary>
    public enum BlacklistListType
    {
        /// <summary> 黑名单：始终禁用 Quicker </summary>
        Blacklist = 0,

        /// <summary> 白名单：开启全屏/最大化禁用后，这些程序仍然启用 Quicker </summary>
        Whitelist = 1
    }

    /// <summary>
    /// 规则的匹配方式
    /// </summary>
    public enum BlacklistMatchType
    {
        /// <summary> 按进程名匹配（exe 文件名去掉扩展名，小写） </summary>
        ProcessName = 0,

        /// <summary> 按文件夹匹配（该文件夹及其子文件夹下的程序） </summary>
        Folder = 1,

        /// <summary> 无法识别进程信息的程序（取不到进程名/路径时） </summary>
        Unknown = 2
    }

    /// <summary>
    /// 一条黑名单/白名单规则（对应 BlacklistRule 表的一行）
    /// </summary>
    public class BlacklistRule
    {
        public int ID { get; set; }
        public BlacklistListType ListType { get; set; }
        public BlacklistMatchType MatchType { get; set; }

        /// <summary>
        /// 规范化后的匹配内容：进程名为小写且无扩展名；文件夹为完整路径且无末尾分隔符；Unknown 为空串
        /// </summary>
        public string Target { get; set; } = string.Empty;
    }

    /// <summary>
    /// 黑名单相关设置（对应 BlacklistSetting 表的唯一一行）
    /// </summary>
    public class BlacklistSetting
    {
        /// <summary> 全屏或最大化时禁用 Quicker </summary>
        public bool DisableOnFullScreen { get; set; }

        /// <summary>
        /// 黑名单与全屏禁用设置是否也作用于扩展热键。
        /// 注意：BlacklistControl 的界面上目前没有这个选项；如果确认别处也没有使用，可以删除这一项及对应的列。
        /// </summary>
        public bool ApplyToExtendedHotkey { get; set; }
    }
}
