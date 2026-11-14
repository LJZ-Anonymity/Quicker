namespace Quicker.Models.Settings
{
    /// <summary>
    /// 基础设置模型
    /// </summary>
    public class Convention
    {
        public int ID { get; set; } // 主键
        public bool AutoStart { get; set; } // 是否开机自启
        public bool ShowNotification { get; set; } // 是否显示通知
        public bool ShowAddImage { get; set; } // 是否显示添加图片
        public double TotalUsageTime { get; set; } // 总使用时长
        public bool HideTooltip { get; set; } // 是否隐藏提示
        public int LongPressThreshold { get; set; } // 长按阈值
        public int MouseMovePixels { get; set; } // 鼠标移动像素
        public bool LoopPageFlipping { get; set; } // 是否循环翻页
        public bool RememberLastPage { get; set; } // 是否记住设置窗口中最后打开的页面
        public int LastPage { get; set; } // 设置窗口中最后打开的页面
        public bool EnableMemoryOptimization { get; set; } // 是否启用内存优化
        public string TrayIconPathRunning { get; set; } // 运行时托盘图标路径
        public string TrayIconPathPaused { get; set; } // 暂停时托盘图标路径
        public bool UseMenuAnimation { get; set; } // 是否启用菜单动画
        public bool IsDarkTheme { get; set; } // 是否使用暗色主题
    }
}