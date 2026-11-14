namespace Quicker.Models
{
    /// <summary>
    /// 动作页信息模型
    /// </summary>
    public class ActionPageData
    {
        public string DefaultActionPageName { get; set; } // 内部默认的动作页名称，例如"Global0"
        public string ActionPageName { get; set; } // 动作页名称
        public DateTime LastEditTime { get; set; } // 最后编辑时间
    }
}