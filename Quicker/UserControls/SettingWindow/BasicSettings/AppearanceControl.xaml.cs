using Color = System.Windows.Media.Color;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using Point = System.Windows.Point;
using Quicker.Windows.ToolWindows;
using System.Windows.Threading;
using Quicker.Contracts.Models;
using Quicker.Models.Settings;
using System.Windows.Controls;
using Quicker.Database.Core;
using System.ComponentModel;
using System.Windows.Input;
using System.Windows.Media;
using System.Diagnostics;
using Quicker.Managers;
using System.Text.Json;
using Quicker.Helpers;
using System.Windows;
using WpfAnimatedGif;
using Quicker.Models;
using System.Text;
using ImageMagick;
using System.IO;

namespace Quicker.UserControls.SettingWindow.BasicSettings
{
    public partial class AppearanceControl : UserControl, INotifyPropertyChanged
    {
        #region 成员字段

        private WeakReference<Quicker.Windows.MainWindows.SettingWindow> weakSettingWindow;
        private readonly ButtonManager buttonManager = new();
        private DispatcherTimer _colorChangeDebounceTimer;
        private readonly List<string> _tempFiles = new();
        private bool _hasPendingGlobalButtonLoad = false; // 脏标记：用于异步加载防抖
        private DispatcherTimer _settingsChangeTimer;
        private bool _isLoadingGlobalButtons = false;
        private readonly ButtonDatabase db2 = new();
        private SolidColorBrush _currentBrush;
        private SettingManager settingManager;
        private bool _isInitializing = true;
        private int _suppressWrites; // 批量刷新界面（重置/预置/导入/补应用字体）时临时抑制写库
        private bool WritesSuppressed => _isInitializing || _suppressWrites > 0;

        // 分享时用它代替本地路径：表示"载体 PNG 本身就是背景图"，避免泄露本机路径又不丢背景
        private const string EmbeddedBackgroundMarker = "<embedded>";
        private Button _currentColorButton;
        private bool _pendingColorFlush;

        // 统一的防抖提交定时器（用于滑块、文本框等非颜色控件）
        private DispatcherTimer _settingsDebounceTimer;
        private bool _pendingSettingsFlush;

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        #endregion

        #region 公开属性

        private CornerRadius _previewCornerRadius = new CornerRadius(5);
        public CornerRadius PreviewCornerRadius
        {
            get => _previewCornerRadius;
            set
            {
                if (_previewCornerRadius != value)
                {
                    _previewCornerRadius = value;
                    OnPropertyChanged(nameof(PreviewCornerRadius));
                }
            }
        }

        private CornerRadius _titleBarCornerRadius = new CornerRadius(5, 5, 0, 0);
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
        public AppearanceControl(Quicker.Windows.MainWindows.SettingWindow settingWindow)
        {
            InitializeComponent();
            settingManager = settingWindow._settingManager;
            weakSettingWindow = new(settingWindow);
            this.DataContext = this;
            InitializeSettingsDebounceTimer(); // 提前创建，避免初始化窗口期内空引用

            Dispatcher.BeginInvoke(new Action(async () =>
            {
                try
                {
                    await InitializeAsync();
                    InitializeSettingsChangeTimer();
                    await InitializeFontComboBoxes();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"AppearanceControl Initialization Error: {ex.Message}");
                }
                finally
                {
                    _isInitializing = false; // 确保无论成功失败，初始化标志都能正确释放
                }
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        #endregion

        #region 设置变化计时器与防抖
        private void InitializeSettingsChangeTimer()
        {
            _settingsChangeTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _settingsChangeTimer.Tick += SettingsChangeTimer_Tick;
            _settingsChangeTimer.Start();
        }

        private void SettingsChangeTimer_Tick(object sender, EventArgs e)
        {
            if (settingManager != null)
            {
                bool hasChanges = settingManager.IsMainAppearanceSettingsChanged();
                ResetAppearanceButton.Visibility = hasChanges ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void InitializeSettingsDebounceTimer()
        {
            _settingsDebounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(150)
            };
            _settingsDebounceTimer.Tick += SettingsDebounceTimer_Tick;
        }

        private void ScheduleSettingsFlush()
        {
            if (WritesSuppressed || _settingsDebounceTimer == null) return;
            _pendingSettingsFlush = true;
            _settingsDebounceTimer.Stop();
            _settingsDebounceTimer.Start();
        }

        private void SettingsDebounceTimer_Tick(object sender, EventArgs e)
        {
            _settingsDebounceTimer.Stop();
            FlushSettingsChange();
        }

        private void FlushSettingsChange(bool reloadPreview = true)
        {
            if (!_pendingSettingsFlush || settingManager == null) return;
            _pendingSettingsFlush = false;

            SettingDatabase.UpdateAppearance(settingManager.appearanceConditions);
            AppStateManager.NotifyAppearanceChanged(settingManager.appearanceConditions);

            // 图标大小、标题宽度、字号等由 ButtonManager 在代码里按设置创建，需要在落库后重刷预览
            if (reloadPreview) LoadGlobalButtonsForPreview();
        }
        #endregion

        #region 初始化与加载设置
        private async Task InitializeAsync()
        {
            await LoadSettingsAsync();
        }

        private async Task LoadSettingsAsync()
        {
            await settingManager.LoadAppearanceAsync();

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                ApplyOptionSettings();
                ResetAppearanceButton.Visibility = Visibility.Collapsed;
            });

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                ApplyButtonSettings();
                ApplyColorSettings();
                ApplyFontSettings();
            });

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                ApplyBackgroundImageSettings();
                ApplyBlurAndCornerSettings();
            });

            _isInitializing = false;

