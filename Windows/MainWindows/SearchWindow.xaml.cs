using VisualTreeHelper = System.Windows.Media.VisualTreeHelper;
using System.Text.RegularExpressions;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Controls;
using System.Threading.Tasks;
using Quicker.Database.Core;
using System.ComponentModel;
using System.Windows.Media;
using System.Windows.Input;
using System.Diagnostics;
using Quicker.Managers;
using Quicker.Helpers;
using Quicker.Models;
using System.Windows;
using System.Text;

namespace Quicker.Windows.MainWindows
{
    public partial class SearchWindow : Window
    {
        public bool IsPinned
        {
            get { return (bool)GetValue(IsPinnedProperty); }
            set { SetValue(IsPinnedProperty, value); }
        }

        public static readonly DependencyProperty IsPinnedProperty =
            DependencyProperty.Register("IsPinned", typeof(bool), typeof(SearchWindow),
                new PropertyMetadata(false));

        private ActionManager actionManager = new();
        private ButtonManager buttonManager = new();
        private ButtonDatabase db2 = new();
        private bool _isLoaded = false;
        public Button targetButton;

        public SearchWindow()
        {
            InitializeComponent();
        }

        private void SearchWindow_Loaded(object sender, RoutedEventArgs e)
        {
            IsPinned = AppStateManager.SearchWindowPinned;
            Topmost = IsPinned;

            const int DesignHeight = 380; // 设计器默认高度
            SizeToContent = SizeToContent.Manual;  // 仅临时锁定
            var workArea = SystemParameters.WorkArea; // 获取屏幕工作区
            Top = (workArea.Height - DesignHeight) / 2 + workArea.Top; // 计算窗口位置
            Dispatcher.BeginInvoke(new Action(() =>
            {
                SizeToContent = SizeToContent.Height; // 恢复动态布局能力
            }), System.Windows.Threading.DispatcherPriority.Render); // 刷新布局

            _isLoaded = true;
            Activate();
        }

        // 更新按钮可见性
        private void UpdateButtonVisibility()
        {
            var buttons = new List<Button>
            {
                RunasCommandButton,
                OpenUrlCommandButton,
                SearchBingCommandButton,
                SearchBilibiliCommandButton,
                SearchGoogleCommandButton
            }; // 按钮列表

            bool isSearchBoxEmpty = string.IsNullOrWhiteSpace(SearchBox.Text) || SearchBox.Text == "开始搜索..."; // 是否搜索框为空
            buttons.ForEach(button => button.Visibility = isSearchBoxEmpty ? Visibility.Collapsed : Visibility.Visible); // 更新按钮可见性
        }

        // 当窗口失去焦点时关闭窗口。
        private void SearchWindow_Deactivated(object sender, EventArgs e)
        {
            if (!AppStateManager.SearchWindowPinned && !buttonManager.isClosing)
                Close();
        }

        // 当搜索框获得焦点时，如果内容为“开始搜索...”，则清空文本。
        private void SearchBox_GotFocus(object sender, RoutedEventArgs e)
        {
            var tb = (TextBox)sender;
            if (tb != null && tb.Text == "开始搜索...")
            {
                tb.Text = ""; //清空文本
                tb.Foreground = Brushes.Black; //恢复默认样式
            }
        }

        // 当搜索框失去焦点时，如果内容为空，则恢复为初始提示文本和样式
        private void SearchBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var tb = (TextBox)sender;
            if (tb != null && string.IsNullOrWhiteSpace(tb.Text))
            {
                tb.Text = "开始搜索..."; //恢复初始提示文本
                tb.Foreground = Brushes.LightGray; //设置提示文本样式
            }
        }

