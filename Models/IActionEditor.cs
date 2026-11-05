namespace Quicker.Models
{
    /// <summary>
    /// 动作编辑器的统一接口，由 OpenFile / OpenWebsite / LoadExtension 等 UserControl 实现。
    /// 从 AddActionWindow 内部接口迁移而来，供 ActionSaveHelper 等外部类型复用。
    /// </summary>
    public interface IActionEditor
    {
        ActionType GetActionType();
        string GetLocation();
        string GetData1();
        string GetData2();
        string GetData3();
    }
}