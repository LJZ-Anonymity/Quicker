using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using UserControl = System.Windows.Controls.UserControl;
using CheckBox = System.Windows.Controls.CheckBox;
using Application = System.Windows.Application;
using Button = System.Windows.Controls.Button;
using Quicker.Database.SettingRepositories;
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
using Quicker.Helpers;
using System.Windows;
using Quicker.Models;
using System.IO;

namespace Quicker.UserControls.SettingWindow.BasicSettings
{
    public partial class BlacklistControl : UserControl
    {
        private const string SelectFromComputerText = "从计算机选择程序...";
        private const string UnknownProcessDisplayText = "unknown-proc.exe"; // "无法识别的应用"在界面上的显示文字

        private WeakReference<Quicker.Windows.MainWindows.SettingWindow> weakSettingWindow; // 弱引用设置窗口
        private readonly HashSet<string> existingProcessNames = new(StringComparer.OrdinalIgnoreCase); // 已在任一名单中的进程名（规范化后），用于过滤"正在运行的程序"列表
        private WindowSelectWindow windowSelectWindow; // 拖选窗口
        private BlacklistSetting _setting = new(); // 当前黑名单设置
        private bool _whitelistDirty; // 白名单文本框有未提交的修改
        private bool _syncingWhitelistText; // 正在以代码方式回写白名单文本框（此时的 TextChanged 不算用户修改）

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="settingWindow">设置窗口</param>
        public BlacklistControl(Quicker.Windows.MainWindows.SettingWindow settingWindow)
        {
            InitializeComponent();
            weakSettingWindow = new(settingWindow); // 保存设置窗口
            InitializeAsync(); // 异步初始化
        }

        // 异步初始化方法
        private async void InitializeAsync()
        {
            await LoadSettingsAsync(); // 异步加载设置
        }

        private static void ShowToast(string message, ToastType type)
        {
            new ToastManager().Show(message, type);
        }

        #region 加载与渲染

        /// <summary>
        /// 异步加载设置与规则
        /// </summary>
        private async Task LoadSettingsAsync()
        {
            try
            {
                var settingTask = Task.Run(() => BlacklistRepository.GetSetting());
                var rulesTask = Task.Run(() => BlacklistRepository.GetAllRules());
                await Task.WhenAll(settingTask, rulesTask);

                _setting = settingTask.Result;
                FullScreenDisableCheckBox.IsChecked = _setting.DisableOnFullScreen;
                RenderRules(rulesTask.Result);
            }
            catch (Exception ex)
            {
                ShowToast($"加载设置失败: {ex.Message}", ToastType.Error);
            }
        }

        /// <summary>
        /// 用规则列表重建界面：黑名单列表、白名单文本框、"无法识别的应用"按钮
        /// </summary>
        private void RenderRules(List<BlacklistRule> rules)
        {
            BlacklistStackPanel.Children.Clear();
            existingProcessNames.Clear();

            foreach (var rule in rules)
            {
                if (rule.MatchType == BlacklistMatchType.ProcessName)
                    existingProcessNames.Add(rule.Target);

                if (rule.ListType == BlacklistListType.Blacklist)
                    AddBlacklistItem(rule);
            }

            SetWhitelistText(rules);
            SetUnknownButtonAdded(rules.Any(r => r.ListType == BlacklistListType.Blacklist && r.MatchType == BlacklistMatchType.Unknown));
        }

        /// <summary>
        /// 从数据库重新读取，刷新"已存在的进程名"缓存和白名单文本框
        /// </summary>
        private void RefreshNamesAndWhitelistText()
        {
            var rules = BlacklistRepository.GetAllRules();

            existingProcessNames.Clear();
            foreach (var rule in rules.Where(r => r.MatchType == BlacklistMatchType.ProcessName))
                existingProcessNames.Add(rule.Target);

            SetWhitelistText(rules);
        }

        /// <summary>
        /// 白名单文本框：只显示白名单里"按进程名匹配"的规则
        /// </summary>
        private void SetWhitelistText(List<BlacklistRule> rules)
        {
            var names = rules
                .Where(r => r.ListType == BlacklistListType.Whitelist && r.MatchType == BlacklistMatchType.ProcessName)
                .Select(r => r.Target);

            _syncingWhitelistText = true;
            try
            {
                BlacklistProcessTextBox.Text = string.Join(";", names);
            }
            finally
            {
                _syncingWhitelistText = false;
            }
            _whitelistDirty = false;
        }