        // 搜索框内容发生变化时
        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isLoaded)
            {
                UpdateButtonVisibility();
                UpdateSearchUrl();
                AddResultButton();
            }
        }

        // 运行命令
        private void RunasCommandButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string command = SearchBox.Text.Trim('"'); // 去除引号
                var parts = SplitCommand(command); // 分割命令
                Process.Start(new ProcessStartInfo
                {
                    Arguments = parts.arguments,
                    FileName = parts.fileName,
                    UseShellExecute = true
                }); // 运行命令
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 2)
            {
                actionManager.ShowToast("找不到指定文件！", ToastType.Error);
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 193 || ex.NativeErrorCode == 1155)
            {
                actionManager.ShowToast("文件格式不支持直接执行！", ToastType.Error);
            }
            catch (ArgumentException)
            {
                actionManager.ShowToast("路径格式无效！", ToastType.Error);
            }
            catch
            {
                actionManager.ShowToast("运行失败！", ToastType.Error);
            }
        }

        /// <summary>
        /// 命令行分割辅助方法
        /// </summary>
        /// <param name="command"> 命令行 </param>
        /// <returns> 文件名和参数 </returns>
        private (string fileName, string arguments) SplitCommand(string command)
        {
            command = command.Trim(); // 去除空白字符
            var buffer = new StringBuilder(); // 缓冲区
            bool inQuotes = false; // 是否在引号内
            for (int i = 0; i < command.Length; i++)
            {
                char current = command[i];
                if (current == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (current == ' ' && !inQuotes)
                {
                    string fileName = buffer.ToString(); // 截取文件名
                    string arguments = command.Substring(i + 1).TrimStart(); // 截取参数
                    return (fileName, arguments); // 返回结果
                }
                buffer.Append(current); // 添加字符到缓冲区
            }
            return (buffer.ToString(), "");
        }

        // 打开指定网址
        private void OpenUrlCommandButton_Click(object sender, RoutedEventArgs e)
        {
            actionManager.LaunchDefaultBrowser(OpenUrlCommandButton_Text.Text); // 默认浏览器打开网址
        }

        // 用必应搜索
        private void SearchBingCommandButton_Click(object sender, RoutedEventArgs e)
        {
            actionManager.LaunchDefaultBrowser(SearchBingCommandButton_Text.Text); //必应搜索
        }

        // 用百度搜索
        private void SearchBaiduCommandButton_Click(object sender, RoutedEventArgs e)
        {
            actionManager.LaunchDefaultBrowser(SearchBilibiliCommandButton_Text.Text); //百度搜索
        }

        // 用谷歌搜索
        private void SearchGoogleCommandButton_Click(object sender, RoutedEventArgs e)
        {
            actionManager.LaunchDefaultBrowser(SearchGoogleCommandButton_Text.Text); //谷歌搜索
        }

        // 更新搜索链接
        private void UpdateSearchUrl()
        {
            string normalizedUrl = NormalizeUrlFromInput(SearchBox.Text); // 规范化输入
            OpenUrlCommandButton_Text.Text = normalizedUrl;
            OpenUrlCommandButton_Title.Text = normalizedUrl;
            SearchBingCommandButton_Text.Text = $"https://cn.bing.com/search?q={Uri.EscapeDataString(SearchBox.Text ?? "")}";
            SearchGoogleCommandButton_Text.Text = $"https://www.google.com/search?q={Uri.EscapeDataString(SearchBox.Text ?? "")}";
            SearchBilibiliCommandButton_Text.Text = $"https://search.bilibili.com/all?keyword={Uri.EscapeDataString(SearchBox.Text ?? "")}";
        }

        /// <summary>
        /// 将用户输入规范化为用于“打开网址”的字符串。
        /// 规则：
        /// 1) 已含 http:// 或 https:// 前缀时，直接返回原值（不重复添加）。
        /// 2) 已含其它自定义 scheme（如 mailto:、ftp://）时，直接返回原值。
        /// 3) 输入已包含点号（如 ChatGPT.com）认为是域名，直接返回原值。
        /// 4) 其它情况，补全为 http://{输入}.com。
        /// </summary>
        private string NormalizeUrlFromInput(string input)
        {
            string text = (input ?? string.Empty).Trim();
            // 移除所有空白字符，避免如 "ChatGPT. com" 导致无效显示
            text = Regex.Replace(text, "\\s+", "");
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            // 记录并识别协议（如 http://、https://、ftp:// 等）
            string originalScheme = null;
            int schemeIndex = text.IndexOf("://", StringComparison.Ordinal);
            if (schemeIndex >= 0)
            {
                originalScheme = text.Substring(0, schemeIndex);
                // 对于非 http/https 的自定义协议，直接返回原输入（保持应用兼容性）
                if (!originalScheme.Equals("http", StringComparison.OrdinalIgnoreCase) &&
                    !originalScheme.Equals("https", StringComparison.OrdinalIgnoreCase))
                    return text;
                // 去掉协议，转而统一后续规范化主机名
                text = text.Substring(schemeIndex + 3);
            }
            else if (text.StartsWith("//")) // 协议相对 URL，统一按 https 处理
            {
                text = text.Substring(2);
                originalScheme = "https";
            }

            text = text.TrimStart('/'); // 去除多余的前导斜杠
            int hostEnd = text.IndexOfAny(new[] { '/', '?', '#' }); // 仅保留主机名部分（忽略路径、查询、片段）
            if (hostEnd >= 0)
                text = text.Substring(0, hostEnd);

            text = text.Trim('.'); // 去掉首尾点号
            if (string.IsNullOrEmpty(text)) // 若已经空了，直接返回空，避免拼出无效的 http://
                return string.Empty;

            if (!DomainHelper.EndsWithKnownTld(text))
                text += ".com";

            // 选择协议：保留用户已给出的 http/https，否则默认使用 https
            string schemeToUse = string.IsNullOrEmpty(originalScheme) ? "https" : originalScheme.ToLowerInvariant();
            string finalUrl = schemeToUse + "://" + text;
            return finalUrl;
        }

        // 置顶切换
        private void PinToggle_Click(object sender, RoutedEventArgs e)
        {
            IsPinned = !IsPinned;
            AppStateManager.SearchWindowPinned = IsPinned;
            Topmost = IsPinned;
        }

        // 添加搜索结果按钮
        private void AddResultButton()
        {
            ClearSearchResultButtons(); // 清除旧按钮
            if (!string.IsNullOrWhiteSpace(SearchBox.Text)) // 空搜索不添加按钮
            {
                var dataList = db2.GetButtonbyName(SearchBox.Text); // 获取按钮数据列表
                for (int i = 0; i < dataList.buttonDataList.Count; i++)
                {
                    var btn = CreateSearchResultButton(dataList.buttonDataList[i]); // 创建搜索结果按钮
                    btn.Name = $"{dataList.tableNames[i]}{dataList.buttonDataList[i].ButtonID}"; // 使用对应的表名和按钮ID作为按钮的名称
                    btn.Tag = new Tuple<string, ButtonData>(dataList.tableNames[i], dataList.buttonDataList[i]); // 存储表名和按钮数据
                    stackPanel.Children.Insert(0, btn); // 从头开始添加按钮到面板
                }
            }
        }

        // 清除所有搜索结果按钮
        private void ClearSearchResultButtons()
        {
            stackPanel.Children.OfType<Button>()
                .Where(btn => btn.Tag?.ToString() != "DefaultButton")
                .ToList()
                .ForEach(btn =>
                {
                    stackPanel.Children.Remove(btn);
                    btn.Click -= Button_Click; // 解绑 Click 事件
                    btn.MouseRightButtonDown -= Button_MouseRightButtonDown; // 解绑右键菜单事件
                });
        }

        /// <summary>
        /// 创建搜索结果按钮
        /// </summary>
        /// <param name="data"> 按钮数据 </param>
        /// <returns> 搜索结果按钮 </returns>
        private Button CreateSearchResultButton(ButtonData data)
        {
            Grid grid = new();
            grid.Children.Add(CreateImage(data.ImagePath)); // 添加图像
            grid.Children.Add(CreateSearchResultTextBlock(data)); // 添加标题文本块
            grid.Children.Add(CreateTagTextBlock()); // 添加标签文本块
            Button btn = new()
            {
                Style = (Style)FindResource("SearchButtonStyle"),
                Content = grid,
                Tag = data
            }; // 创建按钮
            btn.Click += Button_Click; // 绑定 Click 事件
            btn.MouseRightButtonDown += Button_MouseRightButtonDown; // 绑定右键菜单事件
            return btn; // 返回按钮
        }

        /// <summary>
        /// 创建图像
        /// </summary>
        /// <param name="imagePath"> 图像路径 </param>
        /// <returns> 图像 </returns>
        private Image CreateImage(string imagePath)
        {
            return new Image
            {
                Source = new BitmapImage(new Uri(imagePath, UriKind.Absolute)),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(10),
                Height = 32,
                Width = 32
            };
        }

        /// <summary>
        /// 创建标签文本块
        /// </summary>
        /// <returns> 标签文本块 </returns>
        private TextBlock CreateTagTextBlock()
        {
            return new TextBlock
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(51, 0, 0, 9),
                Foreground = Brushes.LightGray,
                Text = "(Quicker动作)",
                FontSize = 10
            };
        }

        /// <summary>
        /// 创建搜索结果文本块
        /// </summary>
        /// <param name="data"> 按钮数据 </param>
        /// <returns> 搜索结果文本块 </returns>
        private TextBlock CreateSearchResultTextBlock(ButtonData data)
        {
            TextBlock textBlock = new()
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(51, 8, 0, 0),
                FontSize = 16
            };

            // 应用高亮逻辑
            TextBlockHelper.SetHighlight(textBlock, new HighlightTextData
            {
                Text = data.Title,
                Keyword = SearchBox.Text.Trim()
            });
            return textBlock;
        }

        // 点击按钮执行动作
        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            Button btn = (Button)sender;
            var tag = (Tuple<string, ButtonData>)btn.Tag; // 提取表名和按钮数据
            string tableName = tag.Item1; // 表名
            ButtonData data = tag.Item2; // 按钮数据
            await actionManager.DoActionAsync(data, tableName); // 执行动作并传递表名
        }

        // 右键菜单
        private void Button_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            targetButton = (Button)sender;
            var tag = (Tuple<string, ButtonData>)targetButton.Tag; // 提取表名和按钮数据
            var data = (ButtonData)tag.Item2; // 按钮数据
            string tableName = tag.Item1; // 表名
            buttonManager.OpenMenu(sender, "OperationMenu", this, tableName); // 使用表名
        }

        // 移除按钮
        public void DeleteButton()
        {
            if (targetButton != null)
            {
                stackPanel.Children.Remove(targetButton);
                targetButton.Click -= Button_Click; // 解绑 Click 事件
                targetButton.MouseRightButtonDown -= Button_MouseRightButtonDown; // 解绑右键菜单事件
                targetButton = null;
            }
        }

        // 滚动条变化
        private void ScrollBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            ScrollViewer.ScrollToVerticalOffset(ScrollBar.Value); // 滚动到指定位置
        }
        private void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            ScrollBar.Maximum = ScrollViewer.ExtentHeight - ScrollViewer.ViewportHeight; // 设置滚动条最大值
            ScrollBar.ViewportSize = ScrollViewer.ViewportHeight; // 设置滚动条视口大小
            ScrollBar.Value = ScrollViewer.VerticalOffset; // 设置滚动条值
        }

        // 滚动条可见性变化
        private void Grid_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            ScrollBar.Visibility = stackPanel.Children.Count > 8 ? Visibility.Visible : Visibility.Collapsed; // 设置滚动条可见性
        }
        private void Grid_MouseLeave(object sender, MouseEventArgs e)
        {
            ScrollBar.Visibility = Visibility.Collapsed; // 设置滚动条可见性
        }

        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !string.IsNullOrEmpty(SearchBox.Text)) 
            {
                SearchBingCommandButton_Click(null, null); // 执行搜索
            }
        }

        // 清理资源
        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            ClearSearchResultButtons(); // 解绑并清除按钮
            stackPanel.Children.Clear(); // 清除所有子元素
            targetButton = null; // 置空目标按钮
        }
    }
}