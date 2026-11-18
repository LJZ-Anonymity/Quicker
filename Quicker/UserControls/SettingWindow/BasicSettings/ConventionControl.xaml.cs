using Quicker.Windows.MainWindows;
using System.Windows.Threading;
using System.Windows.Controls;
using System.ComponentModel;
using Quicker.Database.Core;
using System.Windows.Media;
using System.Windows.Input;
using Quicker.Managers;
using Quicker.Helpers;
using System.Windows;

namespace Quicker.UserControls.SettingWindow.BasicSettings
{
    public partial class ConventionControl : UserControl, INotifyPropertyChanged
    {
        private const string DefaultTrayIconPathRunning = "pack://application:,,,/Resources/Images/Quicker_Enabled.png"; // 运行时托盘图标路径
        private const string DefaultTrayIconPathPaused = "pack://application:,,,/Resources/Images/Quicker_Disabled.png"; // 暂停时托盘图标路径
        private WeakReference<Quicker.Windows.MainWindows.SettingWindow> weakSettingWindow; // 弱引用设置窗口
        private double currentSessionTime; // 当次应用使用时长
        SettingManager settingManager; // 设置管理器
        private double totalUsageTime; // 总使用时长
        private DispatcherTimer timer; // 定时器

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public ImageSource RunningTrayIconImage
        {
            get
            {
                var path = settingManager?.conventions?.TrayIconPathRunning; // 获取运行时托盘图标路径
                if (!string.IsNullOrWhiteSpace(path))
                {
                    try
                    {
                        if (path.StartsWith("pack://"))
                            return new System.Windows.Media.Imaging.BitmapImage(new Uri(path, UriKind.Absolute));
                        if (System.IO.File.Exists(path))
                            return new System.Windows.Media.Imaging.BitmapImage(new Uri(path, UriKind.Absolute));
                    }
                    catch { }
                }
                return new System.Windows.Media.Imaging.BitmapImage(new Uri(DefaultTrayIconPathRunning));
            }
        }
        public ImageSource PausedTrayIconImage
        {
            get
            {
                var path = settingManager?.conventions?.TrayIconPathPaused; // 获取暂停时托盘图标路径
                if (!string.IsNullOrWhiteSpace(path))
                {
                    try
                    {
                        if (path.StartsWith("pack://"))
                            return new System.Windows.Media.Imaging.BitmapImage(new Uri(path, UriKind.Absolute));
                        if (System.IO.File.Exists(path))
                            return new System.Windows.Media.Imaging.BitmapImage(new Uri(path, UriKind.Absolute));
                    }
                    catch { }
                }
                return new System.Windows.Media.Imaging.BitmapImage(new Uri(DefaultTrayIconPathPaused));
            }
        }

        public ConventionControl(Quicker.Windows.MainWindows.SettingWindow settingWindow)
        {
            InitializeComponent();
            weakSettingWindow = new(settingWindow); // 保存设置窗口
            settingManager = settingWindow._settingManager; // 获取设置管理器
            InitializeAsync(); // 异步初始化
        }

        // 异步初始化方法
        private async void InitializeAsync()
        {
            await LoadSettingsAsync(); // 异步加载设置
            await LoadUsageTimeAsync(); // 异步加载使用时长
        }

        // 异步加载设置
        private async Task LoadSettingsAsync()
        {
            settingManager.LoadConventionsAsync(); // 初始化缓存数据
            Application.Current.Dispatcher.Invoke(() =>
            {
                VersionLabel.Content = $"当前版本：{VersionHelper.CurrentVersion}"; // 加载版本信息（程序集版本）
                AutoStartCheckBox.IsChecked = settingManager.conventions.AutoStart; // 加载开机自启动设置
                ShowNotificationCheckBox.IsChecked = settingManager.conventions.ShowNotification; // 加载显示启动完成提示设置
                ShowAddImageCheckBox.IsChecked = settingManager.conventions.ShowAddImage; // 加载左键点击空白按钮时显示创建动作菜单设置
                HideTooltipCheckBox.IsChecked = settingManager.conventions.HideTooltip; // 加载隐藏提示框设置
                LongPressThresholdTextBox.Text = settingManager.conventions.LongPressThreshold.ToString(); // 加载长按阈值设置
                MouseMovePixelsTextBox.Text = settingManager.conventions.MouseMovePixels.ToString(); // 加载鼠标移动像素设置
                LoopPageFlippingCheckBox.IsChecked = settingManager.conventions.LoopPageFlipping; // 加载循环翻页设置
                UseMenuAnimationCheckBox.IsChecked = settingManager.conventions.UseMenuAnimation; // 加载启用菜单动画设置
                RememberLastPageCheckBox.IsChecked = settingManager.conventions.RememberLastPage; // 加载记住设置窗口中最后打开的页面
                EnableMemoryOptimizationCheckBox.IsChecked = settingManager.conventions.EnableMemoryOptimization; // 加载启用内存优化设置
            });
        }

