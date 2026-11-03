using Quicker.Windows.MainWindows.MainWindow;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using Quicker.Windows.MainWindows;
using System.Windows.Controls;
using System.Threading.Tasks;
using Quicker.Database.Core;
using Quicker.Windows.Menus;
using System.Windows.Media;
using System.Windows.Input;
using Quicker.Windows;
using System.Windows;
using Quicker.Models;
using WpfAnimatedGif;
using Quicker.Extend;
using System.IO;

namespace Quicker.Managers
{
    public class ButtonManager
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);
        private const int VK_LCONTROL = 0xA2;
        private const int VK_RCONTROL = 0xA3;

        public bool isClosing = false, isDragging, shouldHideTooltip; // 窗口关闭和拖拽状态、隐藏提示标志
        private readonly IconManager iconManager = new(); // 图标管理器
        private readonly ButtonDatabase db2 = new(); // 按钮数据库
        private Point initialMousePosition; // 鼠标初始位置
        private string SourceTableName; // 数据库表名
        private Button SourceButton; // 源按钮

        public ButtonManager()
        {
            var Convention = SettingDatabase.GetAllConventions().FirstOrDefault(); // 获取所有约定
            shouldHideTooltip = Convention.HideTooltip; // 获取隐藏提示标志
        }

        // 设置按钮拖拽时的效果
        public void Button_PreviewDragOver(object sender, DragEventArgs e)
        {
            e.Handled = true; // 标记事件已处理
            Button button = sender as Button; // 获取按钮
            bool isCtrlPressed = IsCtrlPressed(); // Ctrl键是否按下
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = button.Tag == null ? DragDropEffects.Copy : DragDropEffects.None;
            }
            else if (e.Data.GetDataPresent(typeof(ButtonData)))
            {
                e.Effects = isCtrlPressed ? DragDropEffects.Copy : DragDropEffects.Move;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }

        /// <summary>
        /// 处理文件拖拽到按钮上
        /// </summary>
        /// <param name="sender">目标按钮</param>
        /// <param name="e">拖拽事件参数</param>
        /// <param name="isMainWindow">是否为主窗口</param>
        public void Button_Drop(object sender, DragEventArgs e, bool isMainWindow, string tableName)
        {
            Button TargetButton = sender as Button; // 获取目标按钮
            if (TargetButton == SourceButton) return; // 如果目标按钮和源按钮相同，直接返回
            if (e.Data.GetDataPresent(typeof(ButtonData))) // 如果拖拽的是按钮
                ProcessButtonDrop(TargetButton, isMainWindow, SourceTableName, tableName); // 处理按钮拖拽
            else if (e.Data.GetDataPresent(DataFormats.FileDrop)) // 如果拖拽的是文件
                ProcessFileDrop(e, TargetButton, isMainWindow, tableName); // 处理文件拖拽
            else if (e.Data.GetDataPresent(DataFormats.Text)) // 如果拖拽的是文本（可能是 URL） 
                ProcessTextDrop(e, TargetButton, isMainWindow, tableName); // 处理文本拖拽
        }

        /// <summary>
        /// 处理文本拖拽
        /// </summary>
        /// <param name="e">拖拽事件参数</param>
        /// <param name="targetButton">目标按钮</param>
        /// <param name="isMainWindow">是否为主窗口</param>
        /// <param name="tableName">数据库表名</param>
        private void ProcessTextDrop(DragEventArgs e, Button targetButton, bool isMainWindow, string tableName)
        {
            string text = (string)e.Data.GetData(DataFormats.Text).ToString();
            if (Uri.TryCreate(text, UriKind.Absolute, out Uri url))
            {
                ProcessUrlDrop(url.ToString(), int.Parse(targetButton.Name.Replace(tableName, "")), tableName, isMainWindow);
            }
        }

        /// <summary>
        /// 处理按钮拖拽到其他按钮上
        /// </summary>
        /// <param name="targetButton"> 目标按钮 </param>
        /// <param name="isMainWindow"> 是否为主窗口 </param>
        /// <param name="tableName1"> 原表名 </param>
        /// <param name="tableName2"> 目标表名 </param>
        private void ProcessButtonDrop(Button targetButton, bool isMainWindow, string tableName1, string tableName2)
        {
            if (SourceButton == null) return; // 如果源按钮为空，直接返回
            int buttonID1 = int.Parse(SourceButton.Name.Replace(tableName1, "")); // 获取源按钮ID
            int buttonID2 = int.Parse(targetButton.Name.Replace(tableName2, "")); // 获取目标按钮ID
            db2.ExchangeButtonID(buttonID1, buttonID2, tableName1, tableName2); // 交换按钮编号
            var sourceData = SourceButton.Tag as ButtonData; // 获取源按钮数据
            var targetData = targetButton.Tag as ButtonData; // 获取目标按钮数据

            if(!(Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))) // 如果没有按住 Ctrl 键
                RefreshButtonDisplay(SourceButton, targetData, 60, isMainWindow); // 更新 sourceButton 的内容
            RefreshButtonDisplay(targetButton, sourceData, 60, isMainWindow); // 更新 targetButton 的内容
            SourceButton = null; // 清空源按钮

            AppStateManager.NotifyActionPageChanged(tableName1);
            if (tableName1 != tableName2) AppStateManager.NotifyActionPageChanged(tableName2);
        }

        /// <summary>
        /// 处理文件拖拽
        /// </summary>
        /// <param name="e"> 拖拽事件参数 </param>
        /// <param name="TargetButton"> 目标按钮 </param>
        /// <param name="isMainWindow"> 是否为主窗口 </param>
        private void ProcessFileDrop(DragEventArgs e, Button TargetButton, bool isMainWindow, string tableName)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop); // 获取文件路径
            if (files.Length <= 0) return; // 如果没有文件，直接返回

            if (files.Length == 1) // 如果只有一个文件
                ProcessSingleFileDrop(TargetButton, files[0], isMainWindow, tableName); // 处理单个文件拖拽
            else // 如果有多于一个文件
                ProcessMultipleFileDrop(TargetButton, files, isMainWindow, tableName); // 处理多个文件拖拽

            UpdateButtonDisplay(TargetButton, tableName, isMainWindow); // 更新按钮显示 
        }

        /// <summary>
        /// 更新按钮显示
        /// </summary>
        /// <param name="button"> 目标按钮 </param>
        /// <param name="tableName"> 数据库表名 </param>
        /// <param name="isMainWindow"> 是否为主窗口 </param>
        private void UpdateButtonDisplay(Button button, string tableName, bool isMainWindow)
        {
            var buttonData = db2.GetButtonDataByID(int.Parse(button.Name.Replace(tableName, "")), tableName); // 获取按钮数据
            RefreshButtonDisplay(button, buttonData, 60, isMainWindow); // 刷新按钮显示
        }

        /// <summary>
        /// 处理单个文件拖拽
        /// </summary>
        /// <param name="button"> 目标按钮 </param>
        /// <param name="filePath"> 文件路径 </param>
        private void ProcessSingleFileDrop(Button button, string filePath, bool isMainWindow, string tableName)
        {
            if (IsImaege(filePath)) // 如果是图片文件
            {
                ProcessImageDrop(filePath, int.Parse(button.Name.Replace(tableName,"")), tableName); // 处理图片拖拽
                return; // 直接返回
            }
            else if (Path.GetExtension(filePath).ToLower() == ".url") // 判断是否为 .url 文件
            {
                ProcessUrlShortcutDrop(filePath, int.Parse(button.Name.Replace(tableName,"")), tableName);
                return;
            }

            ImageSource iconSource = IconManager.GetIcon(filePath); // 获取图标
            string iconPath = ""; // 默认图标路径
            if (iconSource != null) // 如果图标存在
            {
                iconPath = iconManager.CheckCachedIcon(filePath); // 检查已经保存的图标
                if (string.IsNullOrEmpty(iconPath)) // 如果不存在保存的图标
                    iconPath = iconManager.SaveIconToFile(iconSource); // 保存图标到文件
            }

            string fileName = Path.GetFileNameWithoutExtension(filePath); // 获取文件名
            ButtonData buttonData = new ButtonData
            {
                ButtonID = int.Parse(button.Name.Replace(tableName,"")), // 获取按钮ID
                Title = fileName, // 设置按钮名称
                Location = filePath, // 设置文件路径
                ImagePath = iconPath, // 设置图标路径
                Data1 = false.ToString(), // 是否使用管理员身份运行
                Data2 = true.ToString(), // 尝试打开已存在的窗口
                Data3 = 0.ToString(), // 设置窗口状态
                Description = $"打开文件: {fileName}", // 设置用途
                CreateTime = DateTime.Now,
                ActionType = ActionType.OpenFile, // 设置动作类型
            }; // 设置按钮数据
            RefreshButtonDisplay(button, buttonData, 60, isMainWindow); // 刷新按钮
            db2.UpdateAction(buttonData, tableName); // 添加按钮数据到数据库
        }

        /// <summary>
        /// 判断是否为图片文件
        /// </summary>
        /// <param name="filePath"> 文件路径 </param>
        /// <returns> 是否为图片文件 </returns>
        private bool IsImaege(string filePath)
        {
            string extension = Path.GetExtension(filePath); // 获取文件地址
            return extension.ToLower() == ".jpg" || extension.ToLower() == ".jpeg" || extension.ToLower() == ".png" || extension.ToLower() == ".gif"; // 判断是否为图片文件
        }

        /// <summary>
        /// 处理图片拖拽
        /// </summary>
        /// <param name="filePath"> 文件路径 </param>
        /// <param name="buttonID"> 按钮ID </param>
        /// <param name="tableName"> 表名 </param>
        private void ProcessImageDrop(string filePath, int buttonID, string tableName)
        {
            BitmapImage bitmap = new BitmapImage(new Uri(filePath)); // 创建 BitmapImage 对象
            string iconPath = ""; // 默认图标路径
            if (bitmap != null) // 如果图标存在
            {
                iconPath = iconManager.CheckCachedIcon(filePath); // 检查已经保存的图标
                if (string.IsNullOrEmpty(iconPath)) // 如果不存在保存的图标
                    iconPath = iconManager.SaveIconToFile(bitmap); // 保存图标到文件
            }

            string fileName = Path.GetFileNameWithoutExtension(filePath); // 获取文件名
            ButtonData buttonData = new ButtonData
            {
                ButtonID = buttonID,
                Title = fileName,
                Location = filePath,
                ImagePath = iconPath,
                Data1 = false.ToString(), // 是否使用管理员身份运行
                Data2 = true.ToString(), // 尝试打开已存在的窗口
                Data3 = 0.ToString(),
                Description = $"打开图片: {fileName}",
                CreateTime = DateTime.Now,
                ActionType = ActionType.OpenFile,
            };
            db2.UpdateAction(buttonData, tableName); // 添加按钮数据到数据库
        }

        /// <summary>
        /// 处理 Internet 快捷方式文件拖拽
        /// </summary>
        /// <param name="filePath"> 文件路径 </param>
        /// <param name="buttonID"> 按钮ID </param>
        /// <param name="tableName"> 表名 </param>
        private void ProcessUrlShortcutDrop(string filePath, int buttonID, string tableName)
        {
            string url = File.ReadAllText(filePath); // 读取 .url 文件内容以获取实际的 URL
            string[] lines = File.ReadAllLines(filePath);
            foreach (string line in lines)
            {
                if (line.StartsWith("URL="))
                {
                    url = line.Substring("URL=".Length).Trim();
                    break;
                }
            }
            ProcessUrlDrop(url, buttonID, tableName, true); // 调用处理 URL 的方法
        }

        /// <summary>
        /// 处理多个文件拖拽
        /// </summary>
        /// <param name="button"> 目标按钮 </param>
        /// <param name="filePaths"> 文件路径 </param>
        /// <param name="isMainWindow"> 是否为主窗口 </param>
        /// <param name="tableName"> 数据库表名 </param>
        private void ProcessMultipleFileDrop(Button button, string[] filePaths, bool isMainWindow, string tableName)
        {
            string filePath = string.Join(";", filePaths); // 用" ; "分隔文件路径
            ImageSource iconSource = IconManager.GetIcon(filePaths[0]); // 获取图标
            string iconPath = ""; // 默认图标路径
            if (iconSource != null) // 如果图标存在
            {
                iconPath = iconManager.CheckCachedIcon(filePaths[0]); // 检查已经保存的图标
                if (string.IsNullOrEmpty(iconPath)) // 如果不存在保存的图标
                    iconPath = iconManager.SaveIconToFile(iconSource); // 保存图标到文件
            }

            string fileName = Path.GetFileNameWithoutExtension(filePaths[0]); // 获取文件名
            ButtonData buttonData = new ButtonData
            {
                ButtonID = int.Parse(button.Name.Replace(tableName,"")), // 获取按钮ID
                Title = $"{fileName}等{filePaths.Length}个文件(夹)", // 设置按钮名称
                Location = filePath, // 设置文件路径
                ImagePath = iconPath, // 设置图标路径
                Data1 = false.ToString(), // 是否使用管理员身份运行
                Data2 = true.ToString(), // 尝试打开已存在的窗口
                Data3 = 0.ToString(), // 设置窗口状态
                Description = $"打开以{fileName}为首的多个文件:", // 设置用途
                CreateTime = DateTime.Now,
                ActionType = ActionType.OpenFiles, // 设置动作类型
            }; // 设置按钮数据
            RefreshButtonDisplay(button, buttonData, 60, isMainWindow); // 刷新按钮
            db2.UpdateAction(buttonData, tableName); // 添加按钮数据到数据库
        }

        /// <summary>
        /// 处理 URL 拖拽
        /// </summary>
        /// <param name="url"> URL 地址 </param>
        /// <param name="buttonID"> 按钮ID </param>
        /// <param name="tableName"> 表名 </param>
        /// <param name="isMainWindow"> 是否为主窗口 </param>
        private async Task ProcessUrlDrop(string url, int buttonID, string tableName, bool isMainWindow)
        {
            ImageSource iconSource = await iconManager.GetWebsiteIconAsync(url); // 获取图标
            string iconPath = ""; // 默认图标路径
            if (iconSource != null) // 如果图标存在
            {
                iconPath = iconManager.CheckCachedIcon(url); // 检查已经保存的图标
                if (string.IsNullOrEmpty(iconPath)) // 如果不存在保存的图标
                    iconPath = iconManager.SaveIconToFile(iconSource); // 保存图标到文件
            }

            ButtonData buttonData = new ButtonData
            {
                ButtonID = buttonID,
                Title = GetWebsiteNameFromUrl(url),
                Location = url,
                ImagePath = iconPath,
                Data3 = 0.ToString(),
                Description = $"打开网页: {url}",
                CreateTime = DateTime.Now,
                ActionType = ActionType.OpenWebsite,
            }; // 设置按钮数据
            db2.UpdateAction(buttonData, tableName); // 添加按钮数据到数据库
            AppStateManager.NotifyActionPageChanged(tableName);
        }

        /// <summary>
        /// 从URL中获取网站名称
        /// </summary>
        /// <param name="url">URL地址</param>
        /// <returns>网站名称</returns>
        public string GetWebsiteNameFromUrl(string url)
        {
            try
            {
                Uri uri = new Uri(url);
                return uri.Host;
            }
            catch
            {
                return url;
            }
        }

        /// <summary>
        /// 刷新按钮显示内容
        /// </summary>
        /// <param name="button"> 目标按钮 </param>
        /// <param name="buttonInformation"> 按钮数据 </param>
        /// <param name="maxWidth"> 最大宽度 </param>
        public void RefreshButtonDisplay(Button button, ButtonData buttonInformation, double maxWidth, bool isMainWindow = true)
        {
            if (buttonInformation == null || buttonInformation.Location == null)
            {
                ResetButtonDisplay(button); // 重置按钮显示
                return; // 直接返回
            }

            button.Tag = buttonInformation; // 设置按钮标签
            var grid = CreateButtonGrid(buttonInformation, maxWidth, isMainWindow); // 创建按钮网格
            button.Content = grid; // 设置按钮内容
            LoadActionTooltip(button, buttonInformation); // 加载动作提示
        }

        /// <summary>
        /// 重置按钮显示
        /// </summary>
        /// <param name="button"> 目标按钮 </param>
        private void ResetButtonDisplay(Button button)
        {
            button.Content = null; // 清空按钮内容
            button.ToolTip = null; // 清空提示文本
            button.Tag = null; // 清空标签
        }

        /// <summary>
        /// 创建按钮网格
        /// </summary>
        /// <param name="buttonInformation"> 按钮数据 </param>
        /// <param name="maxWidth"> 最大宽度 </param>
        /// <param name="isMainWindow"> 是否为主窗口 </param>
        /// <returns> 按钮网格 </returns>
        private Grid CreateButtonGrid(ButtonData buttonInformation, double maxWidth, bool isMainWindow)
        {
            Grid grid = new(); // 创建网格
            if (!string.IsNullOrEmpty(buttonInformation.ImagePath)) // 如果图像路径不为空
            {
                AddImageToGrid(grid, buttonInformation, isMainWindow); // 添加图像到网格
            }

            if (!string.IsNullOrEmpty(buttonInformation.Title)) // 如果标题不为空
            {
                AddTitleToGrid(grid, buttonInformation, maxWidth, isMainWindow); // 添加标题到网格
            }

            return grid; // 返回网格
        }

        /// <summary>
        /// 添加图像到网格，支持GIF动图
        /// </summary>
        /// <param name="grid"> 目标网格 </param>
        /// <param name="buttonInformation"> 按钮数据 </param>
        /// <param name="isMainWindow"> 是否为主窗口 </param>
        private void AddImageToGrid(Grid grid, ButtonData buttonInformation, bool isMainWindow)
        {
            try
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 添加行定义
                Image image = LoadActionIcon(buttonInformation, isMainWindow); // 加载动作图标

                // 检查是否为GIF图片，若是则用WpfAnimatedGif设置动画源
                string ext = Path.GetExtension(buttonInformation.ImagePath ?? "").ToLower();
                if (ext == ".gif" && !string.IsNullOrEmpty(buttonInformation.ImagePath))
                {
                    var bitmap = new BitmapImage(new Uri(buttonInformation.ImagePath));
                    WpfAnimatedGif.ImageBehavior.SetAnimatedSource(image, bitmap);
                }

                grid.Children.Add(image); // 添加图像到网格
                Grid.SetRow(image, 0); // 设置图像行
            }
            catch
            {
                using var toast = new ToastManager(); // 消息提醒管理器
                toast.Show($"图标加载失败：动作{buttonInformation.Title}的图标被移动或删除", ToastType.Error); // 弹出消息提醒
            }
        }

        /// <summary>
        /// 添加标题到网格
        /// </summary>
        /// <param name="grid"> 目标网格 </param>
        /// <param name="buttonInformation"> 按钮数据 </param>
        /// <param name="maxWidth"> 最大宽度 </param>
        private void AddTitleToGrid(Grid grid, ButtonData buttonInformation, double maxWidth, bool isMainWindow)
        {
            var appearance = SettingDatabase.GetAppearanceSettings().FirstOrDefault();
            double buttonSize = appearance.ButtonSize; // 获取按钮大小
            double borderWidth = appearance.BorderWidth; // 获取边框宽度
            maxWidth = isMainWindow ? buttonSize - borderWidth * 2 : maxWidth; // 计算最大可用宽度

            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 添加行定义
            TextBlock textBlock = LoadActionTitle(buttonInformation, maxWidth, isMainWindow); // 创建标题TextBlock
            textBlock.Visibility = appearance.HideActionNameAfterIcon ? Visibility.Collapsed : Visibility.Visible; // 根据设置控制可见性
            if (appearance.AutoHideTitleBar)
            {
                ShrinkTextBlockFontToFit(textBlock, maxWidth); // 自动缩小字号以适应宽度
            }
            else
            {
                AutoEllipsisTextBlock(textBlock, (int)maxWidth); // 超出宽度时自动省略号
            }
            grid.Children.Add(textBlock); // 添加TextBlock到网格
            Grid.SetRow(textBlock, 1); // 设置TextBlock所在行
        }

        /// <summary>
        /// 加载动作图标
        /// </summary>
        private Image LoadActionIcon(ButtonData buttonInformation, bool isMainWindow)
        {
            var appearance = SettingDatabase.GetAppearanceSettings().FirstOrDefault();
            double buttonSize = isMainWindow ? appearance.ButtonSize : 65;
            double imageSize = buttonSize / 2.0;
            var image = new Image
            {
                Width = imageSize,
                Height = imageSize,
                Stretch = Stretch.Uniform,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Source = LoadIconSource(buttonInformation.ImagePath, imageSize)
            };

            if (appearance.ShowActionIconShadow == true)
            {
                image.Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 8,
                    ShadowDepth = 2,
                    Opacity = 0.5
                };
            }

            return image;
        }

        /// <summary>
        /// 以显示尺寸的 2 倍（给高 DPI 留余量）解码图标，避免按原图（512/256）解码造成大对象堆膨胀
        /// </summary>
        private static BitmapImage LoadIconSource(string path, double displaySize)
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.DecodePixelWidth = Math.Max(16, (int)(displaySize * 2));
            bmp.UriSource = new Uri(path);
            bmp.EndInit();
            bmp.Freeze(); // 跨线程共享 + 减少 GC 压力
            return bmp;
        }

        /// <summary>
        /// 重新读取行为相关设置（当前只有 HideTooltip），供常驻窗口在显示时刷新
        /// </summary>
        public void ReloadConventions()
        {
            var convention = SettingDatabase.GetAllConventions().FirstOrDefault();
            if (convention != null)
            {
                shouldHideTooltip = convention.HideTooltip;
            }
        }

        /// <summary>
        /// 加载动作名称
        /// </summary>
        /// <param name="buttonInformation"> 按钮数据 </param>
        /// <param name="maxWidth"> 最大宽度 </param>
        private TextBlock LoadActionTitle(ButtonData buttonInformation, double maxWidth, bool isMainWindow)
        {
            var appearance = SettingDatabase.GetAppearanceSettings().FirstOrDefault();
            var textBlock = new TextBlock
            {
                HorizontalAlignment = HorizontalAlignment.Stretch, // 水平拉伸填满
                TextAlignment = TextAlignment.Center, // 文本居中
                Text = buttonInformation.Title // 设置文本内容
            }; // 创建TextBlock用于显示按钮标题

            textBlock.FontSize = appearance.FontSize; // 滑块值

            return textBlock; // 返回TextBlock对象，由AddTitleToGrid后续决定是否自动缩小字号
        }

        /// <summary>
        /// 加载动作提示
        /// </summary>
        /// <param name="button"> 目标按钮 </param>
        /// <param name="buttonInformation"> 按钮数据 </param>
        private void LoadActionTooltip(Button button, ButtonData buttonInformation)
        {
            if (!shouldHideTooltip)
            {
                string toolTipText = null; // 提示文本
                if (!string.IsNullOrWhiteSpace(buttonInformation.Title) || !string.IsNullOrWhiteSpace(buttonInformation.Description))
                {
                    string name = !string.IsNullOrWhiteSpace(buttonInformation.Title) ? buttonInformation.Title : null; // 获取按钮名称
                    string usage = !string.IsNullOrWhiteSpace(buttonInformation.Description) ? buttonInformation.Description : null; // 获取按钮用途
                    toolTipText = (name + "\n" + usage).Trim('\n'); // 设置按钮提示文本
                } // 如果按钮名称或用途不为空
                button.ToolTip = string.IsNullOrEmpty(toolTipText) ? null : toolTipText; // 设置按钮提示文本
            }
        }

        /// <summary>
        /// 加载动作使用次数
        /// </summary>
        /// <param name="data"> 按钮数据 </param>
        public void LoadActionUsedTimes(Button button, ButtonData data)
        {
            button.Content = null; // 清空按钮内容
            TextBlock textBlock = new()
            {
                FontSize = 11, // 设置字体大小
                TextAlignment = TextAlignment.Center, // 设置文本对齐方式
                Text = "使用次数" + "\n" + data.UsedTimes.ToString() // 设置文本
            }; // 创建文本块对象
            button.Content = textBlock; // 设置按钮内容
        }

        // 鼠标左键按下时记录初始位置
        public void Button_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Button button = sender as Button; // 获取目标按钮
            initialMousePosition = e.GetPosition(button); // 记录初始位置
            SourceButton = button; // 记录源按钮
            isDragging = false; // 初始化拖拽状态
        }

        /// <summary>
        /// 鼠标移动时检查是否满足拖拽条件
        /// </summary>
        /// <param name="sender"> 目标按钮 </param>
        /// <param name="e"> 鼠标事件参数 </param>
        /// <param name="isMainButton"> 是否为主按钮 </param>
        public void Button_PreviewMouseMove(object sender, MouseEventArgs e, bool isMainButton, string sourceTableName = null)
        {
            SourceTableName = sourceTableName; // 记录源表名
            if (sender is Button button && e.LeftButton == MouseButtonState.Pressed)
            {
                Point currentPosition = e.GetPosition(button); // 获取当前位置
                double deltaX = currentPosition.X - initialMousePosition.X; // 计算 X 轴位移
                double deltaY = currentPosition.Y - initialMousePosition.Y; // 计算 Y 轴位移
                double distance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY); // 计算移动距离
                if (distance > 10 && !isDragging) // 如果移动距离超过 10 像素，则视为拖拽开始
                {
                    isDragging = true; // 设置拖拽状态
                    if(isMainButton)
                    {
                        if (button.Tag is ButtonData data) // 如果是主按钮
                            DragDrop.DoDragDrop(button, data, DragDropEffects.Copy | DragDropEffects.Move | DragDropEffects.None); // 开始拖拽操作
                    }
                    else
                    {
                        string buttonName = button.Name; // 获取按钮名称
                        DataObject data = new DataObject();// 创建数据对象
                        data.SetData("ButtonData", buttonName); // 传递 Button 的 Name
                        DragDrop.DoDragDrop(button, data, DragDropEffects.Move); // 开始拖拽操作
                    }
                }
            }
        }

        // 鼠标左键释放时重置状态
        public void Button_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            isDragging = false; // 重置拖拽状态
        }

        /// <summary>
        /// 动态调整TextBlock的字体大小以适应最大宽度
        /// </summary>
        /// <param name="textBlock">指定的TextBlock</param>
        /// <param name="maxWidth">最大宽度</param>
        public void AutoEllipsisTextBlock(TextBlock textBlock, int maxWidth)
        {
            if (string.IsNullOrEmpty(textBlock.Text)) return; // 如果文本为空，直接返回
            textBlock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity)); // 测量文本块的大小
            double textWidth = textBlock.DesiredSize.Width; // 获取文本宽度           
            if (textWidth <= maxWidth) return; // 如果文本宽度小于等于最大宽度，直接返回

            string originalText = textBlock.Text; // 获取原始文本
            string ellipsis = "..."; // 设置省略号
            string truncatedText = originalText; // 初始化截断文本
            while (true) // 循环直到文本宽度小于等于最大宽度
            {
                truncatedText = truncatedText.Substring(0, truncatedText.Length - 1); // 截断文本
                string newText = truncatedText + ellipsis; // 添加省略号
                textBlock.Text = newText; // 更新 TextBlock 的文本
                textBlock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity)); // 测量文本块的大小
                double newWidth = textBlock.DesiredSize.Width; // 获取新文本宽度
                if (newWidth <= maxWidth) break; // 如果新文本宽度小于等于最大宽度，退出循环
            }
            textBlock.Text = truncatedText + ellipsis; // 更新 TextBlock 的文本
        }

        /// <summary>
        /// 缩小TextBlock字号直到文本宽度小于等于按钮大小 - 按钮边框 * 2
        /// </summary>
        /// <param name="textBlock"> 要调整的TextBlock </param>
        /// <param name="buttonSize"> 按钮大小 </param>
        /// <param name="borderWidth"> 按钮边框宽度 </param>
        public void ShrinkTextBlockFontToFit(TextBlock textBlock, double maxWidth)
        {
            if (textBlock == null || string.IsNullOrEmpty(textBlock.Text)) return; // 防止空引用或空文本
            var appearance = SettingDatabase.GetAppearanceSettings().FirstOrDefault();
            double fontSize = appearance.FontSize; // 获取初始字号
            textBlock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity)); // 测量当前文本块宽度
            while (textBlock.DesiredSize.Width >= maxWidth)
            {
                fontSize -= 0.1; // 递减字号
                textBlock.FontSize = fontSize; // 应用新字号
                textBlock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity)); // 重新测量宽度
            }
        }

        /// <summary>
        /// 打开指定菜单（统一管理所有菜单的打开）
        /// </summary>
        /// <param name="sender"> 触发菜单的按钮 </param>
        /// <param name="targetMenu"> 要打开的菜单名称 </param>
        /// <param name="sourceWindow"> 触发菜单的窗口 </param>
        /// <param name="tableName"> 表名 </param>
        public void OpenMenu(object sender, string targetMenu, Window sourceWindow, string tableName)
        {
            if (sender is not Button button) return; // 确保发送者是按钮

            bool isMainWindow = sourceWindow is MainWindow || sourceWindow is SearchWindow; // 是否为主窗口
            if (isMainWindow) isClosing = true; // 如果是主窗口，设置关闭标志

            // 创建新菜单实例
            Window menuWindow = CreateMenuInstance(targetMenu, button, tableName, sourceWindow);
            if (menuWindow == null) return;
            
            BaseMenuWindow menu = menuWindow as BaseMenuWindow;
            if (menu == null) return;

            if (isMainWindow) // 为主窗口菜单绑定关闭事件
                BindMainWindowClosingFlag(menu);

            menu.Show(); // 显示菜单（基类会自动处理动画）
        }

        /// <summary>
        /// 菜单无论是失焦关闭还是按钮直接 Close，都要恢复主窗口的失焦关闭能力。
        /// </summary>
        private void BindMainWindowClosingFlag(BaseMenuWindow menu)
        {
            void ResetClosingFlag()
            {
                isClosing = false;
            }

            menu.ClosingOrHiding += ResetClosingFlag;
            menu.Closed += (_, _) => ResetClosingFlag();
        }

        /// <summary>
        /// 加载扩展模块实例
        /// </summary>
        /// <param name="button">触发按钮</param>
        /// <returns>扩展模块实例，如果加载失败返回null</returns>
        private IExtensionModule LoadExtensionModule(Button button)
        {
            try
            {
                if (button.Tag is not ButtonData buttonData || buttonData.ActionType != ActionType.LoadExtension)
                    return null;

                if (string.IsNullOrEmpty(buttonData.Location) || !File.Exists(buttonData.Location))
                    return null;

                // 加载扩展DLL
                var assembly = System.Reflection.Assembly.LoadFrom(buttonData.Location);
                
                // 查找扩展模块类型
                var moduleType = assembly.GetTypes()
                    .FirstOrDefault(t => typeof(IExtensionModule).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

                if (moduleType == null)
                    return null;

                return (IExtensionModule)Activator.CreateInstance(moduleType); // 创建扩展模块实例
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 检查扩展是否支持右键菜单
        /// </summary>
        /// <param name="button">触发按钮</param>
        /// <returns>是否支持右键菜单</returns>
        public bool CheckExtensionHasContextMenu(Button button)
        {
            var extensionModule = LoadExtensionModule(button);
            return extensionModule?.HasContextMenu ?? false;
        }

        /// <summary>
        /// 尝试加载扩展的右键菜单
        /// </summary>
        /// <param name="button">触发按钮</param>
        /// <param name="tableName">表名</param>
        /// <param name="sourceWindow">源窗口</param>
        public void TryLoadExtensionContextMenu(Button button, string tableName, Window sourceWindow)
        {
            try
            {
                if (button.Tag is not ButtonData buttonData || buttonData.ActionType != ActionType.LoadExtension)
                    return;

                if (string.IsNullOrEmpty(buttonData.Location) || !File.Exists(buttonData.Location))
                    return;

                // 检查是否为主窗口，如果是则设置关闭标志
                bool isMainWindow = sourceWindow is MainWindow || sourceWindow is SearchWindow;
                if (isMainWindow) isClosing = true;

                // 加载扩展DLL
                var assembly = System.Reflection.Assembly.LoadFrom(buttonData.Location);

                // 尝试查找扩展菜单类型
                var menuType = assembly.GetTypes()
                    .FirstOrDefault(t => typeof(Quicker.Windows.Menus.BaseMenuWindow).IsAssignableFrom(t) && !t.IsAbstract);

                if (menuType != null)
                {
                    // 创建扩展菜单实例
                    var menuInstance = (Window)Activator.CreateInstance(menuType, 
                        int.Parse(button.Name.Replace(tableName, "")), tableName, sourceWindow);

                    if (menuInstance != null)
                    {
                        // 如果是主窗口菜单，绑定关闭事件来重置关闭标志
                        if (isMainWindow && menuInstance is BaseMenuWindow menu)
                            BindMainWindowClosingFlag(menu);

                        // 设置菜单位置和显示
                        SetMenuPositionAndShow(menuInstance, sourceWindow);
                    }
                }
            }
            catch
            {
                // 不抛出异常，让系统使用默认菜单
            }
        }

        /// <summary>
        /// 设置菜单位置并显示
        /// </summary>
        /// <param name="menuWindow">菜单窗口</param>
        /// <param name="sourceWindow">源窗口</param>
        private void SetMenuPositionAndShow(Window menuWindow, Window sourceWindow)
        {
            // 设置菜单位置到鼠标附近
            var mousePos = System.Windows.Forms.Cursor.Position;
            menuWindow.Left = mousePos.X;
            menuWindow.Top = mousePos.Y;

            // 显示菜单（BaseMenuWindow会自动处理失焦关闭和焦点管理）
            menuWindow.Show();
        }

        /// <summary>
        /// 创建菜单实例的工厂方法
        /// </summary>
        /// <param name="menuType">菜单类型</param>
        /// <param name="button">触发按钮</param>
        /// <param name="tableName">表名</param>
        /// <param name="sourceWindow">源窗口</param>
        /// <returns>菜单实例</returns>
        private Window CreateMenuInstance(string menuType, Button button, string tableName, Window sourceWindow)
        {
            return menuType switch
            {
                "OperationMenu" => new OperationMenu(int.Parse(button.Name.Replace(tableName, "")), tableName, sourceWindow),
                "CreatActionMenu" => new CreatActionMenu(int.Parse(button.Name.Replace(tableName, "")), tableName),
                "SelectActionPageMenu" => new SelectActionPageMenu(),
                "CustomMenu" => new CustomMenu(),
                "EditSceneMenu" => new EditSceneMenu(tableName), // 假设需要场景标签
                _ => throw new ArgumentException($"未知菜单类型: {menuType}")
            };
        }

        /// <summary>
        /// 处理地址文本内容
        /// </summary>
        /// <param name="location"> 地址文本内容 </param>
        public string ProcessLocation(string location)
        {
            if (location.StartsWith("\"") && location.EndsWith("\"")) // 如果文本含有""符号，则去掉
                return location = location.Substring(1, location.Length - 2);
            return location; // 返回处理后的地址文本内容
        }

        /// <summary>
        /// 通过拖拽删除动作
        /// </summary>
        /// <param name="mainWindow"> 主窗口 </param>
        /// <param name="commonStyle"> 通用样式 </param>
        public void DeleteActionByDrag(MainWindow mainWindow, string commonStyle)
        {
            if (SourceButton == null) return; // 如果按钮不存在，直接返回
            string tableName = SourceTableName.StartsWith("_global") ? "_global" : commonStyle; // 获取表名
            int buttonID = int.Parse(SourceButton.Name.Replace($"{tableName}", "")); // 获取按钮ID
            db2.DeleteAction(buttonID, tableName); // 删除动作
            mainWindow.UpdateButtonContent(buttonID, tableName); // 更新按钮内容
        }

        // 手动释放资源
        public void Dispose()
        {
            iconManager?.Dispose(); // 清理图标管理器资源
            GC.Collect(); // 强制垃圾回收
            GC.WaitForPendingFinalizers(); // 等待垃圾回收完成
            GC.Collect(); // 再次强制垃圾回收
        }

        /// <summary>
        /// 从剪贴板创建文件动作
        /// </summary>
        /// <param name="buttonID">按钮ID</param>
        /// <param name="tableName">表名</param>
        /// <returns>是否成功创建动作</returns>
        public bool CreateFileActionFromClipboard(int buttonID, string tableName)
        {
            if (!Clipboard.ContainsFileDropList()) return false;

            var fileList = Clipboard.GetFileDropList(); // 获取文件列表
            if (fileList.Count <= 0) return false;

            string filePath = fileList[0]; // 获取第一个文件路径
            if (IsImaege(filePath)) // 如果是图片文件
            {
                ProcessImageDrop(filePath, buttonID, tableName); // 处理图片拖拽
                return true;
            }
            else if (Path.GetExtension(filePath).ToLower() == ".url") // 判断是否为 .url 文件
            {
                ProcessUrlShortcutDrop(filePath, buttonID, tableName);
                return true;
            }

            ImageSource iconSource = IconManager.GetIcon(filePath); // 获取图标
            string iconPath = ""; // 默认图标路径
            if (iconSource != null) // 如果图标存在
            {
                iconPath = iconManager.CheckCachedIcon(filePath); // 检查已经保存的图标
                if (string.IsNullOrEmpty(iconPath)) // 如果不存在保存的图标
                    iconPath = iconManager.SaveIconToFile(iconSource); // 保存图标到文件
            }

            string fileName = Path.GetFileNameWithoutExtension(filePath); // 获取文件名
            ButtonData buttonData = new ButtonData
            {
                ButtonID = buttonID, // 使用当前按钮ID
                Title = fileName, // 设置按钮名称
                Location = filePath, // 设置文件路径
                ImagePath = iconPath, // 设置图标路径
                Data1 = false.ToString(), // 是否使用管理员身份运行
                Data2 = true.ToString(), // 尝试打开已存在的窗口
                Data3 = 0.ToString(), // 设置窗口状态
                Description = $"打开文件: {fileName}", // 设置用途
                CreateTime = DateTime.Now,
                ActionType = ActionType.OpenFile, // 设置动作类型
            }; // 设置按钮数据
            db2.UpdateAction(buttonData, tableName); // 添加按钮数据到数据库
            return true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public static bool IsCtrlPressed()
        {
            return (GetAsyncKeyState(VK_LCONTROL) & 0x8000) != 0 ||
                   (GetAsyncKeyState(VK_RCONTROL) & 0x8000) != 0;
        }
    }
}