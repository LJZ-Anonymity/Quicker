using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using UserControl = System.Windows.Controls.UserControl;
using CheckBox = System.Windows.Controls.CheckBox;
using Application = System.Windows.Application;
using Button = System.Windows.Controls.Button;
using System.Windows.Media.Imaging;
using Quicker.Windows.ToolWindows;
using System.Windows.Controls;
using Quicker.Models.Settings;
using Quicker.Database.Core;
using System.Windows.Input;
using System.Windows.Forms;
using Quicker.UserControls;
using System.Windows.Media;
using System.Diagnostics;
using Quicker.Managers;
using System.Windows;
using Quicker.Models;
using System.IO;

namespace Quicker.UserControls.SettingWindow.BasicSettings
{
    public partial class BlacklistControl : UserControl
    {
        private WeakReference<Quicker.Windows.MainWindows.SettingWindow> weakSettingWindow; // 弱引用设置窗口
        private HashSet<string> blacklistAppsCache = new(); // 黑名单缓存
        private WindowSelectWindow windowSelectWindow; // 类成员变量
        SettingManager settingManager; // 设置管理器
        private bool isLoading = true; // 是否全屏禁用

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="settingWindow">设置窗口</param>
        public BlacklistControl(Quicker.Windows.MainWindows.SettingWindow settingWindow)
        {
            InitializeComponent();
            settingManager = settingWindow._settingManager; // 创建设置管理器
            weakSettingWindow = new(settingWindow); // 保存设置窗口
            InitializeAsync(); // 异步初始化
        }

        // 异步初始化方法
        private async void InitializeAsync()
        {
            await LoadSettingsAsync(); // 异步加载设置
        }

