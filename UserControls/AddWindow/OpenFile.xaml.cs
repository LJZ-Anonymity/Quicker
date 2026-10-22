using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using System.Windows.Media.Imaging;
using Quicker.Windows.MainWindows;
using Quicker.Windows.AddWindows;
using System.Windows.Controls;
using Quicker.Database.Core;
using File = System.IO.File;
using System.Windows.Media;
using IWshRuntimeLibrary;
using Quicker.Managers;
using System.Windows;
using Quicker.Models;
using WpfAnimatedGif;
using System.Text;
using System.IO;

namespace Quicker.UserControls.AddWindow
{
    /// <summary>
    /// OpenFile 控件用于处理文件、应用程序和文件夹的选择和打开
    /// </summary>
    public partial class OpenFile : UserControl, AddActionWindow.IActionEditor
    {
        #region 字段

        private Quicker.Windows.AddWindows.AddActionWindow _addWindow; // AddWindow 的引用
        private readonly ButtonManager _buttonManager = new(); // 按钮管理器
        private readonly IconManager _iconManager = new(); // 图标管理器接口
        private bool _wasConvertLnkButtonVisible = false; // 转换lnk按钮之前的可见性状态
        public FindAppsWindow findAppsWindow; // FindAppsWindow 的引用
        private ButtonDatabase _buttonDb = new(); // 按钮数据库
        private bool isLoading = true; // 是否正在加载

        #region IActionEditor

        public ActionType GetActionType() => DetermineActionType();
        public string GetLocation() => LocationTextBox.Text;
        public string GetData1() => (RunasAdministrator.IsChecked == true).ToString();
        public string GetData2() => (TryToOpenExitingWindow.IsChecked == true).ToString();
        public string GetData3() => (WindowStateComboBox.SelectedIndex != -1
            ? WindowStateComboBox.SelectedIndex : 0).ToString();
        #endregion

        #endregion

        #region 构造函数

        public OpenFile(Quicker.Windows.AddWindows.AddActionWindow addWindow)
        {
            _addWindow = addWindow; // 保存 AddWindow 的引用
            InitializeComponent();
        }

        #endregion

        #region 事件处理

        // UI 加载完成后执行
        private void OpenFile_Loaded(object sender, RoutedEventArgs e)
        {
            ExecuteChoiceAction(); // 根据上个窗口数据执行对应命令
            isLoading = false; // 加载完成
            UpdateConvertLnkButtonVisibility(); // 加载后重新计算布局，确保转换按钮留出空间
        }

        // 打开菜单
        private void OpenContextMenu(object sender, RoutedEventArgs e)
        {
            OpenFilePopup.IsOpen = true; // 打开弹出菜单
        }

        // 选择本地应用
        private void ChooseApplications(object sender, RoutedEventArgs e)
        {
            findAppsWindow = new() { Owner = _addWindow }; // 创建 FindAppsWindow 实例，并设置所有者为当前窗口
            findAppsWindow.ApplicationSelected += OnApplicationSelected; // 订阅 ApplicationSelected 事件
            findAppsWindow.ShowDialog(); // 显示为模式对话框
        }

        // 处理选中的应用
        private void OnApplicationSelected(object sender, FindAppsWindow.ApplicationSelectedEventArgs e)
        {
            findAppsWindow.ApplicationSelected -= OnApplicationSelected; // 取消事件订阅
            AppInfo selectedApp = e.SelectedApp; // 获取选中的应用信息
            if (selectedApp != null)
            {
                UpdateUIWithAppInfo(selectedApp); // 更新UI
            }
        }

        /// <summary>
        /// 更新UI
        /// </summary>
        /// <param name="appInfo">应用信息</param>
        private void UpdateUIWithAppInfo(AppInfo appInfo)
        {
            _addWindow.TitleTextBox.Text = appInfo.Name; // 设置标题
            LocationTextBox.Text = appInfo.Location; // 设置地址
            if (appInfo.Icon != null)
            {
                _addWindow.ButtonImage.Source = appInfo.Icon; // 设置图标
                _addWindow.ButtonImage.Visibility = Visibility.Visible; // 显示图标
            }
        }