            if (settingManager.appearanceConditions.EnablePreview)
            {
                await LoadGlobalButtonsForPreviewInternal();
            }
        }
        #endregion

        #region 应用设置到UI
        private void ApplyButtonSettings()
        {
            ButtonSizeSlider.Value = settingManager.appearanceConditions.ButtonSize;
            ButtonGapSlider.Value = settingManager.appearanceConditions.ButtonGap;
            BorderWidthSlider.Value = settingManager.appearanceConditions.BorderWidth;
            ButtonCornerRadiusSlider.Value = settingManager.appearanceConditions.ButtonCornerRadius;
        }

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

        private void ApplyFontSettings()
        {
            FontSizeSlider.Value = settingManager.appearanceConditions.FontSize;
            FontWeightComboBox.SelectedIndex = settingManager.appearanceConditions.FontWeight;
            ApplyGlobalFontFamily();
        }

        private void ApplyBackgroundImageSettings()
        {
            _backgroundImagePath = settingManager.appearanceConditions.BackgroundImagePath;
            OnPropertyChanged(nameof(BackgroundImagePath));
            SetPreviewBackgroundImage(_backgroundImagePath);
            BackgroundImageOpacitySlider.Value = settingManager.appearanceConditions.BackgroundImageOpacity;
        }

        private void ApplyBlurAndCornerSettings()
        {
            BlurComboBox.SelectedIndex = settingManager.appearanceConditions.Blur;
            Win11CornerRadiusComboBox.SelectedIndex = settingManager.appearanceConditions.Win11CornerRadius;
            UpdatePreviewCornerRadiusByComboBox();
        }

        private void ApplyOptionSettings()
        {
            AutoHideTitleBarCheckBox.IsChecked = settingManager.appearanceConditions.AutoHideTitleBar;
            ShowActionButtonMouseOverCheckBox.IsChecked = settingManager.appearanceConditions.ShowActionButtonMouseOver;
            HideActionNameAfterIconCheckBox.IsChecked = settingManager.appearanceConditions.HideActionNameAfterIcon;
            ShowActionIconShadowCheckBox.IsChecked = settingManager.appearanceConditions.ShowActionIconShadow;

            EnablePreviewCheckBox.IsChecked = settingManager.appearanceConditions.EnablePreview;
            ViewPreviewBorder.Visibility = settingManager.appearanceConditions.EnablePreview ? Visibility.Visible : Visibility.Collapsed;
        }
        #endregion

        #region 滚动同步
        private void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (ScrollViewer == null) return;
            AppearanceScrollBar.Maximum = ScrollViewer.ScrollableHeight;
            AppearanceScrollBar.ViewportSize = ScrollViewer.ViewportHeight;
            AppearanceScrollBar.Value = ScrollViewer.VerticalOffset;
        }
        private void AppearanceScrollBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            ScrollViewer?.ScrollToVerticalOffset(AppearanceScrollBar.Value);
        }
        #endregion

        #region 复选框点击事件
        private void CheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (WritesSuppressed || settingManager == null) return;
            var checkBox = sender as CheckBox;
            if (checkBox == null) return;
            bool value = checkBox.IsChecked == true;

            switch (checkBox.Name)
            {
                case "AutoHideTitleBarCheckBox":
                    settingManager.appearanceConditions.AutoHideTitleBar = value;
                    break;
                case "ShowActionButtonMouseOverCheckBox":
                    settingManager.appearanceConditions.ShowActionButtonMouseOver = value;
                    break;
                case "HideActionNameAfterIconCheckBox":
                    settingManager.appearanceConditions.HideActionNameAfterIcon = value;
                    break;
                case "ShowActionIconShadowCheckBox":
                    settingManager.appearanceConditions.ShowActionIconShadow = value;
                    break;
                case "EnablePreviewCheckBox":
                    settingManager.appearanceConditions.EnablePreview = value;
                    ViewPreviewBorder.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
                    if (value) LoadGlobalButtonsForPreview();
                    break;
                default:
                    return;
            }
            SettingDatabase.UpdateAppearance(settingManager.appearanceConditions);
            AppStateManager.NotifyAppearanceChanged(settingManager.appearanceConditions);

            if (checkBox.Name != "EnablePreviewCheckBox")
                LoadGlobalButtonsForPreview();
        }
        #endregion

        #region 颜色选择
        private void ColorButton_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            if (button == null) return;
            _currentColorButton = button;
            _currentBrush = button.Background as SolidColorBrush;
            if (_currentBrush == null) return;

            PopupColorPicker.ResetColorControls(_currentBrush.Color);
            PopupColorPicker.SelectedColor = _currentBrush.Color;

            ColorPickerPopup.PlacementTarget = button;
            ColorPickerPopup.IsOpen = true;
        }

        private void PopupColorPicker_SelectedColorChanged(object sender, ColorChangedEventArgs e)
        {
            if (WritesSuppressed || _currentBrush == null || settingManager == null) return;

            _currentBrush.Color = e.NewColor;
            UpdateAppearanceColorProperty(_currentBrush, e.NewColor);

            if (_currentColorButton != null && _currentColorButton.Name == "TextColorButton")
            {
                ButtonTextColorBrush.Color = e.NewColor;
                settingManager.appearanceConditions.ButtonTextColor = e.NewColor.ToString();
            }

            ScheduleColorChangeFlush();
        }

        private void ScheduleColorChangeFlush()
        {
            if (_colorChangeDebounceTimer == null)
            {
                _colorChangeDebounceTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(150)
                };
                _colorChangeDebounceTimer.Tick += ColorChangeDebounceTimer_Tick;
            }
            _pendingColorFlush = true;
            _colorChangeDebounceTimer.Stop();
            _colorChangeDebounceTimer.Start();
        }

        private void ColorChangeDebounceTimer_Tick(object sender, EventArgs e)
        {
            _colorChangeDebounceTimer.Stop();
            FlushColorChange();
        }

        private void FlushColorChange()
        {
            if (!_pendingColorFlush || settingManager == null) return;
            _pendingColorFlush = false;

            SettingDatabase.UpdateAppearance(settingManager.appearanceConditions);
            AppStateManager.NotifyAppearanceChanged(settingManager.appearanceConditions);
        }

        // 使用委托字典优化反射性能
        private static readonly Dictionary<string, Action<Appearance, string>> ColorPropertySetters = new()
        {
            { "BackgroundColorButton", (a, v) => a.BackgroundColor = v },
            { "BorderColorButton", (a, v) => a.BorderColor = v },
            { "ToolbarColorButton", (a, v) => a.ToolbarColor = v },
            { "ToolbarIconColorButton", (a, v) => a.ToolbarIconColor = v },
            { "ActionButtonColorButton", (a, v) => a.ActionButtonColor = v },
            { "ActionButtonMouseOverColorButton", (a, v) => a.ActionButtonMouseOverColor = v },
            { "BlankButtonColorButton", (a, v) => a.BlankButtonColor = v },
            { "BlankButtonMouseOverColorButton", (a, v) => a.BlankButtonMouseOverColor = v },
            { "TextColorButton", (a, v) => a.ButtonTextColor = v },
            //{ "ActionIconColorButton", (a, v) => a.ActionIconColor = v },
            //{ "TriggerKeyTextColorButton", (a, v) => a.TriggerKeyTextColor = v },
            //{ "OtherIconColorButton", (a, v) => a.OtherIconColor = v }
        };

        private void UpdateAppearanceColorProperty(SolidColorBrush brush, Color color)
        {
            if (_currentColorButton == null) return;
            if (ColorPropertySetters.TryGetValue(_currentColorButton.Name, out var setter))
            {
                setter(settingManager.appearanceConditions, color.ToString());
            }
        }
        #endregion

        #region 资源释放
        private void AppearanceGrid_Unloaded(object sender, RoutedEventArgs e)
        {
            // 离开页面前，把防抖期内尚未提交的修改落库（不再触发预览重载）
            try
            {
                FlushColorChange();
                FlushSettingsChange(false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Unloaded flush error: {ex.Message}");
            }

            _settingsChangeTimer?.Stop();
            _settingsChangeTimer = null;

            _colorChangeDebounceTimer?.Stop();
            _colorChangeDebounceTimer = null;

            _settingsDebounceTimer?.Stop();
            _settingsDebounceTimer = null;

            _pendingColorFlush = false;
            _pendingSettingsFlush = false;

            GlobalGrid.Children.Clear();
            _currentBrush = null;
            _buttonTextColorBrush = null;
            settingManager = null;
            weakSettingWindow = null;
            _currentColorButton = null;
            PropertyChanged = null;

            foreach (string tempFile in _tempFiles)
            {
                try
                {
                    if (File.Exists(tempFile)) File.Delete(tempFile);
                }
                catch { }
            }
            _tempFiles.Clear();
        }
        #endregion

        #region 预置样式（菜单入口）
        private void PresetStyleButton_Click(object sender, RoutedEventArgs e)
        {
            PresetStylePopup.IsOpen = true;
        }
        #endregion

        #region 预览加载与交互

        private void LoadGlobalButtonsForPreview()
        {
            if (_isInitializing || settingManager == null || !settingManager.appearanceConditions.EnablePreview) return;
            LoadGlobalButtonsForPreviewInternal();
        }

        private async Task LoadGlobalButtonsForPreviewInternal()
        {
            if (_isLoadingGlobalButtons)
            {
                _hasPendingGlobalButtonLoad = true;
                return;
            }

            _isLoadingGlobalButtons = true;
            try
            {
                do
                {
                    _hasPendingGlobalButtonLoad = false;
                    var globalButtons = await Task.Run(() => db2.GetPagesOfButtons("_global", 0));

                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        if (settingManager == null) return; // 页面已卸载

                        // ButtonManager 内部缓存了外观设置（来自数据库），每次重刷前先失效，保证读到最新值
                        buttonManager.InvalidateAppearance();

                        var buttons = GlobalGrid.Children.OfType<Button>().ToList();
                        bool hasChanges = ProcessButtonsWithData(buttons, globalButtons);
                        if (!hasChanges)
                        {
                            ApplyDefaultDataToGlobal11(buttons);
                        }
                    });
                }
                while (_hasPendingGlobalButtonLoad);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadGlobalButtonsForPreview Error: {ex.Message}");
            }
            finally
            {
                _isLoadingGlobalButtons = false;
            }
        }

        private bool ProcessButtonsWithData(List<Button> buttons, List<ButtonData> globalButtons)
        {
            bool hasChanges = false;
            for (int i = 0; i < buttons.Count; i++)
            {
                var btn = buttons[i];
                if (int.TryParse(btn.Name.Replace("_global", ""), out int buttonIndex))
                {
                    var buttonData = globalButtons.FirstOrDefault(b => b.ButtonID == buttonIndex);
                    if (buttonData != null)
                    {
                        hasChanges = true;
                        ApplyButtonData(btn, buttonData);
                    }
                    else
                    {
                        btn.Background = BlankButtonColorButton.Background;
                    }
                }
            }
            return hasChanges;
        }

        private void ApplyButtonData(Button button, ButtonData buttonData)
        {
            button.Background = ActionButtonColorButton.Background;
            buttonManager.RefreshButtonDisplay(button, buttonData, ButtonSizeSlider.Value);
        }

        private void ApplyDefaultDataToGlobal11(List<Button> buttons)
        {
            var defaultData = CreateDefaultButtonData();
            var global11Button = buttons.FirstOrDefault(b => b.Name == "_global11");
            if (global11Button != null)
            {
                ApplyButtonData(global11Button, defaultData);
            }
        }

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
            var btn = sender as Button;
            if (btn == null) return;
            btn.Background = btn.Tag == null
                ? BlankButtonMouseOverColorButton.Background
                : ActionButtonMouseOverColorButton.Background;

            if (ShowActionButtonMouseOverCheckBox.IsChecked == true && btn.Tag != null)
            {
                btn.RenderTransformOrigin = new Point(0.5, 0.5);
                btn.RenderTransform = new ScaleTransform(1.05, 1.05);
            }
        }

        private void PreviewButton_MouseLeave(object sender, EventArgs e)
        {
            var btn = sender as Button;
            if (btn == null) return;
            btn.Background = btn.Tag == null
                ? BlankButtonColorButton.Background
                : ActionButtonColorButton.Background;

            if (ShowActionButtonMouseOverCheckBox.IsChecked == true)
            {
                btn.RenderTransform = null;
            }
        }
        #endregion

        #region 滑块改变事件
        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (WritesSuppressed || settingManager == null) return;
            var slider = sender as Slider;
            if (slider == null) return;

            switch (slider.Name)
            {
                case "ButtonSizeSlider":
                    settingManager.appearanceConditions.ButtonSize = slider.Value;
                    break;
                case "ButtonGapSlider":
                    settingManager.appearanceConditions.ButtonGap = slider.Value;
                    break;
                case "BorderWidthSlider":
                    settingManager.appearanceConditions.BorderWidth = slider.Value;
                    break;
                case "ButtonCornerRadiusSlider":
                    settingManager.appearanceConditions.ButtonCornerRadius = slider.Value;
                    break;
                case "FontSizeSlider":
                    settingManager.appearanceConditions.FontSize = slider.Value;
                    break;
                case "BackgroundImageOpacitySlider":
                    settingManager.appearanceConditions.BackgroundImageOpacity = slider.Value;
                    break;
                default:
                    return;
            }
            ScheduleSettingsFlush();
        }
        #endregion

        #region 字体与圆角
        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (WritesSuppressed || settingManager == null) return;
            if (sender is ComboBox comboBox)
            {
                switch (comboBox.Name)
                {
                    case "BlurComboBox":
                        settingManager.appearanceConditions.Blur = comboBox.SelectedIndex;
                        break;
                    case "Win11CornerRadiusComboBox":
                        settingManager.appearanceConditions.Win11CornerRadius = comboBox.SelectedIndex;
                        UpdatePreviewCornerRadiusByComboBox();
                        break;
                    case "FontSizeComboBox1":
                    case "FontSizeComboBox2":
                        // 记录字体名称而非下标，兼容数据迁移
                        if (comboBox.SelectedItem is string fontName)
                        {
                            if (comboBox.Name == "FontSizeComboBox1")
                            {
                                settingManager.appearanceConditions.Font1Name = fontName;
                                settingManager.appearanceConditions.Font1 = comboBox.SelectedIndex; // 过渡期同时保留下标
                            }
                            else
                            {
                                settingManager.appearanceConditions.Font2Name = fontName;
                                settingManager.appearanceConditions.Font2 = comboBox.SelectedIndex;
                            }
                        }
                        ApplyGlobalFontFamily();
                        break;
                    case "FontWeightComboBox":
                        settingManager.appearanceConditions.FontWeight = comboBox.SelectedIndex;
                        break;
                    default:
                        return;
                }
                SettingDatabase.UpdateAppearance(settingManager.appearanceConditions);
                AppStateManager.NotifyAppearanceChanged(settingManager.appearanceConditions);
            }
        }

        private void UpdatePreviewCornerRadiusByComboBox()
        {
            double previewRadius;
            switch (Win11CornerRadiusComboBox.SelectedIndex)
            {
                case 1:
                    previewRadius = 0;
                    break;
                case 3:
                    previewRadius = 3;
                    break;
                default:
                    previewRadius = 5;
                    break;
            }
            PreviewCornerRadius = new CornerRadius(previewRadius);
            TitleBarCornerRadius = new CornerRadius(previewRadius, previewRadius, 0, 0);

            Dispatcher.BeginInvoke(new Action(() =>
            {
                ClipHelper.UpdateBorderClip(ViewPreviewBorder);
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }
        #endregion

        #region 背景图片选择与预览
        private void BackgroundImagePathButton_Click(object sender, RoutedEventArgs e)
        {
            BackgroundImagePathPopup.IsOpen = true;
        }

        private void SelectBackgroundImageButton_Click(object sender, RoutedEventArgs e)
        {
            BackgroundImagePathPopup.IsOpen = false;
            var dialog = new Microsoft.Win32.OpenFileDialog()
            {
                Filter = "图片文件|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.svg;*.ico",
                Title = "选择背景图片"
            };
            if (dialog.ShowDialog() == true)
            {
                double aspectRatio;
                if (settingManager.appearanceConditions.EnablePreview)
                {
                    aspectRatio = ViewPreviewBorder.ActualWidth / ViewPreviewBorder.ActualHeight;
                }
                else
                {
                    double btnSize = ButtonSizeSlider.Value;
                    double gap = ButtonGapSlider.Value;
                    double width = btnSize * 4 + gap * 3;
                    double height = 31 + 25.5 + btnSize * 7 + gap * 5;
                    aspectRatio = width / height;
                }
                var imageCropWindow = new ImageCropWindow(dialog.FileName, aspectRatio, ViewPreviewBorder.CornerRadius);
                RegisterImageCropWindowEvents(imageCropWindow);
                BackgroundImagePathButton.IsEnabled = false;
                imageCropWindow.Show();
            }
        }

        private void RegisterImageCropWindowEvents(ImageCropWindow imageCropWindow)
        {
            Action<object, string> cropCompletedHandler = null;
            EventHandler closedHandler = null;

            cropCompletedHandler = (s, croppedPath) =>
            {
                OnImageCropCompleted(croppedPath);
                UnregisterImageCropWindowEvents(imageCropWindow, cropCompletedHandler, closedHandler);
            };

            closedHandler = (s, args) =>
            {
                BackgroundImagePathButton.IsEnabled = true;
                UnregisterImageCropWindowEvents(imageCropWindow, cropCompletedHandler, closedHandler);
            };

            imageCropWindow.CropCompleted += cropCompletedHandler;
            imageCropWindow.Closed += closedHandler;
        }

        private void OnImageCropCompleted(string croppedPath)
        {
            if (!string.IsNullOrEmpty(croppedPath))
            {
                BackgroundImagePathTextBox.Text = croppedPath;
            }
            BackgroundImagePathButton.IsEnabled = true;
        }

        private void UnregisterImageCropWindowEvents(ImageCropWindow imageCropWindow, Action<object, string> cropCompletedHandler, EventHandler closedHandler)
        {
            imageCropWindow.CropCompleted -= cropCompletedHandler;
            imageCropWindow.Closed -= closedHandler;
        }

        private void InsertClipboardTextButton_Click(object sender, RoutedEventArgs e)
        {
            BackgroundImagePathPopup.IsOpen = false;
            try
            {
                string imagePath = Clipboard.GetText().Trim().Replace("\"", "");
                BackgroundImagePathTextBox.Text = imagePath;
                SetPreviewEnabled(true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Clipboard Read Error: {ex.Message}");
            }
        }

        private string _backgroundImagePath;
        public string BackgroundImagePath
        {
            get => _backgroundImagePath;
            set
            {
                if (_backgroundImagePath != value)
                {
                    _backgroundImagePath = value;
                    OnPropertyChanged(nameof(BackgroundImagePath));
                    SetPreviewBackgroundImage(_backgroundImagePath);
                }
            }
        }

        private void SetPreviewBackgroundImage(string path)
        {
            if (!IsLoaded || PreviewBackgroundImage == null) return;
            var iconManager = new IconManager();
            iconManager.SetImageWithGifSupport(PreviewBackgroundImage, path);
        }

        private void ClearBackgroundImageButton_Click(object sender, RoutedEventArgs e)
        {
            BackgroundImagePathTextBox.Text = "";
        }

        private void BackgroundImagePathTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (WritesSuppressed || settingManager == null) return;
            SetPreviewBackgroundImage(BackgroundImagePathTextBox.Text);
            settingManager.appearanceConditions.BackgroundImagePath = BackgroundImagePathTextBox.Text;
            SettingDatabase.UpdateAppearance(settingManager.appearanceConditions);
            AppStateManager.NotifyAppearanceChanged(settingManager.appearanceConditions);
        }
        #endregion

        #region 外观分享与导入
        private void ShareSaveAppearanceButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var appearance = settingManager.appearanceConditions;
                // 深拷贝后替换本地路径：有自定义背景则写占位符（导入端据此把载体 PNG 当作背景），否则留空
                var exportAppearance = JsonSerializer.Deserialize<Appearance>(JsonSerializer.Serialize(appearance));
                bool hasCustomBackground = !string.IsNullOrEmpty(appearance.BackgroundImagePath) && File.Exists(appearance.BackgroundImagePath);
                if (exportAppearance != null)
                    exportAppearance.BackgroundImagePath = hasCustomBackground ? EmbeddedBackgroundMarker : string.Empty;

                string json = JsonSerializer.Serialize(exportAppearance);
                string inputPngPath = GetAppearanceCarrierImagePath();

                if (Path.GetExtension(inputPngPath).ToLower() != ".png")
                {
                    string tempPngPath = Path.Combine(Path.GetTempPath(), $"QuickerTemp_{Guid.NewGuid()}.png");
                    ConvertImageToPng(inputPngPath, tempPngPath);
                    _tempFiles.Add(tempPngPath);
                    inputPngPath = tempPngPath;
                }
                inputPngPath = EnsureTrueColorPng(inputPngPath);
                string outputPngPath = GetShareAppearanceOutputPath();
                WriteAppearanceToPng(inputPngPath, outputPngPath, json);
                ShowToast("外观保存成功！", ToastType.Success);
                Process.Start("explorer.exe", $"/select,\"{outputPngPath}\"");
            }
            catch (Exception ex)
            {
                ShowToast("外观保存失败", ToastType.Error);
                Debug.WriteLine($"ShareSaveAppearance Error: {ex.Message}");
            }
        }

        private void SharedSavedAppearanceButton_Click(object sender, RoutedEventArgs e)
        {
            AppPathHelper.EnsureDirectoryExists(AppPathHelper.SharedAppearanceFolder);
            Process.Start("explorer.exe", AppPathHelper.SharedAppearanceFolder);
        }

        private void ConvertImageToPng(string inputPath, string outputPath)
        {
            try
            {
                using (var image = new MagickImage(inputPath))
                {
                    image.Write(outputPath, MagickFormat.Png);
                }
            }
            catch
            {
                ShowToast("图片格式不受支持或文件损坏，已自动使用默认图片导出外观。", ToastType.Error);
                string defaultPath = GetAppearanceCarrierImagePath();
                if (!string.Equals(inputPath, defaultPath, StringComparison.OrdinalIgnoreCase))
                {
                    ConvertImageToPng(defaultPath, outputPath);
                }
            }
        }

        private string GetAppearanceCarrierImagePath()
        {
            if (!string.IsNullOrEmpty(BackgroundImagePathTextBox.Text) && File.Exists(BackgroundImagePathTextBox.Text))
            {
                return BackgroundImagePathTextBox.Text;
            }
            else
            {
                var resourceUri = new Uri("pack://application:,,,/Resources/Images/Quicker_Enabled.png");
                var streamInfo = Application.GetResourceStream(resourceUri);
                string tempPath = Path.Combine(Path.GetTempPath(), $"QuickerTemp_{Guid.NewGuid()}.png");
                _tempFiles.Add(tempPath);
                using (var fileStream = File.Create(tempPath))
                {
                    streamInfo.Stream.CopyTo(fileStream);
                }
                return tempPath;
            }
        }

        private string GetShareAppearanceOutputPath()
        {
            AppPathHelper.EnsureDirectoryExists(AppPathHelper.SharedAppearanceFolder);
            string fileName = $"Quicker外观_{DateTime.Now:yyyyMMdd_HHmmss}.png";
            return Path.Combine(AppPathHelper.SharedAppearanceFolder, fileName);
        }

        private void WriteAppearanceToPng(string inputPngPath, string outputPngPath, string json)
        {
            using (var image = new MagickImage(inputPngPath))
            {
                string base64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json));
                image.SetAttribute("QuickerAppearance", base64);
                image.Write(outputPngPath, MagickFormat.Png);
            }
        }

        private void ShowToast(string message, ToastType type)
        {
            var toast = new ToastManager();
            toast.Show(message, type);
        }

        private string EnsureTrueColorPng(string inputPath)
        {
            BitmapImage bitmap = null;
            FormatConvertedBitmap converted = null;
            try
            {
                bitmap = new();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(inputPath, UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();

                converted = new(bitmap, PixelFormats.Bgra32, null, 0);

                string tempPath = Path.Combine(Path.GetTempPath(), $"QuickerTemp_{Guid.NewGuid()}.png");
                _tempFiles.Add(tempPath);
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
                converted?.Freeze();
                bitmap?.Freeze();
            }
        }

        private Appearance ImportAppearanceFromPng(string pngPath)
        {
            string base64 = null;
            using (var image = new MagickImage(pngPath))
            {
                base64 = image.GetAttribute("QuickerAppearance");
            }

            if (string.IsNullOrEmpty(base64))
                return null;

            string json = Encoding.UTF8.GetString(Convert.FromBase64String(base64));
            var imported = JsonSerializer.Deserialize<Appearance>(json);
            if (imported == null) return null;

            NormalizeImportedAppearance(imported, json);
            return imported;
        }

        // 外观中参与导入补缺的字段（与 CreatePreset 中的字段一致）
        private static readonly string[] ImportableFields =
        {
            "ButtonSize", "ButtonGap", "BorderWidth", "ButtonCornerRadius",
            "BackgroundColor", "BorderColor", "ToolbarColor", "ToolbarIconColor",
            "ActionButtonColor", "ActionButtonMouseOverColor", "BlankButtonColor",
            "BlankButtonMouseOverColor", "ButtonTextColor",
            "Font1", "Font2", "FontSize", "FontWeight",
            "BackgroundImagePath", "BackgroundImageOpacity",
            "Blur", "Win11CornerRadius",
            "AutoHideTitleBar", "ShowActionButtonMouseOver", "HideActionNameAfterIcon", "ShowActionIconShadow",
            "EnablePreview"
        };

        /// <summary>
        /// 对导入的外观做补缺与校验：JSON 中缺失的字段（含数值类型）用默认值补齐；颜色格式、数值范围不合法时回退/夹取
        /// </summary>
        private static void NormalizeImportedAppearance(Appearance a, string json)
        {
            var d = CreatePreset();

            try
            {
                using var doc = JsonDocument.Parse(json);
                var present = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    foreach (var p in doc.RootElement.EnumerateObject())
                    {
                        if (p.Value.ValueKind != JsonValueKind.Null) present.Add(p.Name);
                    }
                }

                foreach (var name in ImportableFields)
                {
                    if (present.Contains(name)) continue;
                    var prop = typeof(Appearance).GetProperty(name);
                    if (prop != null && prop.CanRead && prop.CanWrite)
                    {
                        prop.SetValue(a, prop.GetValue(d));
                    }
                }
            }
            catch (JsonException)
            {
                // json 已经反序列化成功过，这里理论上不会出现；出现则只做下面的范围校验
            }

            a.BackgroundColor = NormalizeColor(a.BackgroundColor, d.BackgroundColor);
            a.BorderColor = NormalizeColor(a.BorderColor, d.BorderColor);
            a.ToolbarColor = NormalizeColor(a.ToolbarColor, d.ToolbarColor);
            a.ToolbarIconColor = NormalizeColor(a.ToolbarIconColor, d.ToolbarIconColor);
            a.ActionButtonColor = NormalizeColor(a.ActionButtonColor, d.ActionButtonColor);
            a.ActionButtonMouseOverColor = NormalizeColor(a.ActionButtonMouseOverColor, d.ActionButtonMouseOverColor);
            a.BlankButtonColor = NormalizeColor(a.BlankButtonColor, d.BlankButtonColor);
            a.BlankButtonMouseOverColor = NormalizeColor(a.BlankButtonMouseOverColor, d.BlankButtonMouseOverColor);
            a.ButtonTextColor = NormalizeColor(a.ButtonTextColor, d.ButtonTextColor);

            // 范围与界面滑块/下拉框保持一致
            a.ButtonSize = Math.Clamp(a.ButtonSize, 40, 100);
            a.ButtonGap = Math.Clamp(a.ButtonGap, 0, 10);
            a.BorderWidth = Math.Clamp(a.BorderWidth, 0, 15);
            a.ButtonCornerRadius = Math.Clamp(a.ButtonCornerRadius, 0, 40);
            a.FontSize = Math.Clamp(a.FontSize, 4, 24);
            a.BackgroundImageOpacity = Math.Clamp(a.BackgroundImageOpacity, 0, 1);
            a.Blur = Math.Clamp(a.Blur, 0, 3);
            a.Win11CornerRadius = Math.Clamp(a.Win11CornerRadius, 0, 3);
            a.FontWeight = Math.Clamp(a.FontWeight, 0, 15);
            if (a.Font1 < -1) a.Font1 = -1;
            if (a.Font2 < -1) a.Font2 = -1;
            if (a.BackgroundImagePath == null) a.BackgroundImagePath = string.Empty;
        }

        private static string NormalizeColor(string value, string fallback)
        {
            if (string.IsNullOrWhiteSpace(value)) return fallback;
            try
            {
                ColorConverter.ConvertFromString(value);
                return value;
            }
            catch
            {
                return fallback;
            }
        }

        private void ImportAppearanceButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string selectedPngPath = ShowSelectAppearancePngDialog();
                if (string.IsNullOrEmpty(selectedPngPath))
                    return;

                var appearance = ImportAppearanceFromPng(selectedPngPath);
                if (appearance == null)
                {
                    ShowToast("未检测到外观数据！", ToastType.Error);
                    return;
                }

                HandleImportedAppearanceBackgroundImage(appearance, selectedPngPath);
                ApplyImportedAppearance(appearance);
                ShowToast("导入成功！", ToastType.Success);
            }
            catch (Exception ex)
            {
                ShowToast("导入失败！", ToastType.Error);
                Debug.WriteLine($"ImportAppearance Error: {ex.Message}");
            }
        }

        private string ShowSelectAppearancePngDialog()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "PNG图片|*.png",
                Title = "选择外观分享图片"
            };
            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }

        private void HandleImportedAppearanceBackgroundImage(Appearance appearance, string srcPngPath)
        {
            if (!string.IsNullOrEmpty(appearance.BackgroundImagePath))
            {
                string bgDir = AppPathHelper.BackgroundImagesFolder;
                AppPathHelper.EnsureDirectoryExists(bgDir);
                string hash = DateTime.Now.Ticks.ToString("x");
                string newBgPath = Path.Combine(bgDir, $"bg_{hash}.png");
                RemoveTextChunkAndCopy(srcPngPath, newBgPath);
                appearance.BackgroundImagePath = newBgPath;
            }
        }

        private void ApplyImportedAppearance(Appearance appearance)
        {
            var previous = settingManager.appearanceConditions;
            try
            {
                appearance.EnablePreview = true; // 导入后保证预览可见，并随外观一起落库
                SettingDatabase.UpdateAppearance(appearance);
                settingManager.appearanceConditions = appearance;
                AppStateManager.NotifyAppearanceChanged(settingManager.appearanceConditions);
                RefreshInterfaceDisplay(); // 内部会加载预览
            }
            catch
            {
                // 应用失败则回滚到导入前的外观，避免半应用状态
                settingManager.appearanceConditions = previous;
                SettingDatabase.UpdateAppearance(previous);
                AppStateManager.NotifyAppearanceChanged(previous);
                RefreshInterfaceDisplay();
                throw;
            }
        }

        private void RemoveTextChunkAndCopy(string srcPath, string destPath)
        {
            using (var image = new MagickImage(srcPath))
            {
                image.Strip();
                foreach (var attrName in image.AttributeNames.ToList())
                {
                    image.RemoveAttribute(attrName);
                }
                image.Write(destPath, MagickFormat.Png);
            }
        }
        #endregion

        #region 预置样式（应用与定义）
        private void PresetStyleItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string styleKey)
            {
                ApplyPresetStyle(styleKey);
                PresetStylePopup.IsOpen = false;
            }
        }

        private void ApplyPresetStyle(string styleKey)
        {
            Appearance preset = GetPresetStyle(styleKey);
            if (preset == null) return;

            var currentOptions = SaveCurrentUserOptions();
            ApplyPresetToCurrentSettings(preset);
            RestoreUserOptions(currentOptions);
            RefreshInterfaceDisplay();
            SettingDatabase.UpdateAppearance(settingManager.appearanceConditions);
            AppStateManager.NotifyAppearanceChanged(settingManager.appearanceConditions);
        }

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

        private void ApplyPresetToCurrentSettings(Appearance preset)
        {
            var current = settingManager.appearanceConditions;
            foreach (var prop in typeof(Appearance).GetProperties())
            {
                if (prop.CanWrite && prop.Name != nameof(Appearance.EnablePreview) && prop.Name != nameof(Appearance.BackgroundImagePath))
                {
                    prop.SetValue(current, prop.GetValue(preset));
                }
            }
        }

        private void ExportAppearanceButton_Click(object sender, RoutedEventArgs e)
        {
            AppPathHelper.EnsureDirectoryExists(AppPathHelper.BackgroundImagesFolder);
            Process.Start("explorer.exe", AppPathHelper.BackgroundImagesFolder);
        }

        private void RestoreUserOptions(UserOptions options)
        {
            settingManager.appearanceConditions.AutoHideTitleBar = options.AutoHideTitleBar;
            settingManager.appearanceConditions.ShowActionButtonMouseOver = options.ShowActionButtonMouseOver;
            settingManager.appearanceConditions.HideActionNameAfterIcon = options.HideActionNameAfterIcon;
            settingManager.appearanceConditions.ShowActionIconShadow = options.ShowActionIconShadow;
            settingManager.appearanceConditions.EnablePreview = true;
        }

        private void RefreshInterfaceDisplay()
        {
            _suppressWrites++;
            try
            {
                ApplyButtonSettings();
                ApplyColorSettings();
                ApplyFontSettings();
                ApplyBackgroundImageSettings();
                ApplyBlurAndCornerSettings();
                ApplyOptionSettings();
            }
            finally
            {
                _suppressWrites--;
            }

            if (settingManager.appearanceConditions.EnablePreview)
            {
                LoadGlobalButtonsForPreview();
            }
        }

        private class UserOptions
        {
            public bool AutoHideTitleBar { get; set; }
            public bool ShowActionButtonMouseOver { get; set; }
            public bool HideActionNameAfterIcon { get; set; }
            public bool ShowActionIconShadow { get; set; }
        }

        private static Appearance CreatePreset()
        {
            return new Appearance
            {
                ButtonSize = 72,
                ButtonGap = 0,
                BorderWidth = 0,
                ButtonCornerRadius = 0,
                BackgroundColor = "#60FFFFFF",
                BorderColor = "#00FFFFFF",
                ToolbarColor = "#1F999999",
                ToolbarIconColor = "#FF666666",
                ActionButtonColor = "#9DFFFFFF",
                ActionButtonMouseOverColor = "#59B2F2FF",
                BlankButtonColor = "#32C8C8C8",
                BlankButtonMouseOverColor = "#05000000",
                ButtonTextColor = "#FF000000",
                Font1 = -1,
                Font2 = -1,
                FontSize = 12,
                FontWeight = 4,
                BackgroundImagePath = "",
                BackgroundImageOpacity = 1.0,
                Blur = 1,
                Win11CornerRadius = 0,
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

        private static readonly Appearance PresetOriginal = CreatePreset(p =>
        {
            p.ButtonSize = 79;
            p.ButtonGap = 1;
            p.BackgroundColor = "#99B0B0B0";
            p.ToolbarColor = "#18999999";
            p.ActionButtonColor = "#FFFFFFFF";
            p.ActionButtonMouseOverColor = "#FFB2F2FF";
        });

        private static readonly Appearance PresetSimple = CreatePreset(p =>
        {
            p.ButtonGap = 0.2;
            p.BackgroundColor = "#C5CCCCCC";
            p.ToolbarColor = "#2B999999";
            p.ActionButtonColor = "#FFFFFFFF";
            p.ActionButtonMouseOverColor = "#59B2F2FF";
        });

        private static readonly Appearance PresetTranslucent = CreatePreset();

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

        private void ColorPickerPopup_Closed(object sender, EventArgs e)
        {
            if (_colorChangeDebounceTimer != null && _colorChangeDebounceTimer.IsEnabled)
                _colorChangeDebounceTimer.Stop();

            FlushColorChange();
        }

        #region 字体列表异步加载及补齐
        private static List<string> _cachedSystemFonts;

        private async Task InitializeFontComboBoxes()
        {
            if (_cachedSystemFonts == null)
            {
                _cachedSystemFonts = await Task.Run(() =>
                {
                    var list = Fonts.SystemFontFamilies.Select(f => f.Source).OrderBy(f => f).ToList();
                    list.Add("(系统默认)");
                    return list;
                });
            }

            if (settingManager == null) return; // 页面已卸载

            FontSizeComboBox1.ItemsSource = _cachedSystemFonts;
            FontSizeComboBox2.ItemsSource = _cachedSystemFonts;

            // 列表加载完成后补一次选中项并应用全局字体；期间抑制写库，避免启动时就把设置写回数据库
            _suppressWrites++;
            try
            {
                ApplyFontSettings();
            }
            finally
            {
                _suppressWrites--;
            }
        }

        private void ApplyGlobalFontFamily()
        {
            var fontFamilies = FontSizeComboBox1.ItemsSource as IList<string>;
            if (fontFamilies == null || fontFamilies.Count == 0) return;

            int defaultIndex = fontFamilies.Count - 1;

            // 优先通过名字匹配，兼容老数据的下标索引
            int index1 = !string.IsNullOrEmpty(settingManager.appearanceConditions.Font1Name)
                ? fontFamilies.IndexOf(settingManager.appearanceConditions.Font1Name)
                : settingManager.appearanceConditions.Font1;

            int index2 = !string.IsNullOrEmpty(settingManager.appearanceConditions.Font2Name)
                ? fontFamilies.IndexOf(settingManager.appearanceConditions.Font2Name)
                : settingManager.appearanceConditions.Font2;

            FontSizeComboBox1.SelectedIndex = (index1 < 0 || index1 >= fontFamilies.Count) ? defaultIndex : index1;
            FontSizeComboBox2.SelectedIndex = (index2 < 0 || index2 >= fontFamilies.Count) ? defaultIndex : index2;

            string font1 = FontSizeComboBox1.SelectedItem as string;
            string font2 = FontSizeComboBox2.SelectedItem as string;

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
                fontFamily = new FontFamily("微软雅黑, Segoe UI, sans-serif");
            }

            Application.Current.Resources["GlobalFontFamily"] = fontFamily;
        }
        private void ResetAppearanceButton_Click(object sender, RoutedEventArgs e)
        {
            ResetAppearanceButton.Visibility = Visibility.Collapsed;
            if (settingManager != null)
            {
                bool currentPreviewSetting = settingManager.appearanceConditions.EnablePreview;
                settingManager.RestoreOriginalAppearanceSettings();
                settingManager.appearanceConditions.EnablePreview = currentPreviewSetting;
                RefreshInterfaceDisplay();
                SettingDatabase.UpdateAppearance(settingManager.appearanceConditions);
                AppStateManager.NotifyAppearanceChanged(settingManager.appearanceConditions);
            }
        }
        private void EnablePreviewCheckBox_Click(object sender, RoutedEventArgs e)
        {
            SetPreviewEnabled(EnablePreviewCheckBox.IsChecked == true);
        }

        /// <summary>
        /// 开启/关闭预览的统一入口：同步复选框、可见性，并立即落库与通知
        /// </summary>
        private void SetPreviewEnabled(bool enabled)
        {
            if (settingManager == null) return;
            EnablePreviewCheckBox.IsChecked = enabled;
            settingManager.appearanceConditions.EnablePreview = enabled;
            SettingDatabase.UpdateAppearance(settingManager.appearanceConditions);
            AppStateManager.NotifyAppearanceChanged(settingManager.appearanceConditions);
            ViewPreviewBorder.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
            if (enabled)
            {
                LoadGlobalButtonsForPreview();
            }
        }
        #endregion
    }
}