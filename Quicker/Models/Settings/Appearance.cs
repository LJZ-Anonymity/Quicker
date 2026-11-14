namespace Quicker.Models.Settings
{
    /// <summary>
    /// 外观设置模型
    /// </summary>
    public class Appearance
    {
        public string ThemeName { get; set; } // 主键
        // 尺寸
        public double ButtonSize { get; set; } // 按钮大小
        public double ButtonGap { get; set; } // 按钮间隙
        public double BorderWidth { get; set; } // 边框宽度
        public double ButtonCornerRadius { get; set; } // 按钮圆角

        // 颜色
        public string BackgroundColor { get; set; } // 背景颜色
        public string BorderColor { get; set; } // 边框颜色
        public string ToolbarColor { get; set; } // 工具栏颜色
        public string ToolbarIconColor { get; set; } // 工具栏图标颜色
        public string ActionButtonColor { get; set; } // 动作按钮颜色
        public string ActionButtonMouseOverColor { get; set; } // 动作按钮鼠标悬停颜色
        public string BlankButtonColor { get; set; } // 空白按钮颜色
        public string BlankButtonMouseOverColor { get; set; } // 空白按钮鼠标悬停颜色
        public string ButtonTextColor { get; set; } // 按钮文字颜色

        // 字体
        public int Font1 { get; set; } // 字体1
        public int Font2 { get; set; } // 字体2
        public double FontSize { get; set; } // 字体大小
        public int FontWeight { get; set; } // 字体粗细

        // 背景图片
        public string BackgroundImagePath { get; set; } // 背景图片路径
        public double BackgroundImageOpacity { get; set; } // 背景图片不透明度

        // 模糊与圆角
        public int Blur { get; set; } // 模糊模式
        public int Win11CornerRadius { get; set; } // Win11圆角模式

        // 选项
        public bool AutoHideTitleBar { get; set; } // 自动缩小动作名称文字
        public bool ShowActionButtonMouseOver { get; set; } // 鼠标悬浮在动作按钮上时，放大显示按钮
        public bool HideActionNameAfterIcon { get; set; } // 设置动作图标后隐藏动作名称
        public bool ShowActionIconShadow { get; set; } // 动作图标显示阴影
        public bool EnablePreview { get; set; } // 开启预览功能
    }
}