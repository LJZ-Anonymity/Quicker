namespace Quicker.Models
{
    /// <summary>
    /// 场景数据模型
    /// </summary>
    public class SceneData
    {
        public string SceneName { get; set; } // 场景名称
        public string SceneIconPath { get; set; } // 场景图标路径
        public int SceneCount { get; set; } // 场景数量
        public string SceneTag { get; set; } // 场景标签（不带后缀的文件名）
        public bool AutoReturnToFirstPage { get; set; } // 是否自动返回第一个页面
        public string SceneProcess { get; set; } // 场景所属程序名称
    }
}