        /// <summary>
        /// "无法识别的应用"按钮：已添加则隐藏，并让右侧的"添加目录"按钮左移补位
        /// </summary>
        private void SetUnknownButtonAdded(bool added)
        {
            UnknownProcessButton.Visibility = added ? Visibility.Collapsed : Visibility.Visible;
            AddDirectoryButton.Margin = added ? new Thickness(295, 199, 0, 0) : new Thickness(409, 199, 0, 0);
        }

        private static string GetDisplayText(BlacklistRule rule)
        {
            return rule.MatchType == BlacklistMatchType.Unknown ? UnknownProcessDisplayText : rule.Target;
        }

        #endregion

        #region 添加规则（所有入口统一走这里）

        /// <summary>
        /// 添加一条规则并刷新界面；已存在或冲突时给出提示
        /// </summary>
        private bool AddRuleAndRefresh(BlacklistListType listType, BlacklistMatchType matchType, string target)
        {
            var result = BlacklistRepository.TryAddRule(listType, matchType, target, out var rule);
            switch (result)
            {
                case AddRuleResult.Added:
                    AppStateManager.LoadSettings(); // 重新加载设置（重建运行时规则集）
                    if (rule!.MatchType == BlacklistMatchType.ProcessName)
                        existingProcessNames.Add(rule.Target);

                    if (listType == BlacklistListType.Blacklist)
                    {
                        AddBlacklistItem(rule);
                        if (rule.MatchType == BlacklistMatchType.Unknown)
                            SetUnknownButtonAdded(true);
                    }
                    else
                    {
                        RefreshNamesAndWhitelistText();
                    }
                    return true;

                case AddRuleResult.AlreadyExists:
                    ShowToast($"应用已添加过：{GetDisplayText(rule!)}", ToastType.Error);
                    return false;

                case AddRuleResult.ExistsInOtherList:
                    string otherList = rule!.ListType == BlacklistListType.Blacklist ? "黑名单" : "白名单";
                    ShowToast($"{GetDisplayText(rule)} 已在{otherList}中", ToastType.Error);
                    return false;

                default:
                    ShowToast("无效的应用或路径", ToastType.Error);
                    return false;
            }
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

        // 处理窗口选择事件
        private void OnWindowSelectedForBlacklist(object sender, WindowSelectWindow.WindowSelectedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.ProcessPath))
            {
                try
                {
                    AddRuleAndRefresh(BlacklistListType.Blacklist, BlacklistMatchType.ProcessName, e.ProcessPath);
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

        // 添加未知应用
        private void UnknownProcessButton_Click(object sender, RoutedEventArgs e)
        {
            AddRuleAndRefresh(BlacklistListType.Blacklist, BlacklistMatchType.Unknown, string.Empty);
        }

        // 将选中的文件夹加入黑名单（只存一条规则，运行时按路径前缀匹配，不再逐个扫描 exe）
        private void AddDirectoryButton_Click(object sender, RoutedEventArgs e)
        {
            using var dialog = new FolderBrowserDialog();
            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

            AddRuleAndRefresh(BlacklistListType.Blacklist, BlacklistMatchType.Folder, dialog.SelectedPath);
        }

        // 将选中的进程添加到黑名单
        private void AddToBlacklistButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
                HandleProcessItemClick(button.Tag?.ToString(), BlacklistListType.Blacklist);
            AddBlacklistAppsPop.IsOpen = false; // 关闭提示框
        }

        // 将选中的进程添加到白名单
        private void AddToWhitelistButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
                HandleProcessItemClick(button.Tag?.ToString(), BlacklistListType.Whitelist);
            AddWhitelistAppsPop.IsOpen = false; // 关闭提示框
        }

        private void HandleProcessItemClick(string tag, BlacklistListType listType)
        {
            if (string.IsNullOrEmpty(tag)) return;

            if (tag == SelectFromComputerText)
                SelectLocalFile(listType); // 选择本地文件
            else
                AddRuleAndRefresh(listType, BlacklistMatchType.ProcessName, tag);
        }

