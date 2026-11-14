namespace Quicker.Contracts.Launching
{
    public enum LaunchKind
    {
        OpenFile,
        OpenWebsite,
        OpenUri,
        OpenUwpApp
    }

    public enum WindowShowStyle
    {
        Normal = 0,
        Minimized = 1,
        Maximized = 2
    }

    /// <summary>
    /// 窄契约：只带真正需要的字段，不把整个 ButtonData 塞进来。
    /// </summary>
    public sealed record LaunchRequest
    {
        public LaunchKind Kind { get; init; }
        public string Location { get; init; } = string.Empty;

        /// <summary>网址打开方式（对应原 Data1：0=默认,1=IE,…,8=自定义）</summary>
        public int WebsiteMode { get; init; }

        /// <summary>是否以管理员身份运行（对应原 Data1 == "True"）</summary>
        public bool RunAsAdmin { get; init; }

        /// <summary>窗口状态（对应原 Data3）</summary>
        public WindowShowStyle WindowStyle { get; init; } = WindowShowStyle.Normal;

        /// <summary>尝试激活已打开的窗口（对应原 Data2 == "true"）</summary>
        public bool TryActivateExistingWindow { get; init; }

        /// <summary>工作目录，可为 null</summary>
        public string? WorkingDirectory { get; init; }
    }
}