using SixLabors.ImageSharp.Formats.Png.Chunks;
using Color = System.Windows.Media.Color;
using Image = SixLabors.ImageSharp.Image;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using Point = System.Windows.Point;
using Quicker.Windows.ToolWindows;
using System.Windows.Threading;
using Quicker.Models.Settings;
using System.Windows.Controls;
using Quicker.Database.Core;
using System.ComponentModel;
using System.Windows.Input;
using System.Windows.Media;
using SixLabors.ImageSharp;
using System.Diagnostics;
using Quicker.Managers;
using System.Text.Json;
using Quicker.Helpers;
using System.Windows;
using WpfAnimatedGif;
using Quicker.Models;
using System.Text;
using System.IO;

namespace Quicker.UserControls.SettingWindow.BasicSettings
{
    public partial class AppearanceGrid : UserControl, INotifyPropertyChanged // 实现INotifyPropertyChanged接口，支持属性变更通知
    {
        #region 成员字段

        private WeakReference<Quicker.Windows.MainWindows.SettingWindow> weakSettingWindow; // 弱引用设置窗口
        private readonly ButtonManager buttonManager = new(); // 添加按钮管理器
        private readonly List<string> _tempFiles = new(); // 临时文件列表，用于清理
        private DispatcherTimer _settingsChangeTimer; // 设置变化检测计时器
        private bool _isLoadingGlobalButtons = false; // 是否正在加载全局按钮
        private readonly ButtonDatabase db2 = new(); // 添加按钮数据库
        private SolidColorBrush _currentBrush; // 当前选中的颜色画刷
        private bool _isInitializing = true; // 是否正在初始化
        private Button _currentColorButton; // 记录当前按钮
        SettingManager settingManager; // 设置管理器

        #endregion

        #region INotifyPropertyChanged

        // INotifyPropertyChanged接口实现
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        #endregion

        #region 公开属性

        // 预览区圆角属性，绑定到XAML的CornerRadius
        private CornerRadius _previewCornerRadius = new CornerRadius(5); // 默认圆角为5
        public CornerRadius PreviewCornerRadius
        {
            get => _previewCornerRadius;
            set
            {
                if (_previewCornerRadius != value)
                {
                    _previewCornerRadius = value;
                    OnPropertyChanged(nameof(PreviewCornerRadius)); // 通知界面属性变更
                }
            }
        }

        // 标题栏圆角属性，绑定到XAML
        private CornerRadius _titleBarCornerRadius = new CornerRadius(5, 5, 0, 0); // 默认5,5,0,0
        public CornerRadius TitleBarCornerRadius
        {
            get => _titleBarCornerRadius;
            set
            {
                if (_titleBarCornerRadius != value)
                {
                    _titleBarCornerRadius = value;
                    OnPropertyChanged(nameof(TitleBarCornerRadius));
                }
            }
        }

        // 按钮文字颜色Brush属性，供XAML和代码绑定
        private SolidColorBrush _buttonTextColorBrush = new SolidColorBrush(Colors.Black);
        public SolidColorBrush ButtonTextColorBrush
        {
            get => _buttonTextColorBrush;
            set
            {
                if (_buttonTextColorBrush != value)
                {
                    _buttonTextColorBrush = value;
                    OnPropertyChanged(nameof(ButtonTextColorBrush));
                }
            }
        }
        #endregion

        #region 构造与初始化入口
        public AppearanceGrid(Quicker.Windows.MainWindows.SettingWindow settingWindow)
        {
            InitializeComponent(); // 初始化xaml界面
            settingManager = settingWindow._settingManager; // 初始化设置管理器
            weakSettingWindow = new(settingWindow); // 保存设置窗口
            this.DataContext = this; // 设置自身为DataContext，便于属性绑定
            
            // 延迟初始化，避免阻塞UI线程
            Dispatcher.BeginInvoke(new Action(async () =>
            {
                await InitializeAsync(); // 异步初始化
                InitializeSettingsChangeTimer(); // 初始化设置变化检测计时器
                await InitializeFontComboBoxes(); // 异步初始化字体下拉框
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        #endregion

        #region 设置变化计时器
        // 初始化设置变化检测计时器
        private void InitializeSettingsChangeTimer()
        {
            _settingsChangeTimer = new DispatcherTimer();
            _settingsChangeTimer.Interval = TimeSpan.FromMilliseconds(500); // 每0.5秒检测一次
            _settingsChangeTimer.Tick += SettingsChangeTimer_Tick;
            _settingsChangeTimer.Start();
        }

        // 计时器事件处理
        private void SettingsChangeTimer_Tick(object sender, EventArgs e)
        {
            if (settingManager != null)
            {
                bool hasChanges = settingManager.IsMainAppearanceSettingsChanged();
                ResetAppearanceButton.Visibility = hasChanges ? Visibility.Visible : Visibility.Collapsed;
            }
        }
        #endregion

        #region 初始化与加载设置
        // 异步初始化方法
        private async Task InitializeAsync()
        {
            await LoadSettingsAsync(); // 异步加载设置
        }

        // 异步加载设置
        private async Task LoadSettingsAsync()
        {
            await settingManager.LoadAppearanceAsync(); // 初始化缓存数据
            // 分批更新UI，减少闪烁
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                // 第一批：选项设置（包含预览）
                ApplyOptionSettings();
                ResetAppearanceButton.Visibility = Visibility.Collapsed; // 初始化重置按钮为隐藏状态
            });

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                // 第二批：基础设置
                ApplyButtonSettings();
                ApplyColorSettings();
                ApplyFontSettings();
            });
            
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                // 第三批：背景和模糊设置
                ApplyBackgroundImageSettings();
                ApplyBlurAndCornerSettings();
            });

            // 初始化完成
            _isInitializing = false;
            