        /// <summary>
        /// 将选中的文件添加到名单
        /// </summary>
        private void SelectLocalFile(BlacklistListType listType)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "可执行程序(*.exe)|*.exe|任意文件(*.*)|*.*" // 设置文件类型过滤器
            };

            if (openFileDialog.ShowDialog() == true) // 检查用户是否点击了“确定”
            {
                AddRuleAndRefresh(listType, BlacklistMatchType.ProcessName, openFileDialog.FileName);
            }
        }

        #endregion

        #region 黑名单列表项

        /// <summary>
        /// 向黑名单列表界面中添加一项
        /// </summary>
        private void AddBlacklistItem(BlacklistRule rule)
        {
            Border border = new()
            {
                Style = (Style)FindResource("BlacklistItemBorder"),
                Tag = false
            }; // 创建Border（Tag 表示是否被选中）
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
                Text = GetDisplayText(rule)
            }; // 创建TextBlock
            stackPanel.Children.Add(textBlock); // 添加进程名称/路径

            if (rule.MatchType == BlacklistMatchType.Folder) // 如果是文件夹
            {
                TextBlock folderLabel = new() { Style = (Style)FindResource("BlacklistItemFolderTextBlock") }; // 创建TextBlock
                stackPanel.Children.Add(folderLabel); // 添加文件夹标记
            }

            Button button = new()
            {
                Style = (Style)FindResource("DeleteBlacklistItem"),
                Tag = rule // 删除按钮携带规则本身，按 ID 删除
            }; // 创建按钮
            button.Click += DeleteFromBlacklist; // 绑定删除事件
            grid.Children.Add(button); // 添加按钮

            Image image = new() { Style = (Style)FindResource("BlacklistItemDeleteIcon") }; // 创建图标
            button.Content = image; // 添加图标
            BlacklistStackPanel.Children.Add(border); // 添加到父容器StackPanel
        }

        // 从黑名单中删除一项
        private void DeleteFromBlacklist(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not BlacklistRule rule) return;
            if (button.Parent is not Grid grid || grid.Parent is not Border border) return;

            BlacklistRepository.RemoveRule(rule.ID); // 按 ID 删除
            AppStateManager.LoadSettings(); // 重新加载设置
            BlacklistStackPanel.Children.Remove(border); // 从界面移除

            if (rule.MatchType == BlacklistMatchType.ProcessName)
                existingProcessNames.Remove(rule.Target);
            else if (rule.MatchType == BlacklistMatchType.Unknown)
                SetUnknownButtonAdded(false); // 恢复"无法识别的应用"按钮
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

        #endregion

        #region 设置

        // 勾选框点击事件：直接落库并重新加载运行时设置
        private void CheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not CheckBox checkBox) return;
            if (checkBox.Name != "FullScreenDisableCheckBox") return;

            _setting.DisableOnFullScreen = checkBox.IsChecked == true;
            BlacklistRepository.UpdateSetting(_setting);
            AppStateManager.LoadSettings();
        }

        #endregion

        #region 白名单文本框

        // 用户编辑文本框时只标记"有未提交的修改"，不再每次按键都读写数据库
        private void BlacklistProcessTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_syncingWhitelistText) return;
            _whitelistDirty = true;
        }

        // 失去焦点时提交
        private void BlacklistProcessTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            CommitWhitelistText();
        }

        // 按回车提交
        private void BlacklistProcessTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                CommitWhitelistText();
                e.Handled = true;
            }
        }

        /// <summary>
        /// 把文本框中的进程名与数据库中的白名单对齐：删掉被移除的，添加新输入的
        /// </summary>
        private void CommitWhitelistText()
        {
            if (!_whitelistDirty) return;
            _whitelistDirty = false;

            // 解析文本：支持中英文分号、逗号、换行分隔；统一规范化并去重
            var typedNames = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var separators = new[] { ';', '；', ',', '，', '\r', '\n' };
            foreach (var part in BlacklistProcessTextBox.Text.Split(separators, StringSplitOptions.RemoveEmptyEntries))
            {
                string name = BlacklistRuleNormalizer.NormalizeProcessName(part);
                if (name.Length > 0 && seen.Add(name))
                    typedNames.Add(name);
            }

            var currentRules = BlacklistRepository.GetAllRules()
                .Where(r => r.ListType == BlacklistListType.Whitelist && r.MatchType == BlacklistMatchType.ProcessName)
                .ToList();

            bool changed = false;

            // 删除：数据库里有、文本框里已经没有的
            foreach (var oldRule in currentRules.Where(r => !seen.Contains(r.Target)))
            {
                BlacklistRepository.RemoveRule(oldRule.ID);
                changed = true;
            }

            // 添加：文本框里有、数据库里还没有的
            var currentNames = new HashSet<string>(currentRules.Select(r => r.Target), StringComparer.OrdinalIgnoreCase);
            foreach (var name in typedNames.Where(n => !currentNames.Contains(n)))
            {
                var result = BlacklistRepository.TryAddRule(BlacklistListType.Whitelist, BlacklistMatchType.ProcessName, name, out _);
                if (result == AddRuleResult.Added)
                    changed = true;
                else if (result == AddRuleResult.ExistsInOtherList)
                    ShowToast($"{name} 已在黑名单中，请先从黑名单移除", ToastType.Error);
            }

            if (changed)
                AppStateManager.LoadSettings();

            RefreshNamesAndWhitelistText(); // 以数据库为准回写文本框（规范化、去重后的结果）
        }

        #endregion

        #region 正在运行的程序提示框

        /// <summary>
        /// 判断进程是否有窗口（包括最小化的窗口）
        /// </summary>
        /// <param name="process">进程</param>
        private bool HasWindow(Process process)
        {
            return process.MainWindowHandle != IntPtr.Zero; // 判断是否有窗口句柄
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
                Visibility = appPath == SelectFromComputerText ? Visibility.Hidden : Visibility.Visible,
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
        /// 获取符合条件的进程列表（排除已在名单中的进程）
        /// </summary>
        /// <param name="excludedNames">已在名单中的进程名（规范化后）</param>
        private List<(string FileName, string ProcessName)> GetFilteredProcessList(HashSet<string> excludedNames)
        {
            var processList = new List<(string FileName, string ProcessName)>(); // 创建列表
            var uniqueProcessNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase); // 创建集合

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
                        string normalizedName = BlacklistRuleNormalizer.NormalizeProcessName(fullProcessName);

                        if (excludedNames.Contains(normalizedName) || !uniqueProcessNames.Add(normalizedName))
                            continue; // 如果进程已在名单中或已存在，跳过

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

            AddAppItems(SelectFromComputerText, isInBlacklist); // 添加到列表
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

            CommitWhitelistText(); // 先提交白名单文本框中未保存的输入，避免被下面的回写覆盖

            // 先在 UI 线程刷新"已存在的进程名"，再取一份快照交给后台线程使用
            RefreshNamesAndWhitelistText();
            var excludedNames = new HashSet<string>(existingProcessNames, StringComparer.OrdinalIgnoreCase);

            Task.Run(() => // 在后台线程处理进程
            {
                var processList = GetFilteredProcessList(excludedNames);
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    UpdateAppsListUI(processList, isInBlacklist);
                })); // 在UI线程更新界面
            });
        }

        #endregion

        #region 滚动条

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

        // 鼠标移入/移出显示/隐藏滚动条
        private void BlacklistStackScrollViewer_MouseEnter(object sender, MouseEventArgs e)
        {
            BlacklistScrollBar.Visibility = Visibility.Visible;
        }
        private void BlacklistStackScrollViewer_MouseLeave(object sender, MouseEventArgs e)
        {
            BlacklistScrollBar.Visibility = Visibility.Collapsed;
        }

        #endregion

        // 控件关闭释放资源
        private void BlacklistGrid_Unloaded(object sender, RoutedEventArgs e)
        {
            // 离开页面前，把还没提交的白名单修改保存下来
            try
            {
                CommitWhitelistText();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Commit whitelist on unload failed: {ex.Message}");
            }

            // 清理UI元素
            BlacklistStackPanel.Children.Clear();
            AddBlacklistAppsStackPanel.Children.Clear();
            AddWhitelistAppsStackPanel.Children.Clear();
            BlacklistStackScrollViewer.Content = null;

            // 清理缓存和变量
            existingProcessNames.Clear();
            windowSelectWindow = null;
        }
    }
}