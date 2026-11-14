namespace Quicker.Internal
{
    public enum InternalCommandType
    {
        None = 0,
        CopyAction,
        CutAction,
        OpenActionPage
    }

    public class InternalCommand
    {
        public InternalCommandType CommandType { get; init; } = InternalCommandType.None; // 命令类型
        public string TableName { get; init; } // 表名
        public int ButtonId { get; init; } // 按钮ID
        public string ActionPageType { get; init; } // 动作页类型
        public string ActionPageIndex { get; init; } // 动作页索引
        public DateTime Timestamp { get; init; } = DateTime.UtcNow; // 时间戳
    }
}