        // 选择打开程序
        public void ChooseProcess(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "任意文件(*.*)|*.*|可执行程序(*.exe)|*.exe", // 设置文件类型过滤器
                DereferenceLinks = false
            };
            if (openFileDialog.ShowDialog() == true) // 检查用户是否点击了"确定"
            {
                findAppsWindow?.Close(); // 关闭 FindAppsWindow
                string fileName = Path.GetFileNameWithoutExtension(openFileDialog.FileName); // 获取文件名
                UpdateUIWithFileInfo(openFileDialog.FileName, fileName); // 更新UI
            }
        }

        /// <summary>
        /// 更新UI
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="fileName">文件名</param>
        private void UpdateUIWithFileInfo(string filePath, string fileName)
        {
            LocationTextBox.Text = filePath; // 设置地址
            if(string.IsNullOrEmpty(_addWindow.TitleTextBox.Text)) // 如果标题为空
            {
                _addWindow.TitleTextBox.Text = fileName; // 设置标题
                _addWindow.ButtonTitle.Text = fileName; // 设置按钮标题
            }
            _buttonManager.AutoEllipsisTextBlock(_addWindow.ButtonTitle, 70); // 自动省略文本
            SetIconFromPath(filePath); // 设置图标
        }

        // 选择打开文件夹
        private void ChooseFolder(object sender, RoutedEventArgs e)
        {
            using System.Windows.Forms.FolderBrowserDialog folderDialog = new(); // 创建文件夹选择对话框
            if (folderDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                ProcessSelectedFolder(folderDialog.SelectedPath); // 处理选中的文件夹
            }
        }

        /// <summary>
        /// 处理选中的文件夹
        /// </summary>
        /// <param name="folderPath">文件夹路径</param> 
        private void ProcessSelectedFolder(string folderPath)
        {
            LocationTextBox.Text = folderPath; // 设置地址
            _addWindow.TitleTextBox.Text = Path.GetFileName(folderPath); // 设置标题
            SetIconFromPath(folderPath); // 设置图标
        }

        /// <summary>
        /// 复制地址
        /// </summary>
        private void CopyLocation(object sender, RoutedEventArgs e)
        {
            string clipboardText = System.Windows.Clipboard.GetText(); // 获取剪贴板文本
            string processedLocation = _buttonManager.ProcessLocation(clipboardText); // 处理地址
            ProcessLocationFromClipboard(processedLocation); // 处理剪贴板中的地址
            OpenFilePopup.IsOpen = false; // 关闭弹出菜单
        }

        /// <summary>
        /// 重新提取文件图标
        /// </summary>
        private void ReGetFileIcon(object sender, RoutedEventArgs e)
        {
            SetIconFromPath(LocationTextBox.Text); // 设置图标
            OpenFilePopup.IsOpen = false; // 关闭弹出菜单
        }

        /// <summary>
        /// 处理剪贴板中的地址
        /// </summary>
        /// <param name="location">地址</param>
        private void ProcessLocationFromClipboard(string location)
        {
            LocationTextBox.Text = location; // 设置地址
            SetIconFromPath(location); // 设置图标
            if (string.IsNullOrWhiteSpace(_addWindow.TitleTextBox.Text))
                _addWindow.TitleTextBox.Text = Directory.Exists(location)
                    ? Path.GetFileName(location) // 如果是文件夹，则设置为文件夹名
                    : Path.GetFileNameWithoutExtension(location); // 否则设置为文件名
        }

