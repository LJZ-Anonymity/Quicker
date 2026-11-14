namespace Quicker.Models.Settings
{
    /// <summary>
    /// 打开主窗口的条件模型
    /// </summary>
    public class OpenMainWindow
    {
        public int ID { get; set; } // 主键
        public bool OpenMainWindowByMiddleMouseClick { get; set; } // 按下中键
        public bool OpenMainWindowByX1MouseClick { get; set; } // 按下X1键
        public bool OpenMainWindowByX2MouseClick { get; set; } // 按下X2键
        public bool OpenMainWindowByCtrl_MiddleMouseClick { get; set; } // Ctrl+中键单击
        public bool OpenMainWindowByCtrl_RightMouseClick { get; set; } // Ctrl+右键单击
        public bool OpenMainWindowByMiddleMouseClickLonger { get; set; } // 长按中键
        public bool OpenMainWindowByRightMouseClickLonger { get; set; } // 长按右键
        public bool OpenMainWindowByRightMouseClick_Move { get; set; } // 按右键移动
        public bool OpenMainWindowByCtrl { get; set; } // 单击Ctrl键
        public int WindowStartupLocation { get; set; } // 功能面板打开位置
    }
}