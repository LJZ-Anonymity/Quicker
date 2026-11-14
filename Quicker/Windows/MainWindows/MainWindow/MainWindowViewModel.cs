using Quicker.Models.Settings;
using System.ComponentModel;
using Quicker.Database.Core;
using System.Windows.Media;
using Quicker.Managers;
using System.Windows;

namespace Quicker.Windows.MainWindows.MainWindow
{
    /// <summary>
    /// 主窗口的视图模型，负责管理主界面的外观和行为属性，支持数据绑定和属性变更通知。
    /// </summary>
    internal class MainWindowViewModel : INotifyPropertyChanged
    {
        // 尺寸相关字段
        private double _buttonSize; // 按钮大小
        private double _buttonGap; // 按钮间隙
        private double _borderWidth; // 边框宽度
        private double _buttonCornerRadius; // 按钮圆角

        // 颜色相关字段
        private string _backgroundColor; // 背景颜色
        private string _borderColor; // 边框颜色
        private string _toolbarColor; // 工具栏颜色
        private string _toolbarIconColor; // 工具栏图标颜色
        private string _actionButtonColor; // 动作按钮颜色
        private string _actionButtonMouseOverColor; // 动作按钮鼠标悬停颜色
        private string _blankButtonColor; // 空白按钮颜色
        private string _blankButtonMouseOverColor; // 空白按钮鼠标悬停颜色
        private string _buttonTextColor; // 按钮文字颜色

        // 字体相关字段
        private double _fontSize; // 字体大小
        private FontWeight _fontWeight; // 修改类型

        // 背景图片相关字段
        private string _backgroundImagePath; // 背景图片路径
        private double _backgroundImageOpacity; // 背景图片不透明度

        // 模糊与圆角相关字段
        private int _blur; // 模糊模式
        private int _win11CornerRadius; // Win11圆角模式

        // 选项相关字段
        private bool _showActionButtonMouseOver; // 鼠标悬浮在动作按钮上时，放大显示按钮
        private bool _showAddImage; // 是否显示添加图片按钮

        private bool _isPinned; // 是否固定窗口
        private bool _isLocked; // 是否锁定窗口

        /// <summary>
        /// 背景图片路径
        /// </summary>
        public string BackgroundImagePath
        {
            get => _backgroundImagePath;
            set { _backgroundImagePath = value; OnPropertyChanged(nameof(BackgroundImagePath)); }
        }

        /// <summary>
        /// 背景图片不透明度
        /// </summary>
        public double BackgroundImageOpacity
        {
            get => _backgroundImageOpacity;
            set { _backgroundImageOpacity = value; OnPropertyChanged(nameof(BackgroundImageOpacity)); }
        }

        /// <summary>
        /// Win11圆角模式
        /// </summary>
        public int Win11CornerRadius
        {
            get => _win11CornerRadius;
            set { _win11CornerRadius = value; OnPropertyChanged(nameof(Win11CornerRadius)); }
        }

        /// <summary>
        /// 外观设置中的背景色（如 #FFF3F3F3）
        /// </summary>
        public string BackgroundColor
        {
            get => _backgroundColor;
            set { _backgroundColor = value; OnPropertyChanged(nameof(BackgroundColor)); }
        }

        /// <summary>
        /// 按钮大小
        /// </summary>
        public double ButtonSize
        {
            get => _buttonSize;
            set { _buttonSize = value; OnPropertyChanged(nameof(ButtonSize)); }
        }

        /// <summary>
        /// 按钮间隙
        /// </summary>
        public double ButtonGap
        {
            get => _buttonGap;
            set { _buttonGap = value; OnPropertyChanged(nameof(ButtonGap)); }
        }

        /// <summary>
        /// 外观设置中的工具栏颜色（如 #00F3F3F3）
        /// </summary>
        public string ToolbarColor
        {
            get => _toolbarColor;
            set { _toolbarColor = value; OnPropertyChanged(nameof(ToolbarColor)); }
        }

        /// <summary>
        /// 边框宽度
        /// </summary>
        public double BorderWidth
        {
            get => _borderWidth;
            set { _borderWidth = value; OnPropertyChanged(nameof(BorderWidth)); }
        }

        /// <summary>
        /// 按钮圆角
        /// </summary>
        public double ButtonCornerRadius
        {
            get => _buttonCornerRadius;
            set { _buttonCornerRadius = value; OnPropertyChanged(nameof(ButtonCornerRadius)); }
        }