        /// <summary>
        /// 异步加载设置
        /// </summary>
        private async Task LoadSettingsAsync()
        {
            try
            {
                var loadSettingsTask = settingManager.LoadBlacklistSettingsAsync(); // 加载设置
                var loadBlacklistAppsTask = Task.Run(() => SettingDatabase.GetAllBlacklistApplications()); // 加载黑名单应用
                await Task.WhenAll(loadSettingsTask, loadBlacklistAppsTask); // 等待所有任务完成
                await UpdateUISettingsAsync(); // 更新UI设置
                var blacklistApps = await loadBlacklistAppsTask; // 获取黑名单应用
                await ProcessBlacklistAppsAsync(blacklistApps); // 处理黑名单应用
                isLoading = false; // 加载完成
            }
            catch (Exception ex)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var toast = new ToastManager(); // 消息提醒管理器
                    toast.Show($"加载设置失败: {ex.Message}", ToastType.Error); // 弹出消息提醒
                }); // 在UI线程更新界面
            }
        }

        // 异步更新UI设置
        private async Task UpdateUISettingsAsync()
        {
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                FullScreenDisableCheckBox.IsChecked = settingManager.blacklistSettings.IsFullScreenDisabled;
            });
        }

        /// <summary>
        /// 异步处理黑名单应用
        /// </summary>
        /// <param name="blacklistApps">黑名单应用列表</param>
        private async Task ProcessBlacklistAppsAsync(List<BlacklistApplication> blacklistApps)
        {
            LoadBlacklistAppsIntoCache(); // 加载黑名单应用到缓存

            // 分类处理黑名单和白名单应用
            var blacklistDict = new Dictionary<string, BlacklistApplication>();
            var whitelistDict = new Dictionary<string, BlacklistApplication>();
            foreach (var app in blacklistApps)
            {
                if (app.IsInBlacklist)
                    blacklistDict[app.ApplicationName] = app;
                else
                    whitelistDict[app.ApplicationName] = app;
            }

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                // 添加黑名单应用
                foreach (var app in blacklistDict.Values)
                {
                    AddBlacklistItem(app.ApplicationName, app.IsFolder);
                }

                // 添加白名单应用
                foreach (var app in whitelistDict.Values)
                {
                    AddWhitelistItem(app.ApplicationName);
                }
            }); // 批量更新UI

            // 清理字典
            blacklistDict.Clear();
            whitelistDict.Clear();
        }

        // 拖动按钮选择屏幕上要禁用的应用
        private void BlacklistDragButton_Click(object sender, RoutedEventArgs e)
        {
            windowSelectWindow = new WindowSelectWindow();
            windowSelectWindow.WindowSelected += OnWindowSelectedForBlacklist;
            // 最小化设置窗口
            if (weakSettingWindow != null && weakSettingWindow.TryGetTarget(out var settingWindow))
            {
                settingWindow.WindowState = WindowState.Minimized;
            }
            windowSelectWindow.StartSelecting(null); // 进入选择模式
        }

        /// <summary>
        /// 判断进程是否有窗口（包括最小化的窗口）
        /// </summary>
        /// <param name="process">进程</param>
        /// <returns>是否存在窗口</returns>
        private bool HasWindow(Process process)
        {
            return process.MainWindowHandle != IntPtr.Zero; // 判断是否有窗口句柄
        }

        // 加载黑名单应用到缓存
        private void LoadBlacklistAppsIntoCache()
        {
            var blacklistApps = SettingDatabase.GetAllBlacklistApplications(); // 获取黑名单应用
            blacklistAppsCache.Clear(); // 清空缓存
            foreach (var app in blacklistApps)
            {
                blacklistAppsCache.Add(app.ApplicationName); // 添加到缓存中
            }
        }

        /// <summary>
        /// 判断进程是否在黑名单中
        /// </summary>
        /// <param name="process">进程名</param>
        /// <returns>是否在黑名单中</returns>
        private bool IsInBlacklist(string process)
        {
            return blacklistAppsCache.Contains(process); // 判断是否在黑名单中
        }

        /// <summary>
        /// 添加到正在运行的程序提示框中
        /// </summary>
        /// <param name="appPath">应用路径</param>
        /// <param name="isBlacklist">是否为黑名单</param>
        private void AddAppItems(string appPath, bool isBlacklist = true)
        {
            string appNames = Path.GetFileName(appPath); // 获取进程名
            Button button = new()
            {
                Style = (Style)FindResource("MenuButton"),
                Tag = appNames
            }; // 创建按钮

            StackPanel stackPanel = new()
            {
                Style = (Style)FindResource("BlacklistItemStackPanel")
            }; // 创建StackPanel
            button.Content = stackPanel; // 设置按钮内容

            Image iconImage = new()
            {
                Visibility = appPath == "从计算机选择程序..." ? Visibility.Hidden : Visibility.Visible,
                Style = (Style)FindResource("BlacklistItemIcon"),
                Source = IconManager.GetIcon(appPath)
            }; // 创建图标
            stackPanel.Children.Add(iconImage); // 添加到StackPanel

            TextBlock textBlock = new()
            {
                Style = (Style)FindResource("BlacklistItemTextBlock"),
                Text = appNames
            }; // 创建TextBlock
            stackPanel.Children.Add(textBlock); // 添加进程名称

            if (isBlacklist)
            {
                button.Click += AddToBlacklistButton_Click; // 绑定按钮点击事件
                AddBlacklistAppsStackPanel.Children.Add(button); // 添加到父容器StackPanel
                AddBlacklistAppsPop.Height += 26; // 设置高度
            }
            else
            {
                button.Click += AddToWhitelistButton_Click; // 绑定按钮点击事件
                AddWhitelistAppsStackPanel.Children.Add(button); // 添加到父容器StackPanel
                AddWhitelistAppsPop.Height += 26; // 设置高度
            }
        }

        /// <summary>
        /// 将选中的文件添加到名单
        /// </summary>
        /// <param name="isBlacklist">是否为黑名单</param>
        private void SelectLocalFile(bool isBlacklist = true)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "可执行程序(*.exe)|*.exe|任意文件(*.*)|*.*" // 设置文件类型过滤器
            };

            if (openFileDialog.ShowDialog() == true) // 检查用户是否点击了“确定”
            {
                string filePath = openFileDialog.FileName; // 获取选择的文件路径
                string processName = Path.GetFileNameWithoutExtension(filePath); // 获取进程名
                SettingDatabase.ApplyBlacklistApplication(processName, processName, isBlacklist, false); // 添加到设置中
                AppStateManager.LoadSettings(); // 加载设置
                if (isBlacklist)
                    AddBlacklistItem(processName); // 添加到黑名单
                else
                    AddWhitelistItem(processName); // 添加到白名单
            }
        }

        // 将选中的进程添加到名单
        private void AddToBlacklistButton_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button; // 转换发送者为按钮对象
            if (button.Tag.ToString() == "从计算机选择程序...") // 如果是选择文件
                SelectLocalFile(); // 选择本地文件
            else
            {
                SettingDatabase.ApplyBlacklistApplication(button.Tag.ToString(), Path.GetFileNameWithoutExtension(button.Tag.ToString()), true, false); // 添加到设置中
                AppStateManager.LoadSettings(); // 加载设置
                AddBlacklistItem(button.Tag.ToString()); // 添加到黑名单
            }
            AddBlacklistAppsPop.IsOpen = false; // 关闭提示框
        }

        // 将选中的进程添加到白名单
        private void AddToWhitelistButton_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button; // 转换发送者为按钮对象
            if (button.Tag.ToString() == "从计算机选择程序...") // 如果是选择文件
                SelectLocalFile(false); // 选择本地文件
            else
            {
                SettingDatabase.ApplyBlacklistApplication(button.Tag.ToString(), Path.GetFileNameWithoutExtension(button.Tag.ToString()), false, false); // 添加到设置中
                AppStateManager.LoadSettings(); // 加载设置
                AddWhitelistItem(button.Tag.ToString()); // 添加到白名单
            }
            AddWhitelistAppsPop.IsOpen = false; // 关闭提示框
        }

        // 添加未知应用
        private void UnknownProcessButton_Click(object sender, RoutedEventArgs e)
        {
            AddDirectoryButton.Margin = new Thickness(295, 199, 0, 0); // 调整按钮位置
            UnknownProcessButton.Visibility = Visibility.Collapsed; // 隐藏按钮
            var blacklistprocess = SettingDatabase.GetAllBlacklistApplications(); // 获取黑名单进程
            if (blacklistprocess.Any(p => p.ProcessName == "unknown-proc.exe" && p.IsInBlacklist))
            {
                var toast = new ToastManager(); // 消息提醒管理器
                toast.Show("应用已添加过：unknown-proc.exe", ToastType.Error); // 弹出消息提醒
            }
            else
            {
                SettingDatabase.ApplyBlacklistApplication("unknown-proc.exe", "unknown-proc.exe", true, false); // 添加到设置中
                AppStateManager.LoadSettings(); // 加载设置
                AddBlacklistItem("unknown-proc.exe"); // 添加到黑名单
            }
        }

        // 将选中文件夹里的 .exe 文件添加到黑名单
        private void AddDirectoryButton_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new FolderBrowserDialog()) // 创建文件夹选择对话框
            {
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    Quicker.Windows.MainWindows.SettingWindow settingWindow = System.Windows.Application.Current.Windows.OfType<Quicker.Windows.MainWindows.SettingWindow>().FirstOrDefault(); // 尝试查找现有的设置窗口
                    LoadingWindow loadingWindow = new() { Owner = settingWindow }; // 创建加载窗口
                    loadingWindow.Show(); // 显示加载窗口
                    string selectedPath = dialog.SelectedPath; // 获取选择的文件夹路径
                    AddBlacklistItem(selectedPath, true); // 添加完整路径到黑名单
                    foreach (string file in Directory.GetFiles(selectedPath, "*", SearchOption.AllDirectories)) // 遍历文件夹中的所有文件
                    {
                        if (!Path.GetExtension(file).Equals(".exe", StringComparison.OrdinalIgnoreCase)) continue; // 如果不是.exe文件, 跳过
                        string fileName = Path.GetFileNameWithoutExtension(file); // 获取无后缀的文件名
                        SettingDatabase.ApplyBlacklistApplication(selectedPath, fileName, true, true); // 添加到设置中
                    }
                    AppStateManager.LoadSettings(); // 加载设置
                    loadingWindow.Close(); // 关闭加载窗口
                }
            }
        }

        /// <summary>
        /// 向黑名单中添加进程
        /// </summary>
        /// <param name="process">进程名</param>
        /// <param name="isFolder">是否为文件夹</param>
        private void AddBlacklistItem(string process, bool isFolder = false)
        {
            Border border = new()
            {
                Style = (Style)FindResource("BlacklistItemBorder"),
                Tag = false
            }; // 创建Border
            border.MouseEnter += HightLightBlacklistItem; // 绑定鼠标移入事件
            border.MouseLeave += FadeBlacklistItem; // 绑定鼠标移出事件
            border.MouseDown += SelectBlacklistItem; // 绑定鼠标按下事件

            Grid grid = new() { Style = (Style)FindResource("BlacklistItemGrid") }; // 创建Grid
            border.Child = grid; // 设置Border内容

            StackPanel stackPanel = new() { Style = (Style)FindResource("BlacklistItemIconStackPanel") }; // 创建StackPanel
            grid.Children.Add(stackPanel); // 添加StackPanel

            TextBlock textBlock = new()
            {
                Style = (Style)FindResource("BlacklistItemTextBlock"),
                Text = process
            }; // 创建TextBlock
            stackPanel.Children.Add(textBlock); // 添加进程名称

            if (isFolder) // 如果是文件夹
            {
                TextBlock folderLabel = new() { Style = (Style)FindResource("BlacklistItemFolderTextBlock") }; // 创建TextBlock
                stackPanel.Children.Add(folderLabel); // 添加TextBlock
            }

            Button button = new()
            {
                Style = (Style)FindResource("DeleteBlacklistItem"),
                Tag = process
            }; // 创建按钮
            button.Click += DeleteFromBlacklist; // 绑定删除事件
            grid.Children.Add(button); // 添加按钮

            Image image = new() { Style = (Style)FindResource("BlacklistItemDeleteIcon") }; // 创建图标
            button.Content = image; // 添加图标
            BlacklistStackPanel.Children.Add(border); // 添加到父容器StackPanel
        }

        /// <summary>
        /// 向白名单中添加进程
        /// </summary>
        /// <param name="process">进程名</param>
        private void AddWhitelistItem(string process)
        {
            if (BlacklistProcessTextBox.Text.Length > 0)
                BlacklistProcessTextBox.Text += ";" + process; // 添加到文本框中
            else
                BlacklistProcessTextBox.Text = process; // 设置文本框内容
        }

        // 从黑名单中删除进程
        private void DeleteFromBlacklist(object sender, RoutedEventArgs e)
        {
            var button = sender as Button; // 转换发送者为按钮对象
            var grid = button.Parent as Grid; // 获取按钮的父容器（Grid）
            var border = grid.Parent as Border; // 获取Grid的父容器（Border）
            var process = button.Tag.ToString(); // 获取进程名
            SettingDatabase.DeleteBlacklistApplication(process); // 从设置中删除进程
            AppStateManager.LoadSettings(); // 加载设置
            BlacklistStackPanel.Children.Remove(border); // 将Grid从父容器StackPanel中移除
        }

        // 鼠标移入Border高亮显示黑名单项
        private void HightLightBlacklistItem(object sender, MouseEventArgs e)
        {
            var border = (Border)sender; // 转换发送者为Border对象
            if (!(bool)border.Tag) // 如果Border没有被选中
                border.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F3F3F3"));
        }

        // 鼠标移出Border恢复原状
        private void FadeBlacklistItem(object sender, MouseEventArgs e)
        {
            var border = (Border)sender; // 转换发送者为Border对象
            if (!(bool)border.Tag) // 如果Border没有被选中
                border.Background = Brushes.Transparent;
        }

        // 鼠标按下Border选中黑名单项
        private void SelectBlacklistItem(object sender, MouseButtonEventArgs e)
        {
            Border targetBorder = (Border)sender; // 转换发送者为Border对象
            foreach (var border in BlacklistStackPanel.Children.OfType<Border>()) // 取消上一个选中项
            {
                if ((bool)border.Tag) // 仅处理已选中项
                {
                    border.Tag = false;
                    border.Background = Brushes.Transparent;
                }
            }

            // 设置当前选中项
            targetBorder.Tag = true;
            targetBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFEAEAEA"));
        }

        // 勾选框点击事件
        private void CheckBox_Click(object sender, RoutedEventArgs e)
        {
            var checkBox = sender as CheckBox;
            if (checkBox == null) return;
            bool value = checkBox.IsChecked == true;
            switch (checkBox.Name)
            {
                case "FullScreenDisableCheckBox":
                    settingManager.blacklistSettings.IsFullScreenDisabled = value;
                    break;
                case "ApplyBlacklistToExpandHotkeysCheckBox":
                    settingManager.blacklistSettings.IsBlacklistEnabledForExtendedHotkey = value;
                    break;
                default:
                    return;
            }
        }

        // 黑名单添加按钮点击事件
        private void BlacklistAddButton_Click(object sender, RoutedEventArgs e)
        {
            ShowAppsPop(true); // 显示黑名单应用选择窗口
        }

        // 选择在禁用Quicker时仍然能够打开Quicker的应用
        private void WhitelistSelectButton_Click(object sender, RoutedEventArgs e)
        {
            ShowAppsPop(false); // 显示白名单应用选择窗口
        }

        /// <summary>
        /// 清理应用列表UI
        /// </summary>
        /// <param name="isInBlacklist">是否在黑名单中</param>
        private void ClearAppsListUI(bool isInBlacklist)
        {
            if (isInBlacklist)
            {
                AddBlacklistAppsStackPanel.Children.Clear(); // 清空列表
                AddBlacklistAppsPop.Height = 4; // 重置高度
            }
            else
            {
                AddWhitelistAppsStackPanel.Children.Clear(); // 清空列表
                AddWhitelistAppsPop.Height = 4; // 重置高度
            }
        }

        /// <summary>
        /// 获取符合条件的进程列表
        /// </summary>
        /// <returns>进程信息列表</returns>
        private List<(string FileName, string ProcessName)> GetFilteredProcessList()
        {
            var processList = new List<(string FileName, string ProcessName)>(); // 创建列表
            var uniqueProcessNames = new HashSet<string>(); // 创建集合

            try
            {
                // 只获取有窗口的进程
                var processes = Process.GetProcesses()
                    .Where(p => HasWindow(p))
                    .Take(20); // 限制最大进程数

                foreach (var process in processes)
                {
                    try
                    {
                        string processFileName = process.MainModule.FileName; // 获取进程文件名
                        string fullProcessName = Path.GetFileName(processFileName); // 获取进程名

                        if (IsInBlacklist(fullProcessName) || !uniqueProcessNames.Add(fullProcessName))
                            continue; // 如果进程在黑名单中或已存在，跳过

                        processList.Add((processFileName, fullProcessName)); // 添加到列表
                        if (processList.Count >= 8) break; // 如果超过8个，跳出循环
                    }
                    catch { } // 忽略异常
                    finally
                    {
                        process?.Dispose(); // 释放进程
                    }
                }
            }
            catch { } // 忽略异常

            return processList;
        }

        /// <summary>
        /// 更新应用列表UI
        /// </summary>
        /// <param name="processList">进程信息列表</param>
        /// <param name="isInBlacklist">是否在黑名单中</param>
        private void UpdateAppsListUI(List<(string FileName, string ProcessName)> processList, bool isInBlacklist)
        {
            foreach (var (fileName, processName) in processList)
            {
                AddAppItems(fileName, isInBlacklist); // 添加到列表
            }

            AddAppItems("从计算机选择程序...", isInBlacklist); // 添加到列表
            if (isInBlacklist)
                AddBlacklistAppsPop.IsOpen = true; // 打开黑名单程序提示框
            else
                AddWhitelistAppsPop.IsOpen = true; // 打开白名单程序提示框
        }

        /// <summary>
        /// 显示应用选择窗口
        /// </summary>
        /// <param name="isInBlacklist">是否在黑名单中</param>
        private void ShowAppsPop(bool isInBlacklist)
        {
            // 异步清理UI
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                ClearAppsListUI(isInBlacklist);
            }));
            LoadBlacklistAppsIntoCache(); // 预先加载黑名单缓存
            Task.Run(() => // 在后台线程处理进程
            {
                var processList = GetFilteredProcessList();
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    UpdateAppsListUI(processList, isInBlacklist);
                })); // 在UI线程更新界面
            });
        }

        // 同步滚动条
        private void BlacklistScrollBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            BlacklistStackScrollViewer.ScrollToVerticalOffset(BlacklistScrollBar.Value); // 滚动到指定位置
        }
        private void BlacklistStackScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            BlacklistScrollBar.Maximum = BlacklistStackScrollViewer.ExtentHeight - BlacklistStackScrollViewer.ViewportHeight; // 设置滚动条最大值
            BlacklistScrollBar.ViewportSize = BlacklistStackScrollViewer.ViewportHeight; // 设置滚动条视口大小
            BlacklistScrollBar.Value = BlacklistStackScrollViewer.VerticalOffset; // 设置滚动条值
        }

        // 黑名单进程文本框内容变化事件
        private void BlacklistProcessTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isLoading) return; // 防止在加载过程中执行
            string[] processNames = BlacklistProcessTextBox.Text.Split(';'); // 将文本内容按照分号分隔
            var dbWhitelistApps = SettingDatabase.GetAllBlacklistApplications()
                .Where(app => !app.IsInBlacklist)
                .Select(app => app.ApplicationName)
                .ToList(); // 获取数据库中的所有白名单应用

            var currentWhitelistApps = new HashSet<string>(); // 创建集合
            foreach (var processName in processNames) // 遍历文本内容
            {
                if (processName.Length > 0)
                    currentWhitelistApps.Add(processName.Trim()); // 添加到当前白名单集合中
            }

            var appsToRemove = dbWhitelistApps.Except(currentWhitelistApps).ToList(); // 找出数据库中存在但当前白名单中不存在的应用
            foreach (var appToRemove in appsToRemove) // 遍历待删除的白名单应用
            {
                SettingDatabase.DeleteBlacklistApplication(appToRemove); // 从数据库中删除白名单应用
            }
            AppStateManager.LoadSettings(); // 加载设置

            if (currentWhitelistApps.Count > 0) // 如果有白名单应用
                BlacklistProcessTextBox.Text = string.Join(";", currentWhitelistApps); // 更新文本框内容，避免无限递归
            else
                BlacklistProcessTextBox.Text = string.Empty; // 清空文本框内容
        }

        // 处理窗口选择事件
        private void OnWindowSelectedForBlacklist(object sender, WindowSelectWindow.WindowSelectedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.ProcessPath))
            {
                try
                {
                    string processName = System.IO.Path.GetFileNameWithoutExtension(e.ProcessPath).ToLower(); // 添加到黑名单
                    SettingDatabase.ApplyBlacklistApplication(processName, processName, true, false);
                    AppStateManager.LoadSettings();
                    AddBlacklistItem(processName); // 刷新UI
                    // 恢复设置窗口
                    if (weakSettingWindow != null && weakSettingWindow.TryGetTarget(out var settingWindow))
                    {
                        settingWindow.WindowState = WindowState.Normal;
                        settingWindow.Activate();
                    }
                }
                catch { }
                finally
                {
                    windowSelectWindow?.Close();
                }
            }
        }

        // 鼠标移入/移出显示/隐藏滚动条
        private void BlacklistStackScrollViewer_MouseEnter(object sender, MouseEventArgs e)
        {
            BlacklistScrollBar.Visibility = Visibility.Visible;
        }
        private void BlacklistStackScrollViewer_MouseLeave(object sender, MouseEventArgs e)
        {
            BlacklistScrollBar.Visibility = Visibility.Collapsed;
        }

        // 控件关闭释放资源
        private void BlacklistGrid_Unloaded(object sender, RoutedEventArgs e)
        {
            // 清理UI元素
            BlacklistStackPanel.Children.Clear();
            AddBlacklistAppsStackPanel.Children.Clear();
            AddWhitelistAppsStackPanel.Children.Clear();
            BlacklistStackScrollViewer.Content = null;

            // 清理解绑事件处理程序
            BlacklistButton.Click -= BlacklistAddButton_Click;
            BlacklistAddButton.Click -= BlacklistAddButton_Click;
            WhitelistSelectButton.Click -= WhitelistSelectButton_Click;
            UnknownProcessButton.Click -= UnknownProcessButton_Click;
            AddDirectoryButton.Click -= AddDirectoryButton_Click;
            BlacklistDragButton.Click -= BlacklistDragButton_Click;
            BlacklistScrollBar.ValueChanged -= BlacklistScrollBar_ValueChanged;
            BlacklistStackScrollViewer.ScrollChanged -= BlacklistStackScrollViewer_ScrollChanged;

            // 清理外部资源
            settingManager = null;
            blacklistAppsCache.Clear(); // 清理缓存和变量
        }
    }
}