        // 异步加载使用时长
        private async Task LoadUsageTimeAsync()
        {
            DateTime currentTime = DateTime.Now; // 获取当前时间
            var Conventions = SettingDatabase.GetAllConventions().FirstOrDefault(); // 获取设置信息
            totalUsageTime = Conventions.TotalUsageTime + (currentTime - AppStateManager.RecordedTime).TotalSeconds; // 加载总使用时长
            currentSessionTime = (currentTime - AppStateManager.StartTime).TotalSeconds; // 更新当次应用使用时长
            timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) }; // 创建定时器
            timer.Tick += Timer_Tick; // 定时器每秒更新使用时长
            timer.Start(); // 启动定时器
            Application.Current.Dispatcher.Invoke(() => // 更新界面显示
            {
                // 当次应用使用时长
                var currentSessionTimeSpan = TimeSpan.FromSeconds(currentSessionTime);
                double currentSessionHours = currentSessionTimeSpan.TotalHours;
                CurrentUsingTimeTextBlock.Text = $"{currentSessionHours:0}:{currentSessionTimeSpan:mm}:{currentSessionTimeSpan:ss}";

                // 总使用时长
                var totalTimeSpan = TimeSpan.FromSeconds(totalUsageTime);
                double totalHours = totalTimeSpan.TotalHours;
                TotalUsageTimeTextBlock.Text = $"{totalHours:0}:{totalTimeSpan:mm}:{totalTimeSpan:ss}";
            });
        }

        // 定时器每秒更新使用时长
        private void Timer_Tick(object sender, EventArgs e)
        {
            currentSessionTime += 1; // 更新当次应用使用时长
            totalUsageTime += 1; // 更新总使用时长
            Application.Current.Dispatcher.Invoke(() => // 更新界面显示
            {
                // 当次应用使用时长
                var currentSessionTimeSpan = TimeSpan.FromSeconds(currentSessionTime);
                double currentSessionHours = currentSessionTimeSpan.TotalHours;
                CurrentUsingTimeTextBlock.Text = $"{currentSessionHours:0}:{currentSessionTimeSpan:mm}:{currentSessionTimeSpan:ss}";

                // 总使用时长
                var totalTimeSpan = TimeSpan.FromSeconds(totalUsageTime);
                double totalHours = totalTimeSpan.TotalHours;
                TotalUsageTimeTextBlock.Text = $"{totalHours:0}:{totalTimeSpan:mm}:{totalTimeSpan:ss}";
            });
        }

        // 勾选框点击事件
        private void CheckBox_Click(object sender, RoutedEventArgs e)
        {
            var checkBox = sender as CheckBox;
            if (checkBox == null) return;
            bool value = checkBox.IsChecked == true;
            switch (checkBox.Name)
            {
                case "AutoStartCheckBox":
                    settingManager.conventions.AutoStart = value;
                    break;
                case "ShowNotificationCheckBox":
                    settingManager.conventions.ShowNotification = value;
                    break;
                case "ShowAddImageCheckBox":
                    settingManager.conventions.ShowAddImage = value;
                    break;
                case "HideTooltipCheckBox":
                    settingManager.conventions.HideTooltip = value;
                    break;
                case "LoopPageFlippingCheckBox":
                    settingManager.conventions.LoopPageFlipping = value;
                    break;
                case "UseMenuAnimationCheckBox":
                    settingManager.conventions.UseMenuAnimation = value;
                    break;
                case "RememberLastPageCheckBox":
                    settingManager.conventions.RememberLastPage = value;
                    break;
                case "EnableMemoryOptimizationCheckBox":
                    settingManager.conventions.EnableMemoryOptimization = value;
                    break;
                default:
                    return;
            }
        }

        // 文本框内容改变事件
        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;
            switch (textBox.Name)
            {
                case "LongPressThresholdTextBox":
                    if (int.TryParse(textBox.Text, out int longPress))
                    {
                        settingManager.conventions.LongPressThreshold = Math.Clamp(longPress, 30, 3000);
                    }
                    break;
                case "MouseMovePixelsTextBox":
                    if (int.TryParse(textBox.Text, out int movePixels))
                    {
                        settingManager.conventions.MouseMovePixels = Math.Clamp(movePixels, 1, 200);
                    }
                    break;
                default:
                    return;
            }
        }

        private void TrayIconButton_Click(object sender, RoutedEventArgs e)
        {
            TrayIconPopup.IsOpen = true; // 显示托盘图标菜单
        }

        // 选择托盘图标文件（仅支持ICO和PNG）
        private string SelectTrayIconFilePath(string title)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = title,
                Filter = "图标文件 (*.ico;*.png)|*.ico;*.png"
            }; // 设置文件过滤器
            return dialog.ShowDialog() == true ? dialog.FileName : null; // 显示文件选择对话框
        }

        /// <summary>
        /// 选择图片后保存到本地图标目录并返回保存路径
        /// </summary>
        /// <param name="file">用户选择的图片文件路径</param>
        /// <returns>保存到本地后的路径，失败返回null</returns>
        private string SaveCustomTrayIcon(string file)
        {
            if (string.IsNullOrEmpty(file)) return null;
            var iconManager = new IconManager();
            return iconManager.SaveImageToLocalIcons(file);
        }

        // 编辑运行时托盘图标
        private void EditRunningTrayIconButton_Click(object sender, RoutedEventArgs e)
        {
            var file = SelectTrayIconFilePath("选择运行时托盘图标");
            var savedPath = SaveCustomTrayIcon(file); // 保存到本地
            if (!string.IsNullOrEmpty(savedPath))
            {
                settingManager.conventions.TrayIconPathRunning = savedPath; // 设置运行时托盘图标路径
                OnPropertyChanged(nameof(RunningTrayIconImage)); // 更新运行时托盘图标
            }
        }

        // 编辑暂停时托盘图标
        private void EditPausedTrayIconButton_Click(object sender, RoutedEventArgs e)
        {
            var file = SelectTrayIconFilePath("选择暂停时托盘图标");
            var savedPath = SaveCustomTrayIcon(file); // 保存到本地
            if (!string.IsNullOrEmpty(savedPath))
            {
                settingManager.conventions.TrayIconPathPaused = savedPath; // 设置暂停时托盘图标路径
                OnPropertyChanged(nameof(PausedTrayIconImage)); // 更新暂停时托盘图标
            }
        }

        // 右键恢复默认托盘图标
        private void RestoreDefaultTrayIconButton_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Button btn)
            {
                if (btn.Name == "EditRunningTrayIconButton") // 恢复运行时图标为默认
                {
                    settingManager.conventions.TrayIconPathRunning = DefaultTrayIconPathRunning; // 设置运行时托盘图标路径为默认
                    OnPropertyChanged(nameof(RunningTrayIconImage)); // 更新运行时托盘图标
                }
                else if (btn.Name == "EditPausedTrayIconButton") // 恢复暂停时图标为默认
                {
                    settingManager.conventions.TrayIconPathPaused = DefaultTrayIconPathPaused; // 设置暂停时托盘图标路径为默认
                    OnPropertyChanged(nameof(PausedTrayIconImage)); // 更新暂停时托盘图标
                }
            }
        }

        // 窗体关闭释放资源
        private void ConventionGrid_Unloaded(object sender, RoutedEventArgs e)
        {
            timer.Stop(); // 停止定时器
            totalUsageTime = 0; // 清空总使用时长
            currentSessionTime = 0; // 清空当次应用使用时长
            AutoStartCheckBox = null;
            ShowNotificationCheckBox = null;
            ShowAddImageCheckBox = null;
            HideTooltipCheckBox = null;
            LongPressThresholdTextBox = null;
            MouseMovePixelsTextBox = null;
            LoopPageFlippingCheckBox = null;
            CurrentUsingTimeTextBlock = null;
            TotalUsageTimeTextBlock = null;
            GC.Collect(); // 垃圾回收
        }
    }
}