        /// <summary>
        /// 边框颜色
        /// </summary>
        public string BorderColor
        {
            get => _borderColor;
            set { _borderColor = value; OnPropertyChanged(nameof(BorderColor)); }
        }

        /// <summary>
        /// 空白按钮颜色
        /// </summary>
        public string BlankButtonColor
        {
            get => _blankButtonColor;
            set { _blankButtonColor = value; OnPropertyChanged(nameof(BlankButtonColor)); }
        }

        /// <summary>
        /// 空白按钮鼠标悬停颜色
        /// </summary>
        public string BlankButtonMouseOverColor
        {
            get => _blankButtonMouseOverColor;
            set { _blankButtonMouseOverColor = value; OnPropertyChanged(nameof(BlankButtonMouseOverColor)); }
        }

        /// <summary>
        /// 字体大小
        /// </summary>
        public double FontSize
        {
            get => _fontSize;
            set { _fontSize = value; OnPropertyChanged(nameof(FontSize)); }
        }

        /// <summary>
        /// 字体粗细
        /// </summary>
        public FontWeight FontWeight
        {
            get => _fontWeight;
            set { _fontWeight = value; OnPropertyChanged(nameof(FontWeight)); }
        }

        /// <summary>
        /// 动作按钮颜色
        /// </summary>
        public string ActionButtonColor
        {
            get => _actionButtonColor;
            set { _actionButtonColor = value; OnPropertyChanged(nameof(ActionButtonColor)); }
        }

        /// <summary>
        /// 动作按钮鼠标悬停颜色
        /// </summary>
        public string ActionButtonMouseOverColor
        {
            get => _actionButtonMouseOverColor;
            set { _actionButtonMouseOverColor = value; OnPropertyChanged(nameof(ActionButtonMouseOverColor)); }
        }

        /// <summary>
        /// 鼠标悬浮在动作按钮上时，放大显示按钮
        /// </summary>
        public bool ShowActionButtonMouseOver
        {
            get => _showActionButtonMouseOver;
            set { _showActionButtonMouseOver = value; OnPropertyChanged(nameof(ShowActionButtonMouseOver)); }
        }

        /// <summary>
        /// 按钮文字颜色
        /// </summary>
        public string ButtonTextColor
        {
            get => _buttonTextColor;
            set { _buttonTextColor = value; OnPropertyChanged(nameof(ButtonTextColor)); }
        }

        /// <summary>
        /// 工具栏图标颜色
        /// </summary>
        public string ToolbarIconColor
        {
            get => _toolbarIconColor;
            set { _toolbarIconColor = value; OnPropertyChanged(nameof(ToolbarIconColor)); OnPropertyChanged(nameof(SelectedBrush)); OnPropertyChanged(nameof(UnSelectedBrush)); }
        }

        /// <summary>
        /// 选中页面按钮颜色（动态绑定 ToolbarIconColor）
        /// </summary>
        public SolidColorBrush SelectedBrush =>
            string.IsNullOrEmpty(ToolbarIconColor)
                ? new SolidColorBrush(Colors.Transparent)
                : new SolidColorBrush((Color)ColorConverter.ConvertFromString(ToolbarIconColor));

        /// <summary>
        /// 未选中页面按钮颜色（动态绑定 ToolbarIconColor 的浅色）
        /// </summary>
        public SolidColorBrush UnSelectedBrush
        {
            get
            {
                if (string.IsNullOrEmpty(ToolbarColor))
                    return new SolidColorBrush(Colors.Transparent);
                try
                {
                    var color = (Color)ColorConverter.ConvertFromString(ToolbarColor);
                    double factor = 0.8;
                    byte r = (byte)(color.R * factor);
                    byte g = (byte)(color.G * factor);
                    byte b = (byte)(color.B * factor);
                    return new SolidColorBrush(Color.FromArgb(color.A, r, g, b));
                }
                catch
                {
                    return new SolidColorBrush(Colors.Transparent);
                }
            }
        }

        /// <summary>
        /// 是否显示添加图片按钮
        /// </summary>
        public bool ShowAddImage
        {
            get => _showAddImage;
            set { _showAddImage = value; OnPropertyChanged(nameof(ShowAddImage)); }
        }