        // 如果地址栏不为空，则启用保存按钮
        private void LocationTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _addWindow.SaveButton.IsEnabled = !string.IsNullOrWhiteSpace(LocationTextBox.Text);
            UpdateConvertLnkButtonVisibility();
        }

        // 移除控件清理资源
        private void OpenFile_Unloaded(object sender, RoutedEventArgs e)
        {
            CleanupResources();
        }

        // 调整窗口高度
        private void LocationTextBox_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (isLoading) return; // 防止在加载过程中调整大小
            double heightChange = e.NewSize.Height - e.PreviousSize.Height; // 计算文本框高度变化量
            AdjustWindowAndGridLayout(heightChange); // 调整窗口和Grid布局
        }

        /// <summary>
        /// 调整窗口高度和Grid1的上边距
        /// </summary>
        /// <param name="heightChange">高度变化量（正数表示增加，负数表示减少）</param>
        private void AdjustWindowAndGridLayout(double heightChange)
        {
            if (isLoading || _addWindow == null) return; // 防止在加载过程中调整大小
            _addWindow.Height += heightChange; // 调整窗口高度
            Thickness grid1Margin = Grid1.Margin; // 获取当前的Margin
            Thickness newMargin = new Thickness(93, grid1Margin.Top + heightChange, 0, 0); // 创建一个新的Margin对象，只修改Top属性
            Grid1.Margin = newMargin; // 将新的Margin赋给Grid1
        }

        /// <summary>
        /// 转换lnk按钮点击事件处理
        /// </summary>
        private void ConvertLnkButton_Click(object sender, RoutedEventArgs e)
        {
            string lnkPath = LocationTextBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(lnkPath))
                return;

            // 普通文件/文件夹快捷方式
            string targetPath = GetLnkTargetPath(lnkPath, out string arguments);
            if (!string.IsNullOrEmpty(targetPath))
            {
                if (IsUriProtocol(targetPath))
                {
                    // 如果目标是 URI 协议，直接拼接参数
                    LocationTextBox.Text = string.IsNullOrWhiteSpace(arguments)
                        ? targetPath
                        : $"{targetPath} {arguments.Trim()}";
                }
                else
                {
                    // 判断路径是否包含空格且没有引号，如果有则加上引号，防止命令行解析错误
                    string quotedTarget = targetPath.Contains(" ") && !targetPath.StartsWith("\"")
                        ? $"\"{targetPath}\""
                        : targetPath;

                    // 拼接启动参数
                    LocationTextBox.Text = string.IsNullOrWhiteSpace(arguments)
                        ? quotedTarget
                        : $"{quotedTarget} {arguments.Trim()}";
                }
                return;
            }

            // Internet 快捷方式(URI 协议)
            string url = GetInternetShortcutUrl(lnkPath);
            if (!string.IsNullOrEmpty(url))
            {
                LocationTextBox.Text = url;
                return;
            }

            // 兜底: 从 Arguments 里抠 URI
            string uriCommand = GetLnkUriCommand(lnkPath);
            if (!string.IsNullOrEmpty(uriCommand))
            {
                LocationTextBox.Text = uriCommand;
                return;
            }

            using var toast = new ToastManager();
            toast.Show("无法解析快捷方式目标!", ToastType.Error);
        }

        /// <summary>
        /// 获取lnk文件的目标路径及启动参数
        /// </summary>
        /// <param name="lnkFilePath">lnk文件路径</param>
        /// <param name="arguments">输出参数：启动参数</param>
        /// <returns>目标路径，如果获取失败则返回null</returns>
        private string GetLnkTargetPath(string lnkFilePath, out string arguments)
        {
            arguments = string.Empty;
            if (string.IsNullOrEmpty(lnkFilePath) || !File.Exists(lnkFilePath))
                return null;

            IWshShortcut shortcut = null;
            WshShell shell = null;
            try
            {
                shell = new WshShell();
                shortcut = (IWshShortcut)shell.CreateShortcut(lnkFilePath);
                string targetPath = shortcut.TargetPath;
                arguments = shortcut.Arguments ?? string.Empty; // 获取启动参数
                return string.IsNullOrEmpty(targetPath) ? null : targetPath;
            }
            catch
            {
                return null;
            }
            finally
            {
                if (shortcut != null) Marshal.ReleaseComObject(shortcut);
                if (shell != null) Marshal.ReleaseComObject(shell);
            }
        }

        #endregion

        #region 业务逻辑

        // 根据上个窗口数据执行对应命令
        private void ExecuteChoiceAction()
        {
            switch (_addWindow.Choice) // 根据选择执行对应命令
            {
                case 0:
                    LoadButtonInformation(); // 编辑动作，加载动作信息
                    break;
                case 1:
                    ChooseApplications(null, null); // 选择本地应用
                    break;
                case 2:
                    ChooseProcess(null, null); // 选择打开程序
                    break;
                case 3:
                    ChooseFolder(null, null); // 选择文件夹
                    break;
            }
        }

        // 编辑动作 加载动作信息
        private void LoadButtonInformation()
        {
            ButtonData buttonData = _buttonDb.GetButtonDataByID(_addWindow.ButtonID, _addWindow.TableName); // 获取按钮数据
            if (buttonData.ActionType == null || !LoadButtonInfoActionTypes.Contains(buttonData.ActionType.Value)) return;
            SetButtonTitleAndLocation(buttonData); // 设置标题和地址
            SetButtonImageSafe(buttonData.ImagePath); // 设置图标
            SetOtherOptions(buttonData); // 设置其他选项
        }

        /// <summary>
        /// 加载动作信息
        /// </summary>
        private static readonly HashSet<ActionType> LoadButtonInfoActionTypes = new()
        {
            ActionType.OpenFile,
            ActionType.OpenFiles,
            ActionType.OpenUwpApp,
            ActionType.OpenUri
        }; // 将来新增的文件相关动作类型可以在这里添加

        /// <summary>
        /// 设置标题和地址栏
        /// </summary>
        /// <param name="buttonData">按钮数据</param>
        private void SetButtonTitleAndLocation(ButtonData buttonData)
        {
            if (!string.IsNullOrWhiteSpace(buttonData.Title))
            {
                _addWindow.ButtonTitle.Visibility = Visibility.Visible; // 显示按钮标题
                _addWindow.ButtonTitle.Text = buttonData.Title; // 设置按钮标题
            }
            _addWindow.TitleTextBox.Text = buttonData.Title; // 设置标题
            LocationTextBox.Text = buttonData.Location; // 设置地址
        }

        /// <summary>
        /// 安全设置图标（带异常处理）
        /// </summary>
        /// <param name="imagePath">图片路径</param>
        private void SetButtonImageSafe(string imagePath)
        {
            if (!string.IsNullOrEmpty(imagePath))
            {
                _addWindow.SetButtonImage(imagePath);
            }
        }

        /// <summary>
        /// 设置其他选项（如复选框、下拉框、描述等）
        /// </summary>
        /// <param name="buttonData">按钮数据</param>
        private void SetOtherOptions(ButtonData buttonData)
        {
            RunasAdministrator.IsChecked = buttonData.Data1 == "True"; // 设置运行方式
            TryToOpenExitingWindow.IsChecked = buttonData.Data2 == "True"; // 设置是否尝试打开已存在的窗口
            WindowStateComboBox.SelectedIndex = int.Parse(buttonData.Data3); // 设置窗口状态
            _addWindow.DescriptionTextBox.Text = buttonData.Description; // 设置描述
            _addWindow.UpdateTooltip(); // 更新提示文本
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 根据路径设置图标
        /// </summary>
        /// <param name="path">路径</param>
        private void SetIconFromPath(string path)
        {
            string cachedIconPath = _iconManager.CheckCachedIcon(path); // 检查缓存图标
            if (!string.IsNullOrEmpty(cachedIconPath))
            {
                _addWindow.SetButtonImage(cachedIconPath); // 设置图标
            }
            else
            {
                ImageSource iconSource = IconManager.GetIcon(path); // 获取图标
                if (iconSource != null)
                {
                    _addWindow.ButtonImage.Source = iconSource; // 设置图标
                    _addWindow.ButtonImage.Visibility = Visibility.Visible; // 显示图标
                    _addWindow.iconPath = ""; // 清空旧的图标路径，保证保存时从当前图像源导出
                }
                else
                {
                    using var toast = new ToastManager(); // 创建 ToastManager 实例
                    toast.Show("图标提取失败!", ToastType.Error); // 显示错误提示
                }
            }
        }

        /// <summary>
        /// 确定动作类型
        /// </summary>
        /// <returns>动作类型</returns>
        private ActionType DetermineActionType()
        {
            string location = LocationTextBox.Text?.Trim() ?? string.Empty;

            // 0. URI 协议命令优先：weixin://、steam://、ms-settings:// 等
            if (IsUriProtocol(location))
                return ActionType.OpenUri;

            // 1. 多个文件
            if (location.Contains(";"))
                return ActionType.OpenFiles;

            // 2. 系统命令 / Shell 命名空间 / 窗口句柄
            if (IsSystemCommand(location) ||
                IsShellNamespacePath(location) ||
                location.StartsWith("hwnd:", StringComparison.OrdinalIgnoreCase))
            {
                return ActionType.OpenFile;
            }

            // 3. 普通本地路径 (提取实际路径部分，避免被启动参数干扰)
            string checkPath = location;
            if (checkPath.StartsWith("\""))
            {
                // 有引号包裹，取第一个引号对的内容
                int endQuote = checkPath.IndexOf('"', 1);
                if (endQuote > 0) checkPath = checkPath.Substring(1, endQuote - 1);
            }
            else if (checkPath.Length > 2 && checkPath[1] == ':')
            {
                // 无引号但有空格且是盘符开头，尝试截取到 .exe 为止
                int exeIndex = checkPath.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
                if (exeIndex > 0) checkPath = checkPath.Substring(0, exeIndex + 4);
                else
                {
                    int spaceIndex = checkPath.IndexOf(' ');
                    if (spaceIndex > 0) checkPath = checkPath.Substring(0, spaceIndex);
                }
            }

            const string pattern = @"^[A-Za-z]:\\(?:[^\\/:*?""<>|\r\n]+\\)*[^\\/:*?""<>|\r\n]*$";
            bool isValidPath = Regex.IsMatch(checkPath, pattern);
            return isValidPath ? ActionType.OpenFile : ActionType.OpenUwpApp;
        }

        /// <summary>
        /// 是否为系统命令（如 syscmd:controlpanel）
        /// </summary>
        private bool IsSystemCommand(string location)
        {
            return !string.IsNullOrWhiteSpace(location) &&
                   location.StartsWith("syscmd:", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 是否为 Shell 命名空间路径（如 shell:::{CLSID}，用于控制面板等系统位置）
        /// </summary>
        private bool IsShellNamespacePath(string location)
        {
            return !string.IsNullOrWhiteSpace(location) &&
                   location.StartsWith("shell:::", StringComparison.OrdinalIgnoreCase);
        }

        // 清理资源
        private void CleanupResources()
        {
            if (findAppsWindow != null)
            {
                findAppsWindow.ApplicationSelected -= OnApplicationSelected;
                findAppsWindow = null;
            }

            _buttonManager.Dispose(); // 释放按钮管理器
            _iconManager.Dispose(); // 释放图标管理器

            // 重置UI控件状态
            LocationTextBox.Text = "";
            WindowStateComboBox.SelectedIndex = -1;
            RunasAdministrator.IsChecked = false;
            TryToOpenExitingWindow.IsChecked = false;
            _wasConvertLnkButtonVisible = false; // 重置按钮可见性状态

            // 清空引用
            _addWindow = null;
            _buttonDb = null;
        }

        /// <summary>
        /// 更新转换lnk按钮的可见性
        /// </summary>
        private void UpdateConvertLnkButtonVisibility()
        {
            string text = LocationTextBox.Text?.Trim();
            bool shouldBeVisible = false;
            if (!string.IsNullOrWhiteSpace(text))
            {
                if (text.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase)) // 检查是否为lnk文件路径
                {
                    shouldBeVisible = File.Exists(text); // 只要是.lnk文件且存在就显示转换按钮
                }
            }

            // 确定新的可见性状态
            Visibility newVisibility = shouldBeVisible ? Visibility.Visible : Visibility.Collapsed;
            bool isNowVisible = newVisibility == Visibility.Visible;

            // 检测可见性变化并调整布局
            ConvertLnkButton.Visibility = newVisibility; // 设置按钮可见性

            // 加载期间不更新布局状态，防止后续计算失效
            if (isLoading) return;
            if (_wasConvertLnkButtonVisible != isNowVisible)
            {
                AdjustWindowAndGridLayout(isNowVisible ? 23 : -23); // 调整布局
                _wasConvertLnkButtonVisible = isNowVisible; // 更新状态
            }
        }

        /// <summary>
        /// 获取lnk文件的目标路径
        /// </summary>
        /// <param name="lnkFilePath">lnk文件路径</param>
        /// <returns>目标路径，如果获取失败则返回null</returns>
        private string GetLnkTargetPath(string lnkFilePath)
        {
            if (string.IsNullOrEmpty(lnkFilePath) || !File.Exists(lnkFilePath))
                return null;

            IWshShortcut shortcut = null;
            WshShell shell = null;
            try
            {
                shell = new WshShell();
                shortcut = (IWshShortcut)shell.CreateShortcut(lnkFilePath);
                string targetPath = shortcut.TargetPath;
                return string.IsNullOrEmpty(targetPath) ? null : targetPath;
            }
            catch
            {
                return null;
            }
            finally
            {
                if (shortcut != null) Marshal.ReleaseComObject(shortcut);
                if (shell != null) Marshal.ReleaseComObject(shell);
            }
        }

        /// <summary>
        /// 获取lnk文件中的URI协议命令
        /// </summary>
        /// <param name="lnkFilePath">lnk文件路径</param>
        /// <returns>URI协议命令，如果获取失败则返回null</returns>
        private string GetLnkUriCommand(string lnkFilePath)
        {
            if (string.IsNullOrEmpty(lnkFilePath) || !File.Exists(lnkFilePath))
                return null;

            IWshShortcut shortcut = null;
            WshShell shell = null;
            try
            {
                shell = new WshShell();
                shortcut = (IWshShortcut)shell.CreateShortcut(lnkFilePath);

                string arguments = shortcut.Arguments?.Trim();
                string targetPath = shortcut.TargetPath?.Trim();

                // 情况1：TargetPath 本身就是 URI 协议，如 steam://xxx
                if (!string.IsNullOrWhiteSpace(targetPath) &&
                    IsUriProtocol(targetPath))
                {
                    return string.IsNullOrWhiteSpace(arguments)
                        ? targetPath
                        : $"{targetPath} {arguments}";
                }

                // 情况2：Arguments 中包含 URI 协议命令，如 "steam://rungameid/123"
                if (!string.IsNullOrWhiteSpace(arguments))
                {
                    string uri = ExtractUriFromArguments(arguments);
                    if (!string.IsNullOrEmpty(uri))
                        return uri;
                }

                return null;
            }
            catch
            {
                return null;
            }
            finally
            {
                if (shortcut != null) Marshal.ReleaseComObject(shortcut);
                if (shell != null) Marshal.ReleaseComObject(shell);
            }
        }

        /// <summary>
        /// 判断字符串是否为URI协议
        /// </summary>
        private bool IsUriProtocol(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            return Regex.IsMatch(value.Trim(), @"^[a-zA-Z][a-zA-Z0-9+.-]*://");
        }

        /// <summary>
        /// 从参数中提取URI协议命令
        /// </summary>
        private string ExtractUriFromArguments(string arguments)
        {
            if (string.IsNullOrWhiteSpace(arguments))
                return null;

            // 匹配 xxx://... 形式的 URI，允许后面带参数
            Match match = Regex.Match(arguments, @"[a-zA-Z][a-zA-Z0-9+.-]*://[^\s""']+");
            return match.Success ? match.Value : null;
        }

        /// <summary>
        /// 读取 Internet 快捷方式中的目标 URL
        /// 说明：.lnk 中字符串以 UTF-16LE 明文存储，直接扫描字节流即可，避免 COM 互操作的不确定性
        /// </summary>
        private string GetInternetShortcutUrl(string lnkFilePath)
        {
            if (string.IsNullOrEmpty(lnkFilePath) || !File.Exists(lnkFilePath))
                return null;

            try
            {
                byte[] bytes = File.ReadAllBytes(lnkFilePath);

                // .lnk 中 URL 通常以 UTF-16LE 存储
                string text = Encoding.Unicode.GetString(bytes);
                string url = MatchUriInText(text);
                if (!string.IsNullOrEmpty(url))
                    return url;

                // 兜底：ASCII 编码
                text = Encoding.ASCII.GetString(bytes);
                return MatchUriInText(text);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 从文本中匹配 URI 协议命令(如 weixin://...、steam://...、ms-settings://...)
        /// </summary>
        private static string MatchUriInText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return null;

            // 匹配 <协议>://<非空白/非控制字符/非引号/非反斜杠> 的内容
            var match = Regex.Match(
                text,
                @"[a-zA-Z][a-zA-Z0-9+.\-]*://[^\s\u0000-\u001F""'<>\\]{1,2048}");

            return match.Success ? match.Value : null;
        }

        #endregion
    }
}