            // 初始化完成后，如果预览功能开启，则加载一次全局按钮
            if (settingManager.appearanceConditions.EnablePreview)
            {
                await LoadGlobalButtonsForPreviewInternal();
            }
        }
        #endregion

        #region 应用设置到UI
        // 按钮相关设置
        private void ApplyButtonSettings()
        {
            ButtonSizeSlider.Value = settingManager.appearanceConditions.ButtonSize; // 设置按钮大小
            ButtonGapSlider.Value = settingManager.appearanceConditions.ButtonGap; // 设置按钮间距
            BorderWidthSlider.Value = settingManager.appearanceConditions.BorderWidth; // 设置边框宽度
            ButtonCornerRadiusSlider.Value = settingManager.appearanceConditions.ButtonCornerRadius; // 设置按钮圆角半径
        }

        // 颜色相关设置
        private void ApplyColorSettings()
        {
            BackgroundColorButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(settingManager.appearanceConditions.BackgroundColor));
            BorderColorButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(settingManager.appearanceConditions.BorderColor));
            ToolbarColorButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(settingManager.appearanceConditions.ToolbarColor));
            ToolbarIconColorButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(settingManager.appearanceConditions.ToolbarIconColor));
            ActionButtonColorButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(settingManager.appearanceConditions.ActionButtonColor));
            ActionButtonMouseOverColorButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(settingManager.appearanceConditions.ActionButtonMouseOverColor));
            BlankButtonColorButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(settingManager.appearanceConditions.BlankButtonColor));
            BlankButtonMouseOverColorButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(settingManager.appearanceConditions.BlankButtonMouseOverColor));
            TextColorButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(settingManager.appearanceConditions.ButtonTextColor));
            ButtonTextColorBrush = TextColorButton.Background as SolidColorBrush;
        }

        // 字体相关设置
        private void ApplyFontSettings()
        {
            FontSizeComboBox1.SelectedIndex = settingManager.appearanceConditions.Font1; // 设置字体1
            FontSizeComboBox2.SelectedIndex = settingManager.appearanceConditions.Font2; // 设置字体2
            FontSizeSlider.Value = settingManager.appearanceConditions.FontSize; // 设置字体大小
            FontWeightComboBox.SelectedIndex = settingManager.appearanceConditions.FontWeight; // 设置字体粗细
            ApplyGlobalFontFamily(); // 应用全局字体
        }

        // 背景图片相关设置
        private void ApplyBackgroundImageSettings()
        {
            BackgroundImagePath = settingManager.appearanceConditions.BackgroundImagePath; // 设置背景图片路径
            BackgroundImageOpacitySlider.Value = settingManager.appearanceConditions.BackgroundImageOpacity; // 设置背景图片不透明度
        }

        // 模糊与圆角相关设置
        private void ApplyBlurAndCornerSettings()
        {
            BlurComboBox.SelectedIndex = settingManager.appearanceConditions.Blur; // 设置模糊模式
            Win11CornerRadiusComboBox.SelectedIndex = settingManager.appearanceConditions.Win11CornerRadius; // 设置Win11圆角模式
            UpdatePreviewCornerRadiusByComboBox(); // 同步预览区圆角显示
        }

        // 选项相关设置
        private void ApplyOptionSettings()
        {
            AutoHideTitleBarCheckBox.IsChecked = settingManager.appearanceConditions.AutoHideTitleBar; // 设置自动隐藏标题栏
            ShowActionButtonMouseOverCheckBox.IsChecked = settingManager.appearanceConditions.ShowActionButtonMouseOver; // 设置鼠标悬停显示动作按钮
            HideActionNameAfterIconCheckBox.IsChecked = settingManager.appearanceConditions.HideActionNameAfterIcon; // 设置隐藏动作名称
            ShowActionIconShadowCheckBox.IsChecked = settingManager.appearanceConditions.ShowActionIconShadow; // 设置显示动作图标阴影

            EnablePreviewCheckBox.IsChecked = settingManager.appearanceConditions.EnablePreview; // 设置显示设置应用效果

            // 直接设置预览区域可见性，避免调用EnablePreviewCheckBox_Click导致的逻辑问题
            ViewPreviewBorder.Visibility = settingManager.appearanceConditions.EnablePreview ? Visibility.Visible : Visibility.Collapsed;

            // 如果开启预览，延迟加载预览区域（仅在非初始化期间）
            if (settingManager.appearanceConditions.EnablePreview && !_isInitializing)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    LoadGlobalButtonsForPreview(); // 加载全局按钮到预览区
                }), System.Windows.Threading.DispatcherPriority.Background);
            }
        }
        #endregion

        #region 滚动同步
        // 同步滚动条数据
        private void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (ScrollViewer == null) return; // 防止空引用
            AppearanceScrollBar.Maximum = ScrollViewer.ScrollableHeight; // 设置最大值
            AppearanceScrollBar.ViewportSize = ScrollViewer.ViewportHeight; // 设置视口大小
            AppearanceScrollBar.Value = ScrollViewer.VerticalOffset; // 设置当前值
        }
        private void AppearanceScrollBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            ScrollViewer.ScrollToVerticalOffset(AppearanceScrollBar.Value); // 设置滚动条值
        }
        #endregion

        #region 复选框事件
        // 复选框点击事件
        private void CheckBox_Click(object sender, RoutedEventArgs e)
        {
            var checkBox = sender as CheckBox; // 获取CheckBox控件
            if (checkBox == null) return;
            bool value = checkBox.IsChecked == true; // 获取当前勾选状态
            switch (checkBox.Name) // 根据控件名称区分处理
            {
                case "AutoHideTitleBarCheckBox":
                    settingManager.appearanceConditions.AutoHideTitleBar = value; // 自动缩小动作名称文字
                    break;
                case "ShowActionButtonMouseOverCheckBox":
                    settingManager.appearanceConditions.ShowActionButtonMouseOver = value; // 鼠标悬浮放大动作按钮
                    break;
                case "HideActionNameAfterIconCheckBox":
                    settingManager.appearanceConditions.HideActionNameAfterIcon = value; // 设置动作图标后隐藏动作名称
                    break;
                case "ShowActionIconShadowCheckBox":
                    settingManager.appearanceConditions.ShowActionIconShadow = value; // 动作图标显示阴影
                    break;
                case "EnablePreviewCheckBox":
                    settingManager.appearanceConditions.EnablePreview = value; // 开启/关闭预览
                    break;
                default:
                    return; // 其它CheckBox不处理
            }
            SettingDatabase.UpdateAppearance(settingManager.appearanceConditions); // 更新外观设置到数据库
            
            // 在非初始化期间才刷新预览
            if (!_isInitializing)
            {
                LoadGlobalButtonsForPreview(); // 刷新预览区按钮内容和样式
            }
        }
        #endregion

        #region 颜色选择
        // 颜色按钮点击事件，弹出颜色选择器并初始化为当前颜色
        private void ColorButton_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button; // 获取按钮
            if (button == null) return; // 如果按钮为null，则返回
            _currentColorButton = button; // 记录当前按钮
            _currentBrush = button.Background as SolidColorBrush; // 获取按钮的Background作为当前选中的颜色画刷
            if (_currentBrush == null) return; // 如果brush为null，则返回

            PopupColorPicker.ResetColorControls(_currentBrush.Color); // 重置色彩选择器为当前颜色
            PopupColorPicker.SelectedColor = _currentBrush.Color; // 同步SelectedColor依赖属性

            // 设置Popup的位置并打开
            ColorPickerPopup.PlacementTarget = button; // 设置弹窗目标为当前按钮
            ColorPickerPopup.IsOpen = true; // 打开颜色选择器弹窗
        }

        // 处理颜色选择器颜色变化事件
        private void PopupColorPicker_SelectedColorChanged(object sender, ColorChangedEventArgs e)
        {
            if (_currentBrush != null)
            {
                _currentBrush.Color = e.NewColor;
                UpdateAppearanceColorProperty(_currentBrush, e.NewColor);
                if (_currentColorButton != null && _currentColorButton.Name == "TextColorButton") // 如果当前是文字颜色按钮，顺便同步ViewModel属性
                {
                    ButtonTextColorBrush.Color = e.NewColor;
                    settingManager.appearanceConditions.ButtonTextColor = e.NewColor.ToString(); // 同步更新数据库字段
                }
                SettingDatabase.UpdateAppearance(settingManager.appearanceConditions);
            }
        }

        // 按钮名到属性名的映射字典
        private static readonly Dictionary<string, string> ColorButtonToProperty = new()
        {
            { "BackgroundColorButton", "BackgroundColor" },
            { "BorderColorButton", "BorderColor" },
            { "ToolbarColorButton", "ToolbarColor" },
            { "ToolbarIconColorButton", "ToolbarIconColor" },
            { "ActionButtonColorButton", "ActionButtonColor" },
            { "ActionButtonMouseOverColorButton", "ActionButtonMouseOverColor" },
            { "BlankButtonColorButton", "BlankButtonColor" },
            { "BlankButtonMouseOverColorButton", "BlankButtonMouseOverColor" },
            { "TextColorButton", "ButtonTextColor" },
            { "ActionIconColorButton", "ActionIconColor" },
            { "TriggerKeyTextColorButton", "TriggerKeyTextColor" },
            { "OtherIconColorButton", "OtherIconColor" }
        };

        /// <summary>
        /// 根据当前颜色按钮的Name，自动设置appearanceConditions的对应属性
        /// </summary>
        /// <param name="brush">当前选中的颜色画刷</param>
        /// <param name="color">新颜色</param>
        private void UpdateAppearanceColorProperty(SolidColorBrush brush, Color color)
        {
            if (_currentColorButton == null) return;
            if (ColorButtonToProperty.TryGetValue(_currentColorButton.Name, out string propertyName))
            {
                var prop = settingManager.appearanceConditions.GetType().GetProperty(propertyName);
                if (prop != null && prop.CanWrite)
                {
                    prop.SetValue(settingManager.appearanceConditions, color.ToString());
                }
            }
        }
        #endregion

        #region 资源释放
        // 释放资源
        private void AppearanceGrid_Unloaded(object sender, RoutedEventArgs e)
        {
            // 停止并清理计时器
            if (_settingsChangeTimer != null)
            {
                _settingsChangeTimer.Stop();
                _settingsChangeTimer.Tick -= SettingsChangeTimer_Tick;
                _settingsChangeTimer = null;
            }

            // 清理UI元素
            GlobalGrid.Children.Clear(); // 清空按钮
            // 清理颜色画刷资源
            if (_currentBrush != null)
            {
                _currentBrush = null;
            }

            if (_buttonTextColorBrush != null)
            {
                _buttonTextColorBrush = null;
            }
            settingManager = null; // 释放设置管理器
            weakSettingWindow = null; // 释放弱引用设置窗口
            _currentColorButton = null; // 释放当前颜色按钮引用
            PropertyChanged = null; // 清理事件绑定
            foreach (string tempFile in _tempFiles) // 清理临时文件
            {
                try
                {
                    if (File.Exists(tempFile))
                    {
                        File.Delete(tempFile);
                    }
                }
                catch
                {
                    // 忽略删除失败的情况
                }
            }
            _tempFiles.Clear();
        }
        #endregion

        #region 预置样式（菜单入口）
        // 点击按钮打开预设菜单
        private void PresetStyleButton_Click(object sender, RoutedEventArgs e)
        {
            PresetStylePopup.IsOpen = true; // 打开预设样式弹出窗口
        }
        #endregion

        #region 预览加载与交互

        // 为预览加载全局按钮
        private async void LoadGlobalButtonsForPreview()
        {
            if (_isLoadingGlobalButtons) return; // 防止重复调用
            if (_isInitializing) // 在初始化期间，只在最后一次调用时执行
            {
                Dispatcher.BeginInvoke(new Action(async () => // 延迟执行，确保是最后一次调用
                {
                    await Task.Delay(100); // 等待100ms，确保其他初始化调用完成
                    if (!_isInitializing) // 如果初始化已完成，则执行加载
                    {
                        await LoadGlobalButtonsForPreviewInternal();
                    }
                }), System.Windows.Threading.DispatcherPriority.Background);
                return;
            }

            await LoadGlobalButtonsForPreviewInternal();
        }

        /// <summary>
        /// 内部方法：实际执行全局按钮加载逻辑
        /// </summary>
        private async Task LoadGlobalButtonsForPreviewInternal()
        {
            if (_isLoadingGlobalButtons) return;
            _isLoadingGlobalButtons = true;
            try
            {
                // 异步加载按钮数据，避免阻塞UI
                var globalButtons = await Task.Run(() => db2.GetPagesOfButtons("_global", 0)); // 异步加载全局按钮数据
                var buttons = GlobalGrid.Children.OfType<Button>().ToList(); // 异步加载全局按钮数据
                bool hasChanges = ProcessButtonsWithData(buttons, globalButtons); // 处理按钮数据并判断是否有数据变更
                if (!hasChanges) // 如果没有数据变更，则使用默认数据
                {
                    ApplyDefaultDataToGlobal11(buttons);
                }
            }
            finally
            {
                _isLoadingGlobalButtons = false;
            }
        }

        /// <summary>
        /// 处理按钮数据，为每个按钮设置对应的数据或清空
        /// </summary>
        /// <param name="buttons">按钮列表</param>
        /// <param name="globalButtons">全局按钮数据</param>
        /// <returns>是否有数据变更</returns>
        private bool ProcessButtonsWithData(List<Button> buttons, List<ButtonData> globalButtons)
        {
            bool hasChanges = false; // 是否有数据变更
            for (int i = 0; i < buttons.Count; i++)
            {
                var btn = buttons[i]; // 获取按钮
                int buttonIndex = int.Parse(btn.Name.Replace("_global", "")); // 获取按钮索引
                var buttonData = globalButtons.FirstOrDefault(b => b.ButtonID == buttonIndex);// 根据按钮ID查找对应的按钮数据
                if (buttonData != null)
                {
                    hasChanges = true; // 有数据变更
                    ApplyButtonData(btn, buttonData); // 应用按钮数据
                }
                else
                {
                    btn.Background = BlankButtonColorButton.Background;
                }
            }
            return hasChanges;
        }

        /// <summary>
        /// 为按钮应用数据
        /// </summary>
        /// <param name="button">目标按钮</param>
        /// <param name="buttonData">按钮数据</param>
        private void ApplyButtonData(Button button, ButtonData buttonData)
        {
            button.Background = ActionButtonColorButton.Background; // 默认为动作按钮颜色
            buttonManager.RefreshButtonDisplay(button, buttonData, 0); // 刷新按钮显示
        }

        /// <summary>
        /// 为_global11按钮应用默认数据
        /// </summary>
        /// <param name="buttons">按钮列表</param>
        private void ApplyDefaultDataToGlobal11(List<Button> buttons)
        {
            var defaultData = CreateDefaultButtonData(); // 创建默认按钮数据
            var global11Button = buttons.FirstOrDefault(b => b.Name == "_global11"); // 获取_global11按钮
            if (global11Button != null)
            {
                ApplyButtonData(global11Button, defaultData); // 应用默认按钮数据
            }
        }

        /// <summary>
        /// 创建默认按钮数据
        /// </summary>
        /// <returns>默认按钮数据</returns>
        private ButtonData CreateDefaultButtonData()
        {
            return new ButtonData
            {
                ButtonID = 11,
                Title = "Quicker",
                Location = "",
                ImagePath = "pack://application:,,,/Resources/Images/Quicker_Enabled.png",
                Data1 = "",
                Data2 = "",
                Data3 = "",
                Description = "",
                CreateTime = DateTime.Now,
                LatestEditTime = DateTime.Now,
                ActionType = ActionType.OpenFile,
                UsedTimes = 0
            };
        }

        private void PreviewButton_MouseEnter(object sender, EventArgs e)
        {
            var btn = sender as Button; // 获取按钮
            btn.Background = btn.Tag == null
                ? BlankButtonMouseOverColorButton.Background
                : ActionButtonMouseOverColorButton.Background; // 绑定颜色选择

            // 判断是否需要放大按钮
            if (ShowActionButtonMouseOverCheckBox.IsChecked == true &&
                btn.Tag != null)
            {
                btn.RenderTransformOrigin = new Point(0.5, 0.5); // 设置缩放中心为按钮中心
                btn.RenderTransform = new ScaleTransform(1.05, 1.05); // 放大1.05倍
            }
        }

        // 鼠标移出 Button 还原 Background，并还原按钮大小
        private void PreviewButton_MouseLeave(object sender, EventArgs e)
        {
            var btn = sender as Button; // 获取按钮
            btn.Background = btn.Tag == null
                ? BlankButtonColorButton.Background
                : ActionButtonColorButton.Background; // 绑定颜色选择

            // 判断是否需要还原按钮大小
            if (ShowActionButtonMouseOverCheckBox.IsChecked == true)
            {
                btn.RenderTransform = null; // 还原为默认大小
            }
        }
        #endregion

        #region 滑块事件
        // 滑块值改变事件，统一处理所有相关Slider
        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            var slider = sender as Slider; // 获取Slider控件
            if (slider == null || settingManager == null) return; // 防止空引用
            switch (slider.Name)
            {
                case "ButtonSizeSlider":
                    settingManager.appearanceConditions.ButtonSize = slider.Value; // 设置按钮大小
                    break;
                case "ButtonGapSlider":
                    settingManager.appearanceConditions.ButtonGap = slider.Value; // 设置按钮间距
                    break;
                case "BorderWidthSlider":
                    settingManager.appearanceConditions.BorderWidth = slider.Value; // 设置边框宽度
                    break;
                case "ButtonCornerRadiusSlider":
                    settingManager.appearanceConditions.ButtonCornerRadius = slider.Value; // 设置按钮圆角
                    break;
                case "FontSizeSlider":
                    settingManager.appearanceConditions.FontSize = slider.Value; // 设置字体大小
                    break;
                case "BackgroundImageOpacitySlider":
                    settingManager.appearanceConditions.BackgroundImageOpacity = slider.Value; // 设置背景图片不透明度
                    break;
                default:
                    return;
            }
            SettingDatabase.UpdateAppearance(settingManager.appearanceConditions); // 更新外观设置到数据库
            if (!_isInitializing) // 在非初始化期间才刷新预览
            {
                LoadGlobalButtonsForPreview(); // 刷新预览区按钮内容和样式
            }
        }
        #endregion

        #region 字体与圆角
        // 下拉框选择改变事件
        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox) // 判断事件源是否为ComboBox
            {
                switch (comboBox.Name) // 根据ComboBox的Name区分处理
                {
                    case "BlurComboBox":
                        settingManager.appearanceConditions.Blur = comboBox.SelectedIndex; // 设置模糊模式
                        break;
                    case "Win11CornerRadiusComboBox":
                        settingManager.appearanceConditions.Win11CornerRadius = comboBox.SelectedIndex; // 设置Win11圆角模式
                        UpdatePreviewCornerRadiusByComboBox(); // 变更预览区圆角
                        break;
                    case "FontSizeComboBox1":
                        settingManager.appearanceConditions.Font1 = comboBox.SelectedIndex; // 设置字体1
                        break;
                    case "FontSizeComboBox2":
                        settingManager.appearanceConditions.Font2 = comboBox.SelectedIndex; // 设置字体2
                        break;
                    case "FontWeightComboBox":
                        settingManager.appearanceConditions.FontWeight = comboBox.SelectedIndex; // 设置字体粗细
                        break;
                    default:
                        return; // 其它ComboBox不处理
                }
                ApplyGlobalFontFamily(); // 每次字体选择后应用全局字体
                SettingDatabase.UpdateAppearance(settingManager.appearanceConditions); // 更新外观设置到数据库
            }
        }

        /// <summary>
        /// 根据Win11圆角模式ComboBox的选项，动态设置预览区圆角
        /// </summary>
        private void UpdatePreviewCornerRadiusByComboBox()
        {
            double previewRadius;
            switch (Win11CornerRadiusComboBox.SelectedIndex) // 0: 默认 1: 无 2: 圆角 3: 小圆角
            {
                case 1: // 无
                    previewRadius = 0;
                    break;
                case 3: // 小圆角
                    previewRadius = 3;
                    break;
                default: // 默认、圆角
                    previewRadius = 5;
                    break;
            }
            PreviewCornerRadius = new CornerRadius(previewRadius); // 预览区圆角
            TitleBarCornerRadius = new CornerRadius(previewRadius, previewRadius, 0, 0); // 标题栏只上面有圆角

            // 延迟到UI刷新后再裁剪
            Dispatcher.BeginInvoke(new Action(() =>
            {
                ClipHelper.UpdateBorderClip(ViewPreviewBorder);
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }
        #endregion

        #region 预览开关

        // "开启预览"复选框点击事件
        private void EnablePreviewCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if(settingManager == null) return; // 防止空引用
            settingManager.appearanceConditions.EnablePreview = EnablePreviewCheckBox.IsChecked == true; // 同步复选框状态到缓存
            SettingDatabase.UpdateAppearance(settingManager.appearanceConditions); // 更新外观设置到数据库
            ViewPreviewBorder.Visibility = (Visibility)(EnablePreviewCheckBox.IsChecked == true ? 0 : 2); // 切换预览区可见性
            if (EnablePreviewCheckBox.IsChecked == true) // 如果开启预览
            {
                LoadGlobalButtonsForPreview(); // 加载全局按钮到预览区
            }
        }

        #endregion

        #region 重置外观
        // 重置外观设置按钮点击事件
        private void ResetAppearanceButton_Click(object sender, RoutedEventArgs e)
        {
            ResetAppearanceButton.Visibility = Visibility.Collapsed; // 隐藏按钮
            if (settingManager != null)
            {
                // 保存当前预览设置
                bool currentPreviewSetting = settingManager.appearanceConditions.EnablePreview;

                // 恢复原始外观设置
                settingManager.RestoreOriginalAppearanceSettings();

                // 恢复预览设置为当前值（不重置预览设置）
                settingManager.appearanceConditions.EnablePreview = currentPreviewSetting;
                RefreshInterfaceDisplay(); // 刷新界面
                SettingDatabase.UpdateAppearance(settingManager.appearanceConditions); // 更新数据库
            }
        }
        #endregion

        #region 字体列表初始化与全局字体
        // 初始化字体下拉框
        private async Task InitializeFontComboBoxes()
        {
            // 异步加载字体列表，避免阻塞UI
            await Task.Run(() =>
            {
                var fontFamilies = Fonts.SystemFontFamilies.Select(f => f.Source).OrderBy(f => f).ToList();
                fontFamilies.Add("(系统默认)"); // 在最后插入一个空项
                
                Application.Current.Dispatcher.Invoke(() =>
                {
                    FontSizeComboBox1.ItemsSource = fontFamilies;
                    FontSizeComboBox2.ItemsSource = fontFamilies;
                });
            });
        }

        // 应用全局字体方法
        private void ApplyGlobalFontFamily()
        {
            // 获取当前 appearanceConditions 的 Font1/Font2 索引
            int font1Index = settingManager.appearanceConditions.Font1;
            int font2Index = settingManager.appearanceConditions.Font2;

            // ComboBox 的 ItemsSource 是字体名列表，最后一项是"(系统默认)"
            var fontFamilies = FontSizeComboBox1.ItemsSource as IList<string>;
            if (fontFamilies == null || fontFamilies.Count == 0)
            {
                return; // 字体列表未初始化，直接返回
            }
            int defaultIndex = fontFamilies.Count - 1;
            // 设置 ComboBox 选中项（防止越界）
            FontSizeComboBox1.SelectedIndex = (font1Index == -1 || font1Index < 0 || font1Index >= fontFamilies.Count) ? defaultIndex : font1Index;
            FontSizeComboBox2.SelectedIndex = (font2Index == -1 || font2Index < 0 || font2Index >= fontFamilies.Count) ? defaultIndex : font2Index;

            // 获取字体名
            string font1 = FontSizeComboBox1.SelectedItem as string;
            string font2 = FontSizeComboBox2.SelectedItem as string;

            // 防止 null
            bool isDefault1 = string.IsNullOrEmpty(font1) || font1 == "(系统默认)";
            bool isDefault2 = string.IsNullOrEmpty(font2) || font2 == "(系统默认)";
            FontFamily fontFamily;
            if (!isDefault1 && !isDefault2)
            {
                fontFamily = new FontFamily($"{font1}, {font2}");
            }
            else if (!isDefault1)
            {
                fontFamily = new FontFamily(font1);
            }
            else if (!isDefault2)
            {
                fontFamily = new FontFamily(font2);
            }
            else
            {
                fontFamily = new FontFamily("微软雅黑"); // 系统默认改为微软雅黑
            }

            Application.Current.Resources["GlobalFontFamily"] = fontFamily;
        }
        #endregion

        #region 背景图片选择与预览
        // 点击"选择..."按钮
        private void BackgroundImagePathButton_Click(object sender, RoutedEventArgs e)
        {
            BackgroundImagePathPopup.IsOpen = true; // 打开背景图片选择弹出窗口
        }

        // 选择图片
        private void SelectBackgroundImageButton_Click(object sender, RoutedEventArgs e)
        {
            BackgroundImagePathPopup.IsOpen = false; // 关闭弹窗
            var dialog = new System.Windows.Forms.OpenFileDialog()
            {
                Filter = "图片文件|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.svg;*.ico",
                Title = "选择背景图片"
            };
            var result = dialog.ShowDialog(); // 显示文件选择对话框
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                double aspectRatio;
                if(settingManager.appearanceConditions.EnablePreview) // 根据预览器可见性使用不同方法，保证性能
                {
                    aspectRatio = ViewPreviewBorder.ActualWidth / ViewPreviewBorder.ActualHeight; // 计算宽高比
                }
                else
                {
                    // 使用转换器逻辑计算宽高比
                    double btnSize = ButtonSizeSlider.Value;
                    double gap = ButtonGapSlider.Value;

                    // 计算宽度：btnSize * 4 + gap * 3 (4列，3个间隙)
                    double width = btnSize * 4 + gap * 3;

                    // 计算高度：31 + 25.5 + btnSize * 7 + gap * 5 (标题栏31 + 功能栏25.5 + 按钮区域 + 间隙)
                    double height = 31 + 25.5 + btnSize * 7 + gap * 5;

                    aspectRatio = width / height; // 计算宽高比
                }
                var imageCropWindow = new ImageCropWindow(dialog.FileName, aspectRatio, ViewPreviewBorder.CornerRadius); // 创建图片裁剪窗口
                RegisterImageCropWindowEvents(imageCropWindow); // 注册图片裁剪窗口的事件
                BackgroundImagePathButton.IsEnabled = false; // 禁用选择按钮
                imageCropWindow.Show(); // 显示图片裁剪窗口
            }
        }

        /// <summary>
        /// 注册图片裁剪窗口的事件，处理裁剪完成和窗口关闭
        /// </summary>
        /// <param name="imageCropWindow">图片裁剪窗口</param>
        private void RegisterImageCropWindowEvents(ImageCropWindow imageCropWindow)
        {
            Action<object, string> cropCompletedHandler = null; // 裁剪完成事件处理器
            EventHandler closedHandler = null; // 窗口关闭事件处理器

            // 裁剪完成事件：设置图片路径，启用按钮，并解绑事件
            cropCompletedHandler = (s, croppedPath) =>
            {
                OnImageCropCompleted(croppedPath);
                UnregisterImageCropWindowEvents(imageCropWindow, cropCompletedHandler, closedHandler);
            };

            // 窗口关闭事件：无论是否裁剪，均启用按钮并解绑事件
            closedHandler = (s, args) =>
            {
                BackgroundImagePathButton.IsEnabled = true; // 启用选择按钮
                UnregisterImageCropWindowEvents(imageCropWindow, cropCompletedHandler, closedHandler);
            };

            imageCropWindow.CropCompleted += cropCompletedHandler;
            imageCropWindow.Closed += closedHandler; 
        }

        /// <summary>
        /// 处理图片裁剪完成后的逻辑
        /// </summary>
        /// <param name="croppedPath">裁剪后的图片路径</param>
        private void OnImageCropCompleted(string croppedPath)
        {
            if (!string.IsNullOrEmpty(croppedPath))
            {
                BackgroundImagePathTextBox.Text = croppedPath; // 设置新图片路径
            }
            BackgroundImagePathButton.IsEnabled = true; // 启用选择按钮
        }

        /// <summary>
        /// 解绑图片裁剪窗口的事件，防止内存泄漏
        /// </summary>
        /// <param name="imageCropWindow">图片裁剪窗口</param>
        /// <param name="cropCompletedHandler">裁剪完成事件处理器</param>
        /// <param name="closedHandler">窗口关闭事件处理器</param>
        private void UnregisterImageCropWindowEvents(ImageCropWindow imageCropWindow, Action<object, string> cropCompletedHandler, EventHandler closedHandler)
        {
            imageCropWindow.CropCompleted -= cropCompletedHandler;
            imageCropWindow.Closed -= closedHandler;
        }

        // 点击"插入剪贴板"按钮
        private void InsertClipboardTextButton_Click(object sender, RoutedEventArgs e)
        {
            BackgroundImagePathPopup.IsOpen = false; // 关闭弹窗
            string imagePath = Clipboard.GetText().Trim().Replace("\"", ""); // 去除所有引号
            BackgroundImagePathTextBox.Text = imagePath;
            EnablePreviewCheckBox.IsChecked = true; // 开启预览
            EnablePreviewCheckBox_Click(null, null);
        }

        private string _backgroundImagePath; // 背景图片路径
        public string BackgroundImagePath
        {
            get => _backgroundImagePath;
            set
            {
                if (_backgroundImagePath != value)
                {
                    _backgroundImagePath = value;
                    OnPropertyChanged(nameof(BackgroundImagePath));
                    SetPreviewBackgroundImage(_backgroundImagePath); // 动态设置预览背景图片，支持GIF动图
                }
            }
        }

        // 右键清空图片路径
        private void BackgroundImagePathButton_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            BackgroundImagePathTextBox.Text = ""; // 清空路径
        }

        /// <summary>
        /// 设置预览区背景图片，支持GIF动图
        /// </summary>
        /// <param name="path">图片路径</param>
        private void SetPreviewBackgroundImage(string path)
        {
            if (!IsLoaded) return; // 避免控件未初始化时访问
            if (PreviewBackgroundImage == null) return; // 避免空引用
            var iconManager = new Quicker.Managers.IconManager();
            iconManager.SetImageWithGifSupport(PreviewBackgroundImage, path);
        }

        // 文本框文本变化时，保存背景图片路径
        private void BackgroundImagePathTextBox_TextChanged(object sender, RoutedEventArgs e)
        {
            SettingDatabase.UpdateAppearance(settingManager.appearanceConditions); // 更新外观设置到数据库
            settingManager.appearanceConditions.BackgroundImagePath = BackgroundImagePathTextBox.Text; // 保存路径到缓存
        }
        #endregion

        #region 外观分享与导入

        // 点击按钮分享外观
        private void ShareSaveAppearanceButton_Click(object sender, RoutedEventArgs e)
        {
            var appearance = settingManager.appearanceConditions; // 获取当前外观设置对象
            string json = JsonSerializer.Serialize(appearance); // 序列化为 JSON 字符串
            string inputPngPath = GetAppearanceCarrierImagePath(); // 获取作为分享载体的 PNG 图片路径（优先用用户自定义背景，否则用内置图片）
            if (Path.GetExtension(inputPngPath).ToLower() != ".png") // 如果不是PNG图片，自动转换为PNG
            {
                string tempPngPath = Path.GetTempFileName() + ".png";
                ConvertImageToPng(inputPngPath, tempPngPath);
                _tempFiles.Add(tempPngPath); // 添加到临时文件跟踪列表
                inputPngPath = tempPngPath;
            }
            inputPngPath = EnsureTrueColorPng(inputPngPath); // 保证是32位真彩色
            string outputPngPath = GetShareAppearanceOutputPath(); // 获取输出路径（自动创建保存文件夹，文件名带时间戳）
            WriteAppearanceToPng(inputPngPath, outputPngPath, json); // 写入 PNG 文件并嵌入 JSON 数据
            ShowToast("外观保存成功！", ToastType.Success); // 显示保存成功的 Toast 提示
            Process.Start("explorer.exe", $"/select,\"{outputPngPath}\""); // 打开资源管理器并选中刚保存的 PNG 文件
        }

        // 点击"已分享或保存的外观"按钮打开外观分享文件夹
        private void SharedSavedAppearanceButton_Click(object sender, RoutedEventArgs e)
        {
            AppPathHelper.EnsureDirectoryExists(AppPathHelper.SharedAppearanceFolder); // 确保目录存在
            Process.Start("explorer.exe", AppPathHelper.SharedAppearanceFolder); // 使用资源管理器打开该文件夹
        }

        /// <summary>
        /// 将任意图片文件转换为 PNG 格式并保存到指定路径
        /// </summary>
        /// <param name="inputPath">原始图片路径（支持jpg、bmp、gif等）</param>
        /// <param name="outputPath">输出PNG图片路径</param>
        private void ConvertImageToPng(string inputPath, string outputPath)
        {
            try
            {
                using (var image = SixLabors.ImageSharp.Image.Load(inputPath))
                {
                    image.Save(outputPath, new SixLabors.ImageSharp.Formats.Png.PngEncoder());
                }
            }
            catch
            {
                ShowToast("图片格式不受支持或文件损坏，已自动使用默认图片导出外观。", ToastType.Error);
                string defaultPath = GetAppearanceCarrierImagePath(); // 获取默认图片路径
                // 防止死循环：如果inputPath已经是默认图片，则不再递归
                if (!string.Equals(inputPath, defaultPath, StringComparison.OrdinalIgnoreCase))
                {
                    ConvertImageToPng(defaultPath, outputPath);
                }
            }
        }

        /// <summary>
        /// 获取外观分享载体图片路径（优先用用户自定义背景，否则用内置图片）
        /// </summary>
        /// <returns>载体图片路径</returns>
        private string GetAppearanceCarrierImagePath()
        {
            // 如果用户设置了背景图片且文件存在，则直接用该图片
            if (!string.IsNullOrEmpty(BackgroundImagePathTextBox.Text) && File.Exists(BackgroundImagePathTextBox.Text))
            {
                return BackgroundImagePathTextBox.Text;
            }
            else // 否则用内置资源图片，先保存到临时文件
            {
                var resourceUri = new Uri("pack://application:,,,/Resources/Images/Quicker_Enabled.png"); // 内置图片资源路径
                var streamInfo = Application.GetResourceStream(resourceUri); // 获取资源流
                string tempPath = Path.GetTempFileName() + ".png"; // 临时文件路径
                _tempFiles.Add(tempPath); // 添加到临时文件跟踪列表
                using (var fileStream = File.Create(tempPath)) // 创建临时文件
                {
                    streamInfo.Stream.CopyTo(fileStream); // 复制资源流到临时文件
                }
                return tempPath;
            }
        }

        /// <summary>
        /// 获取分享 PNG 的输出路径（自动创建文件夹，文件名带时间戳）
        /// </summary>
        /// <returns>输出路径</returns>
        private string GetShareAppearanceOutputPath()
        {
            AppPathHelper.EnsureDirectoryExists(AppPathHelper.SharedAppearanceFolder); // 确保目录存在
            string fileName = $"Quicker外观_{DateTime.Now:yyyyMMdd_HHmmss}.png"; // 文件名格式：Quicker外观_年月日_时分秒.png
            return Path.Combine(AppPathHelper.SharedAppearanceFolder, fileName);
        }

        /// <summary>
        /// 将 JSON 数据写入 PNG 图片的 tEXt 块，实现外观参数嵌入（ImageSharp 版本）
        /// </summary>
        /// <param name="inputPngPath">输入PNG路径（载体图片）</param>
        /// <param name="outputPngPath">输出PNG路径（保存路径）</param>
        /// <param name="json">要嵌入的 JSON 数据</param>
        private void WriteAppearanceToPng(string inputPngPath, string outputPngPath, string json)
        {
            // 用 ImageSharp 读取图片，写入 tEXt 块
            using (var image = SixLabors.ImageSharp.Image.Load(inputPngPath))
            {
                var pngMeta = image.Metadata.GetPngMetadata();
                string base64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json));
                // 先移除同名块，避免重复
                var toRemove = pngMeta.TextData.Where(t => t.Keyword == "QuickerAppearance").ToList();
                foreach (var item in toRemove)
                {
                    pngMeta.TextData.Remove(item);
                }
                pngMeta.TextData.Add(new PngTextData("QuickerAppearance", base64, null, null));
                image.Save(outputPngPath);
            }
        }

        /// <summary>
        /// 显示 Toast 提示
        /// </summary>
        /// <param name="message">提示内容</param>
        /// <param name="type">提示类型 (Success, Error, Info)</param>
        private void ShowToast(string message, ToastType type)
        {
            using var toast = new ToastManager();
            toast.Show(message, type);
        }

        /// <summary>
        /// 确保输入的 PNG 图片为 32 位真彩色格式（BGRA32），如果不是则自动转换并返回新文件路径。
        /// </summary>
        /// <param name="inputPath">原始 PNG 图片路径（可以是索引色或其他格式）</param>
        /// <returns>转换为 32 位真彩色的 PNG 文件路径（临时文件）</returns>
        private string EnsureTrueColorPng(string inputPath)
        {
            BitmapImage bitmap = null;
            FormatConvertedBitmap converted = null;
            try
            {
                // 用 WPF BitmapImage 读取
                bitmap = new();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(inputPath, UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();

                // 转换为 32 位 BGRA
                converted = new(bitmap, PixelFormats.Bgra32, null, 0);

                // 保存为临时 PNG
                string tempPath = Path.GetTempFileName() + ".png";
                _tempFiles.Add(tempPath); // 添加到临时文件跟踪列表
                using (var fileStream = File.Create(tempPath))
                {
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(converted));
                    encoder.Save(fileStream);
                }
                return tempPath;
            }
            finally
            {
                // 确保资源被释放
                converted?.Freeze();
                bitmap?.Freeze();
            }
        }

        /// <summary>
        /// 从 PNG 文件导入外观数据，返回 Appearance 对象，失败返回 null
        /// </summary>
        /// <param name="pngPath">PNG 文件路径</param>
        /// <returns>Appearance 对象或 null</returns>
        private Appearance ImportAppearanceFromPng(string pngPath)
        {
            string json = null;
            using (var image = Image.Load(pngPath))
            {
                var pngMeta = image.Metadata.GetPngMetadata();
                var textData = pngMeta.TextData.FirstOrDefault(t => t.Keyword == "QuickerAppearance");
                // 使用更安全的方式检查是否找到了数据
                if (textData != null && !string.IsNullOrEmpty(textData.Value))
                {
                    json = Encoding.UTF8.GetString(Convert.FromBase64String(textData.Value));
                }
            }
            if (string.IsNullOrEmpty(json))
                return null;
            return JsonSerializer.Deserialize<Appearance>(json);
        }

        // 导入外观设置按钮点击事件
        private void ImportAppearanceButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string selectedPngPath = ShowSelectAppearancePngDialog(); // 选择图片
                if (string.IsNullOrEmpty(selectedPngPath))
                    return;

                var appearance = ImportAppearanceFromPng(selectedPngPath); // 导入外观数据
                if (appearance == null)
                {
                    ShowToast("未检测到外观数据！", ToastType.Error);
                    return;
                }

                HandleImportedAppearanceBackgroundImage(appearance, selectedPngPath); // 处理背景图片
                ApplyImportedAppearance(appearance); // 应用外观参数并刷新界面
                ShowToast("导入成功！", ToastType.Success);
            }
            catch
            {
                ShowToast("导入失败！", ToastType.Error);
            }
        }

        /// <summary>
        /// 弹出文件选择对话框，返回用户选择的PNG文件路径，取消则返回null
        /// </summary>
        /// <returns>PNG文件路径</returns>
        private string ShowSelectAppearancePngDialog()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "PNG图片|*.png",
                Title = "选择外观分享图片"
            };
            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }

        /// <summary>
        /// 处理导入Appearance对象的背景图片，将其去除文本块后复制到本地并更新路径
        /// </summary>
        /// <param name="appearance">要处理的Appearance对象</param>
        /// <param name="srcPngPath">源PNG文件路径（载体图片）</param>
        private void HandleImportedAppearanceBackgroundImage(Appearance appearance, string srcPngPath)
        {
            if (!string.IsNullOrEmpty(appearance.BackgroundImagePath))
            {
                string bgDir = AppPathHelper.BackgroundImagesFolder; // 背景图片文件夹路径
                AppPathHelper.EnsureDirectoryExists(bgDir); // 确保目录存在
                string hash = DateTime.Now.Ticks.ToString("x"); // 随机哈希值作为文件名
                string newBgPath = Path.Combine(bgDir, $"bg_{hash}.png"); // 新背景图片路径
                RemoveTextChunkAndCopy(srcPngPath, newBgPath); // 去除文本块并复制图片
                appearance.BackgroundImagePath = newBgPath; // 更新路径
            }
        }

        /// <summary>
        /// 应用导入的Appearance对象到设置，并刷新界面和预览
        /// </summary>
        /// <param name="appearance">要应用的Appearance对象</param>
        private void ApplyImportedAppearance(Appearance appearance)
        {
            SettingDatabase.UpdateAppearance(appearance); // 更新数据库
            settingManager.appearanceConditions = appearance; // 应用到缓存
            RefreshInterfaceDisplay(); // 刷新界面显示
            // 保证导入后预览区可见
            EnablePreviewCheckBox.IsChecked = true;
            EnablePreviewCheckBox_Click(null, null);
        }

        /// <summary>
        /// 去除 PNG 图片的 tEXt/iTXt/zTXt 元数据并复制到指定路径
        /// 用于处理导入Appearance对象的背景图片，将其去除文本块后复制到本地
        /// </summary>
        /// <param name="srcPath"> 源 PNG 文件路径 </param>
        /// <param name="destPath"> 目标 PNG 文件路径 </param>
        private void RemoveTextChunkAndCopy(string srcPath, string destPath)
        {
            using (var image = Image.Load(srcPath)) // 用 ImageSharp 读取图片像素并去除所有 tEXt/iTXt/zTXt 元数据
            {
                var pngMeta = image.Metadata.GetPngMetadata();
                pngMeta.TextData.Clear(); // 移除所有文本块
                image.Save(destPath);
            }
        }
        #endregion

        #region 预置样式（应用与定义）
        // 预置样式按钮点击事件
        private void PresetStyleItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string styleKey)
            {
                ApplyPresetStyle(styleKey);
                PresetStylePopup.IsOpen = false; // 关闭弹窗
            }
        }

        /// <summary>
        /// 应用预置样式
        /// </summary>
        /// <param name="styleKey"> 预置样式键 </param>
        private void ApplyPresetStyle(string styleKey)
        {
            // 获取预置样式对象
            Appearance preset = GetPresetStyle(styleKey);
            if (preset == null) return;

            var currentOptions = SaveCurrentUserOptions(); // 保存当前用户选项设置
            ApplyPresetToCurrentSettings(preset); // 应用预置样式到当前设置
            RestoreUserOptions(currentOptions); // 恢复用户选项设置
            RefreshInterfaceDisplay(); // 刷新界面显示
            SettingDatabase.UpdateAppearance(settingManager.appearanceConditions); // 保存到数据库
        }

        /// <summary>
        /// 根据样式键获取对应的预置样式对象
        /// </summary>
        /// <param name="styleKey">样式键</param>
        /// <returns>预置样式对象，如果未找到则返回null</returns>
        private Appearance GetPresetStyle(string styleKey)
        {
            return styleKey switch
            {
                "Original" => PresetOriginal,
                "Simple" => PresetSimple,
                "Translucent" => PresetTranslucent,
                "Dark" => PresetDark,
                "DarkTranslucent" => PresetDarkTranslucent,
                _ => null
            };
        }

        /// <summary>
        /// 保存当前用户的选项设置，这些设置不会被预置样式覆盖
        /// </summary>
        /// <returns>包含用户选项设置的对象</returns>
        private UserOptions SaveCurrentUserOptions()
        {
            return new UserOptions
            {
                AutoHideTitleBar = settingManager.appearanceConditions.AutoHideTitleBar,
                ShowActionButtonMouseOver = settingManager.appearanceConditions.ShowActionButtonMouseOver,
                HideActionNameAfterIcon = settingManager.appearanceConditions.HideActionNameAfterIcon,
                ShowActionIconShadow = settingManager.appearanceConditions.ShowActionIconShadow
            };
        }

        /// <summary>
        /// 将预置样式应用到当前设置对象
        /// </summary>
        /// <param name="preset">预置样式对象</param>
        private void ApplyPresetToCurrentSettings(Appearance preset)
        {
            var current = settingManager.appearanceConditions;
            foreach (var prop in typeof(Appearance).GetProperties()) // 复制预置样式的所有属性到当前设置
            {
                if (prop.CanWrite)
                {
                    prop.SetValue(current, prop.GetValue(preset));
                }
            }
        }

        // 打开保存背景图片的文件夹
        private void ExportAppearanceButton_Click(object sender, RoutedEventArgs e)
        {
            AppPathHelper.EnsureDirectoryExists(AppPathHelper.BackgroundImagesFolder); // 确保目录存在
            Process.Start("explorer.exe", AppPathHelper.BackgroundImagesFolder); // 打开背景图片文件夹
        }

        /// <summary>
        /// 恢复用户之前保存的选项设置
        /// </summary>
        /// <param name="options">用户选项设置对象</param>
        private void RestoreUserOptions(UserOptions options)
        {
            settingManager.appearanceConditions.AutoHideTitleBar = options.AutoHideTitleBar;
            settingManager.appearanceConditions.ShowActionButtonMouseOver = options.ShowActionButtonMouseOver;
            settingManager.appearanceConditions.HideActionNameAfterIcon = options.HideActionNameAfterIcon;
            settingManager.appearanceConditions.ShowActionIconShadow = options.ShowActionIconShadow;

            // 确保预览功能开启
            settingManager.appearanceConditions.EnablePreview = true;
        }

        /// <summary>
        /// 刷新界面显示，应用新的设置到UI控件
        /// </summary>
        private void RefreshInterfaceDisplay()
        {
            ApplyButtonSettings(); // 应用按钮设置
            ApplyColorSettings(); // 应用颜色设置
            ApplyFontSettings(); // 应用字体设置
            ApplyBackgroundImageSettings(); // 应用背景图片设置
            ApplyBlurAndCornerSettings(); // 应用模糊与圆角设置
            ApplyOptionSettings(); // 应用选项设置

            // 只在预览功能开启时才加载预览按钮（仅在非初始化期间）
            if (settingManager.appearanceConditions.EnablePreview && !_isInitializing)
            {
                LoadGlobalButtonsForPreview(); // 加载全局按钮列表并显示预览区
            }
        }

        /// <summary>
        /// 用户选项设置的数据结构，用于保存和恢复用户特定的选项
        /// </summary>
        private class UserOptions
        {
            public bool AutoHideTitleBar { get; set; }
            public bool ShowActionButtonMouseOver { get; set; }
            public bool HideActionNameAfterIcon { get; set; }
            public bool ShowActionIconShadow { get; set; }
        }

        // 预置样式静态数据
        // 统一的预置构造：提供公共默认值，个别预置只覆盖差异，减少重复
        private static Appearance CreatePreset()
        {
            return new Appearance
            {
                // 尺寸（大多数预置共享）
                ButtonSize = 72,
                ButtonGap = 0,
                BorderWidth = 0,
                ButtonCornerRadius = 0,

                // 颜色（基础为浅色半透）
                BackgroundColor = "#60FFFFFF",
                BorderColor = "#00FFFFFF",
                ToolbarColor = "#1F999999",
                ToolbarIconColor = "#FF666666",
                ActionButtonColor = "#9DFFFFFF",
                ActionButtonMouseOverColor = "#59B2F2FF",
                BlankButtonColor = "#32C8C8C8",
                BlankButtonMouseOverColor = "#05000000",
                ButtonTextColor = "#FF000000",

                // 字体
                Font1 = -1,
                Font2 = -1,
                FontSize = 12,
                FontWeight = 4,

                // 背景图片
                BackgroundImagePath = "",
                BackgroundImageOpacity = 1.0,

                // 模糊与圆角
                Blur = 1,
                Win11CornerRadius = 0,

                // 选项
                AutoHideTitleBar = true,
                ShowActionButtonMouseOver = true,
                HideActionNameAfterIcon = false,
                ShowActionIconShadow = false,
                EnablePreview = true
            };
        }
        private static Appearance CreatePreset(System.Action<Appearance> configure)
        {
            var preset = CreatePreset();
            configure?.Invoke(preset);
            return preset;
        }

        // 原始风格
        private static readonly Appearance PresetOriginal = CreatePreset(p =>
        {
            p.ButtonSize = 79;
            p.ButtonGap = 1;

            p.BackgroundColor = "#99B0B0B0";
            p.ToolbarColor = "#18999999";
            p.ActionButtonColor = "#FFFFFFFF";
            p.ActionButtonMouseOverColor = "#FFB2F2FF";
        });

        // 小白风格
        private static readonly Appearance PresetSimple = CreatePreset(p =>
        {
            p.ButtonGap = 0.2;

            p.BackgroundColor = "#C5CCCCCC";
            p.ToolbarColor = "#2B999999";
            p.ActionButtonColor = "#FFFFFFFF";
            p.ActionButtonMouseOverColor = "#59B2F2FF";
        });

        // 半透风格
        private static readonly Appearance PresetTranslucent = CreatePreset();

        // 深色风格
        private static readonly Appearance PresetDark = CreatePreset(p =>
        {
            p.ButtonGap = 0.5;

            p.BackgroundColor = "#C5737373";
            p.ToolbarColor = "#00999999";
            p.ToolbarIconColor = "#FF000000";
            p.ActionButtonColor = "#31000000";
            p.ActionButtonMouseOverColor = "#59D7D7D7";
            p.BlankButtonColor = "#38000000";
            p.ButtonTextColor = "#FFFFFFFF";
        });

        // 深色半透风格
        private static readonly Appearance PresetDarkTranslucent = CreatePreset(p =>
        {
            p.BackgroundColor = "#355E5E5E";
            p.ToolbarColor = "#29999999";
            p.ToolbarIconColor = "#AA000000";
            p.ActionButtonColor = "#31000000";
            p.ActionButtonMouseOverColor = "#59D7D7D7";
            p.BlankButtonColor = "#38000000";
            p.ButtonTextColor = "#FFFFFFFF";
            p.Blur = 0;
        });
        #endregion

        private void DefaultAppearanceSettingButton_Click(object sender, RoutedEventArgs e)
        {
            DarkThemeSettingButton.BorderThickness = new Thickness(0);
            DefaultAppearanceSettingButton.BorderThickness = new Thickness(0, 0, 0, 2);
        }

        private void DarkThemeSettingButton_Click(object sender, RoutedEventArgs e)
        {
            DefaultAppearanceSettingButton.BorderThickness = new Thickness(0);
            DarkThemeSettingButton.BorderThickness = new Thickness(0, 0, 0, 2);
        }
    }
}