        /// <summary>
        /// 是否固定窗口
        /// </summary>
        public bool IsPinned
        {
            get => _isPinned;
            set { _isPinned = value; OnPropertyChanged(nameof(IsPinned)); }
        }

        /// <summary>
        /// 是否锁定窗口
        /// </summary>
        public bool IsLocked
        {
            get => _isLocked;
            set { _isLocked = value; OnPropertyChanged(nameof(IsLocked)); }
        }

        /// <summary>
        /// 模糊模式
        /// </summary>
        public int Blur
        {
            get => _blur;
            set { _blur = value; OnPropertyChanged(nameof(Blur)); }
        }

        /// <summary>
        /// 构造函数，初始化视图模型并从数据库加载外观和行为设置。
        /// </summary>
        public MainWindowViewModel()
        {
            // 加载数据库数据
            var appearance = SettingDatabase.GetAppearanceSettings()?.FirstOrDefault();
            if (appearance != null)
            {
                BackgroundImagePath = appearance.BackgroundImagePath;
                BackgroundImageOpacity = appearance.BackgroundImageOpacity;
                Win11CornerRadius = appearance.Win11CornerRadius;
                BackgroundColor = appearance.BackgroundColor;
                ButtonSize = appearance.ButtonSize;
                ButtonGap = appearance.ButtonGap;
                ToolbarColor = appearance.ToolbarColor;
                BorderWidth = appearance.BorderWidth;
                ButtonCornerRadius = appearance.ButtonCornerRadius;
                BorderColor = appearance.BorderColor;
                BlankButtonColor = appearance.BlankButtonColor;
                BlankButtonMouseOverColor = appearance.BlankButtonMouseOverColor;
                FontSize = appearance.FontSize;
                FontWeight = Quicker.Converters.FontWeightConverter.IndexToFontWeight(appearance.FontWeight); // 调用FontWeightConverter
                ActionButtonColor = appearance.ActionButtonColor;
                ActionButtonMouseOverColor = appearance.ActionButtonMouseOverColor;
                ShowActionButtonMouseOver = appearance.ShowActionButtonMouseOver;
                ButtonTextColor = appearance.ButtonTextColor;
                ToolbarIconColor = appearance.ToolbarIconColor;
                Blur = appearance.Blur;
            }
            var convention = SettingDatabase.GetAllConventions()?.FirstOrDefault();
            ShowAddImage = convention?.ShowAddImage ?? false;
            IsPinned = AppStateManager.MainWindowPinned;
            IsLocked = AppStateManager.Locked;
        }

        /// <summary>
        /// 属性变更事件，支持数据绑定通知。
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;
        /// <summary>
        /// 触发属性变更通知。
        /// </summary>
        /// <param name="name">属性名</param>
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        /// <summary>
        /// 直接应用指定的外观设置（供事件实时刷新）
        /// </summary>
        public void ApplyAppearance(Appearance appearance)
        {
            if (appearance == null) return;
            BackgroundImagePath = appearance.BackgroundImagePath;
            BackgroundImageOpacity = appearance.BackgroundImageOpacity;
            Win11CornerRadius = appearance.Win11CornerRadius;
            BackgroundColor = appearance.BackgroundColor;
            ButtonSize = appearance.ButtonSize;
            ButtonGap = appearance.ButtonGap;
            ToolbarColor = appearance.ToolbarColor;
            BorderWidth = appearance.BorderWidth;
            ButtonCornerRadius = appearance.ButtonCornerRadius;
            BorderColor = appearance.BorderColor;
            BlankButtonColor = appearance.BlankButtonColor;
            BlankButtonMouseOverColor = appearance.BlankButtonMouseOverColor;
            FontSize = appearance.FontSize;
            FontWeight = Quicker.Converters.FontWeightConverter.IndexToFontWeight(appearance.FontWeight);
            ActionButtonColor = appearance.ActionButtonColor;
            ActionButtonMouseOverColor = appearance.ActionButtonMouseOverColor;
            ShowActionButtonMouseOver = appearance.ShowActionButtonMouseOver;
            ButtonTextColor = appearance.ButtonTextColor;
            ToolbarIconColor = appearance.ToolbarIconColor;
            Blur = appearance.Blur;
        }

        // 保留原来的 ReloadAppearance，改成调用 ApplyAppearance
        public void ReloadAppearance()
        {
            var appearance = SettingDatabase.GetAppearanceSettings()?.FirstOrDefault();
            ApplyAppearance(appearance);
        }
    }
}