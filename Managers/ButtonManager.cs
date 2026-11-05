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
using Quicker.Helpers;
using System.Windows;
using Quicker.Models;
using WpfAnimatedGif;
using Quicker.Extend;
using System.IO;

namespace Quicker.Managers
{
    public class ButtonManager
    {
        /// <summary>
        /// 管理窗口（ActionPageManageWindow）中按钮的固定渲染尺寸
        /// 与该窗口硬编码的画布高度（280 / 215）保持一致
        /// </summary>
        public const double ManageWindowButtonSize = 65;

        /// <summary>
        /// 管理窗口中按钮标题的最大宽度。
        /// </summary>
        public const double ManageWindowTitleMaxWidth = 60;

        public bool isClosing = false, isDragging, shouldHideTooltip; // 窗口关闭和拖拽状态、隐藏提示标志
        private readonly IconManager iconManager = new(); // 图标管理器
        private readonly ButtonDatabase db2 = new(); // 按钮数据库（用于读数据 / 交换 / 删除等非保存操作）
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
            bool isCtrlPressed = App.GetCtrlKeyState(); // Ctrl键是否按下
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
        public void Button_Drop(object sender, DragEventArgs e, double buttonSize, double titleMaxWidth, string tableName)
        {
            Button TargetButton = sender as Button; // 获取目标按钮
            if (TargetButton == SourceButton) return; // 如果目标按钮和源按钮相同，直接返回
            if (e.Data.GetDataPresent(typeof(ButtonData))) // 如果拖拽的是按钮
                ProcessButtonDrop(TargetButton, SourceTableName, tableName, buttonSize, titleMaxWidth); // 处理按钮拖拽
            else if (e.Data.GetDataPresent(DataFormats.FileDrop)) // 如果拖拽的是文件
                ProcessFileDrop(e, TargetButton, buttonSize, titleMaxWidth, tableName); // 处理文件拖拽
            else if (e.Data.GetDataPresent(DataFormats.Text)) // 如果拖拽的是文本（可能是 URL）
                ProcessTextDrop(e, TargetButton, tableName); // 处理文本拖拽
        }

        /// <summary>
        /// 处理文本拖拽
        /// </summary>
        private void ProcessTextDrop(DragEventArgs e, Button targetButton, string tableName)
        {
            string text = e.Data.GetData(DataFormats.Text) as string;
            if (string.IsNullOrEmpty(text)) return;
            if (Uri.TryCreate(text, UriKind.Absolute, out Uri url))
            {
                _ = ProcessUrlDrop(url.ToString(), int.Parse(targetButton.Name.Replace(tableName, "")), tableName);
            }
        }

        /// <summary>
        /// 处理按钮拖拽到其他按钮上
        /// </summary>
        private void ProcessButtonDrop(Button targetButton, string tableName1, string tableName2, double buttonSize, double titleMaxWidth)
        {
            if (SourceButton == null) return;
            int buttonID1 = int.Parse(SourceButton.Name.Replace(tableName1, ""));
            int buttonID2 = int.Parse(targetButton.Name.Replace(tableName2, ""));
            db2.ExchangeButtonID(buttonID1, buttonID2, tableName1, tableName2);
            var sourceData = SourceButton.Tag as ButtonData;
            var targetData = targetButton.Tag as ButtonData;

            if (!(Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)))
                RefreshButtonDisplay(SourceButton, targetData, buttonSize, titleMaxWidth);
            RefreshButtonDisplay(targetButton, sourceData, buttonSize, titleMaxWidth);
            SourceButton = null;

            AppStateManager.NotifyActionPageChanged(tableName1);
            if (tableName1 != tableName2) AppStateManager.NotifyActionPageChanged(tableName2);
        }

        /// <summary>
        /// 处理文件拖拽
        /// </summary>
        private void ProcessFileDrop(DragEventArgs e, Button TargetButton, double buttonSize, double titleMaxWidth, string tableName)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files.Length <= 0) return;

            if (files.Length == 1)
                ProcessSingleFileDrop(TargetButton, files[0], buttonSize, titleMaxWidth, tableName);
            else
                ProcessMultipleFileDrop(TargetButton, files, buttonSize, titleMaxWidth, tableName);

            UpdateButtonDisplay(TargetButton, tableName, buttonSize, titleMaxWidth);
        }

        /// <summary>
        /// 更新按钮显示
        /// </summary>
        private void UpdateButtonDisplay(Button button, string tableName, double buttonSize, double titleMaxWidth)
        {
            var buttonData = db2.GetButtonDataByID(int.Parse(button.Name.Replace(tableName, "")), tableName);
            RefreshButtonDisplay(button, buttonData, buttonSize, titleMaxWidth);
        }

        /// <summary>
        /// 处理单个文件拖拽
        /// </summary>
        private void ProcessSingleFileDrop(Button button, string filePath, double buttonSize, double titleMaxWidth, string tableName)
        {
            if (IsImage(filePath))
            {
                ProcessImageDrop(filePath, int.Parse(button.Name.Replace(tableName, "")), tableName);
                return;
            }
            else if (Path.GetExtension(filePath).ToLower() == ".url")
            {
                ProcessUrlShortcutDrop(filePath, int.Parse(button.Name.Replace(tableName, "")), tableName);
                return;
            }

            ImageSource iconSource = IconManager.GetIcon(filePath);
            string iconPath = "";
            if (iconSource != null)
            {
                iconPath = iconManager.CheckCachedIcon(filePath);
                if (string.IsNullOrEmpty(iconPath))
                    iconPath = iconManager.SaveIconToFile(iconSource);
            }

            string fileName = Path.GetFileNameWithoutExtension(filePath);
            ButtonData buttonData = new ButtonData
            {
                ButtonID = int.Parse(button.Name.Replace(tableName, "")),
                Title = fileName,
                Location = filePath,
                ImagePath = iconPath,
                Data1 = false.ToString(),
                Data2 = true.ToString(),
                Data3 = 0.ToString(),
                Description = $"打开文件: {fileName}",
                ActionType = ActionType.OpenFile,
            };
            ActionSaveHelper.SaveAction(buttonData, tableName, notify: false);
            RefreshButtonDisplay(button, buttonData, buttonSize, titleMaxWidth);
        }

        /// <summary>
        /// 判断是否为图片文件
        /// </summary>
        private bool IsImage(string filePath)
        {
            string extension = Path.GetExtension(filePath).ToLower();
            return extension == ".jpg" || extension == ".jpeg" || extension == ".png" || extension == ".gif";
        }

        /// <summary>
        /// 处理图片拖拽
        /// </summary>
        private void ProcessImageDrop(string filePath, int buttonID, string tableName)
        {
            BitmapImage bitmap = new BitmapImage(new Uri(filePath));
            string iconPath = "";
            if (bitmap != null)
            {
                iconPath = iconManager.CheckCachedIcon(filePath);
                if (string.IsNullOrEmpty(iconPath))
                    iconPath = iconManager.SaveIconToFile(bitmap);
            }

            string fileName = Path.GetFileNameWithoutExtension(filePath);
            ButtonData buttonData = new ButtonData
            {
                ButtonID = buttonID,
                Title = fileName,
                Location = filePath,
                ImagePath = iconPath,
                Data1 = false.ToString(),
                Data2 = true.ToString(),
                Data3 = 0.ToString(),
                Description = $"打开图片: {fileName}",
                ActionType = ActionType.OpenFile,
            };
            ActionSaveHelper.SaveAction(buttonData, tableName, notify: false);
        }

        /// <summary>
        /// 处理 Internet 快捷方式文件拖拽
        /// </summary>
        private void ProcessUrlShortcutDrop(string filePath, int buttonID, string tableName)
        {
            string url = File.ReadAllText(filePath);
            string[] lines = File.ReadAllLines(filePath);
            foreach (string line in lines)
            {
                if (line.StartsWith("URL="))
                {
                    url = line.Substring("URL=".Length).Trim();
                    break;
                }
            }
            _ = ProcessUrlDrop(url, buttonID, tableName);
        }

        /// <summary>
        /// 处理多个文件拖拽
        /// </summary>
        private void ProcessMultipleFileDrop(Button button, string[] filePaths, double buttonSize, double titleMaxWidth, string tableName)
        {
            string filePath = string.Join(";", filePaths);
            ImageSource iconSource = IconManager.GetIcon(filePaths[0]);
            string iconPath = "";
            if (iconSource != null)
            {
                iconPath = iconManager.CheckCachedIcon(filePaths[0]);
                if (string.IsNullOrEmpty(iconPath))
                    iconPath = iconManager.SaveIconToFile(iconSource);
            }

            string fileName = Path.GetFileNameWithoutExtension(filePaths[0]);
            ButtonData buttonData = new ButtonData
            {
                ButtonID = int.Parse(button.Name.Replace(tableName, "")),
                Title = $"{fileName}等{filePaths.Length}个文件(夹)",
                Location = filePath,
                ImagePath = iconPath,
                Data1 = false.ToString(),
                Data2 = true.ToString(),
                Data3 = 0.ToString(),
                Description = $"打开以{fileName}为首的多个文件:",
                ActionType = ActionType.OpenFiles,
            };
            ActionSaveHelper.SaveAction(buttonData, tableName, notify: false);
            RefreshButtonDisplay(button, buttonData, buttonSize, titleMaxWidth);
        }

        /// <summary>
        /// 处理 URL 拖拽
        /// </summary>
        private async Task ProcessUrlDrop(string url, int buttonID, string tableName)
        {
            ImageSource iconSource = await iconManager.GetWebsiteIconAsync(url);
            string iconPath = "";
            if (iconSource != null)
            {
                iconPath = iconManager.CheckCachedIcon(url);
                if (string.IsNullOrEmpty(iconPath))
                    iconPath = iconManager.SaveIconToFile(iconSource);
            }

            ButtonData buttonData = new ButtonData
            {
                ButtonID = buttonID,
                Title = DomainHelper.GetWebsiteNameFromUrl(url),
                Location = url,
                ImagePath = iconPath,
                Data3 = 0.ToString(),
                Description = $"打开网页: {url}",
                ActionType = ActionType.OpenWebsite,
            };
            ActionSaveHelper.SaveAction(buttonData, tableName, notify: true);
        }

        /// <summary>
        /// 根据按钮尺寸计算标题最大宽度（主窗口使用：按钮大小减去左右边框宽度）。
        /// </summary>
        public static double GetTitleMaxWidthForButtonSize(double buttonSize)
        {
            var appearance = SettingDatabase.GetAppearanceSettings().FirstOrDefault();
            double borderWidth = appearance?.BorderWidth ?? 0;
            return buttonSize - borderWidth * 2;
        }

        /// <summary>
        /// 刷新按钮显示内容（主窗口用：标题最大宽度由 buttonSize 与外观设置自动推导）
        /// </summary>
        public void RefreshButtonDisplay(Button button, ButtonData buttonInformation, double buttonSize)
        {
            double titleMaxWidth = GetTitleMaxWidthForButtonSize(buttonSize);
            RefreshButtonDisplayCore(button, buttonInformation, buttonSize, titleMaxWidth);
        }

        /// <summary>
        /// 刷新按钮显示内容（管理窗口 / 内部透传用：显式指定标题最大宽度）
        /// </summary>
        public void RefreshButtonDisplay(Button button, ButtonData buttonInformation, double buttonSize, double titleMaxWidth)
        {
            RefreshButtonDisplayCore(button, buttonInformation, buttonSize, titleMaxWidth);
        }

        /// <summary>
        /// 刷新按钮显示内容的统一实现
        /// </summary>
        private void RefreshButtonDisplayCore(Button button, ButtonData buttonInformation, double buttonSize, double titleMaxWidth)
        {
            if (buttonInformation == null || buttonInformation.Location == null)
            {
                ResetButtonDisplay(button);
                return;
            }

            button.Tag = buttonInformation;
            var grid = CreateButtonGrid(buttonInformation, buttonSize, titleMaxWidth);
            button.Content = grid;
            LoadActionTooltip(button, buttonInformation);
        }

        /// <summary>
        /// 重置按钮显示
        /// </summary>
        private void ResetButtonDisplay(Button button)
        {
            button.Content = null;
            button.ToolTip = null;
            button.Tag = null;
        }

        /// <summary>
        /// 创建按钮网格
        /// </summary>
        private Grid CreateButtonGrid(ButtonData buttonInformation, double buttonSize, double titleMaxWidth)
        {
            Grid grid = new();
            if (!string.IsNullOrEmpty(buttonInformation.ImagePath))
            {
                AddImageToGrid(grid, buttonInformation, buttonSize);
            }

            if (!string.IsNullOrEmpty(buttonInformation.Title))
            {
                AddTitleToGrid(grid, buttonInformation, titleMaxWidth);
            }

            return grid;
        }

        /// <summary>
        /// 添加图像到网格，支持GIF动图
        /// </summary>
        private void AddImageToGrid(Grid grid, ButtonData buttonInformation, double buttonSize)
        {
            try
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                Image image = LoadActionIcon(buttonInformation, buttonSize);

                string ext = Path.GetExtension(buttonInformation.ImagePath ?? "").ToLower();
                if (ext == ".gif" && !string.IsNullOrEmpty(buttonInformation.ImagePath))
                {
                    var bitmap = new BitmapImage(new Uri(buttonInformation.ImagePath));
                    WpfAnimatedGif.ImageBehavior.SetAnimatedSource(image, bitmap);
                }

                grid.Children.Add(image);
                Grid.SetRow(image, 0);
            }
            catch
            {
                using var toast = new ToastManager();
                toast.Show($"图标加载失败：动作{buttonInformation.Title}的图标被移动或删除", ToastType.Error);
            }
        }

        /// <summary>
        /// 添加标题到网格
        /// </summary>
        private void AddTitleToGrid(Grid grid, ButtonData buttonInformation, double titleMaxWidth)
        {
            var appearance = SettingDatabase.GetAppearanceSettings().FirstOrDefault();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            TextBlock textBlock = LoadActionTitle(buttonInformation);
            textBlock.Visibility = appearance.HideActionNameAfterIcon ? Visibility.Collapsed : Visibility.Visible;
            if (appearance.AutoHideTitleBar)
            {
                ShrinkTextBlockFontToFit(textBlock, titleMaxWidth);
            }
            else
            {
                AutoEllipsisTextBlock(textBlock, (int)titleMaxWidth);
            }
            grid.Children.Add(textBlock);
            Grid.SetRow(textBlock, 1);
        }

        /// <summary>
        /// 加载动作图标
        /// </summary>
        private Image LoadActionIcon(ButtonData buttonInformation, double buttonSize)
        {
            var appearance = SettingDatabase.GetAppearanceSettings().FirstOrDefault();
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
            bmp.Freeze();
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
        private TextBlock LoadActionTitle(ButtonData buttonInformation)
        {
            var appearance = SettingDatabase.GetAppearanceSettings().FirstOrDefault();
            var textBlock = new TextBlock
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                TextAlignment = TextAlignment.Center,
                Text = buttonInformation.Title
            };
            textBlock.FontSize = appearance.FontSize;
            return textBlock;
        }

        /// <summary>
        /// 加载动作提示
        /// </summary>
        private void LoadActionTooltip(Button button, ButtonData buttonInformation)
        {
            if (!shouldHideTooltip)
            {
                string toolTipText = null;
                if (!string.IsNullOrWhiteSpace(buttonInformation.Title) || !string.IsNullOrWhiteSpace(buttonInformation.Description))
                {
                    string name = !string.IsNullOrWhiteSpace(buttonInformation.Title) ? buttonInformation.Title : null;
                    string usage = !string.IsNullOrWhiteSpace(buttonInformation.Description) ? buttonInformation.Description : null;
                    toolTipText = (name + "\n" + usage).Trim('\n');
                }
                button.ToolTip = string.IsNullOrEmpty(toolTipText) ? null : toolTipText;
            }
        }

        /// <summary>
        /// 加载动作使用次数
        /// </summary>
        public void LoadActionUsedTimes(Button button, ButtonData data)
        {
            button.Content = null;
            TextBlock textBlock = new()
            {
                FontSize = 11,
                TextAlignment = TextAlignment.Center,
                Text = "使用次数" + "\n" + data.UsedTimes.ToString()
            };
            button.Content = textBlock;
        }

        // 鼠标左键按下时记录初始位置
        public void Button_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Button button = sender as Button;
            initialMousePosition = e.GetPosition(button);
            SourceButton = button;
            isDragging = false;
        }

        /// <summary>
        /// 鼠标移动时检查是否满足拖拽条件
        /// </summary>
        public void Button_PreviewMouseMove(object sender, MouseEventArgs e, bool isMainButton, string sourceTableName = null)
        {
            SourceTableName = sourceTableName;
            if (sender is Button button && e.LeftButton == MouseButtonState.Pressed)
            {
                Point currentPosition = e.GetPosition(button);
                double deltaX = currentPosition.X - initialMousePosition.X;
                double deltaY = currentPosition.Y - initialMousePosition.Y;
                double distance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
                if (distance > 10 && !isDragging)
                {
                    isDragging = true;
                    if (isMainButton)
                    {
                        if (button.Tag is ButtonData data)
                            DragDrop.DoDragDrop(button, data, DragDropEffects.Copy | DragDropEffects.Move | DragDropEffects.None);
                    }
                    else
                    {
                        string buttonName = button.Name;
                        DataObject data = new DataObject();
                        data.SetData("ButtonData", buttonName);
                        DragDrop.DoDragDrop(button, data, DragDropEffects.Move);
                    }
                }
            }
        }

        // 鼠标左键释放时重置状态
        public void Button_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            isDragging = false;
        }

        /// <summary>
        /// 动态调整TextBlock的字体大小以适应最大宽度
        /// </summary>
        public void AutoEllipsisTextBlock(TextBlock textBlock, int maxWidth)
        {
            if (textBlock == null || string.IsNullOrEmpty(textBlock.Text)) return;
            if (maxWidth <= 0) return;

            textBlock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            if (textBlock.DesiredSize.Width <= maxWidth) return;

            const string ellipsis = "...";
            string originalText = textBlock.Text;

            for (int length = originalText.Length - 1; length >= 0; length--)
            {
                textBlock.Text = originalText.Substring(0, length) + ellipsis;
                textBlock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                if (textBlock.DesiredSize.Width <= maxWidth) return;
            }

            textBlock.Text = ellipsis;
        }

        /// <summary>
        /// 缩小TextBlock字号直到文本宽度小于等于按钮大小 - 按钮边框 * 2
        /// </summary>
        public void ShrinkTextBlockFontToFit(TextBlock textBlock, double maxWidth)
        {
            if (textBlock == null || string.IsNullOrEmpty(textBlock.Text)) return;
            if (maxWidth <= 0) return;

            var appearance = SettingDatabase.GetAppearanceSettings().FirstOrDefault();
            double fontSize = appearance?.FontSize ?? 12;
            const double minFontSize = 1;

            textBlock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            while (textBlock.DesiredSize.Width >= maxWidth && fontSize > minFontSize)
            {
                fontSize -= 0.1;
                textBlock.FontSize = fontSize;
                textBlock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            }
        }

        /// <summary>
        /// 打开指定菜单（统一管理所有菜单的打开）
        /// </summary>
        public void OpenMenu(object sender, string targetMenu, Window sourceWindow, string tableName)
        {
            if (sender is not Button button) return;

            bool isMainWindow = sourceWindow is MainWindow || sourceWindow is SearchWindow;

            Window menuWindow;
            try
            {
                menuWindow = CreateMenuInstance(targetMenu, button, tableName, sourceWindow);
            }
            catch
            {
                return;
            }

            if (menuWindow is not BaseMenuWindow menu) return;

            if (isMainWindow)
            {
                isClosing = true;
                BindMainWindowClosingFlag(menu);
            }

            menu.Show();
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
        private IExtensionModule LoadExtensionModule(Button button)
        {
            try
            {
                if (button.Tag is not ButtonData buttonData || buttonData.ActionType != ActionType.LoadExtension)
                    return null;

                if (string.IsNullOrEmpty(buttonData.Location) || !File.Exists(buttonData.Location))
                    return null;

                var assembly = System.Reflection.Assembly.LoadFrom(buttonData.Location);

                var moduleType = assembly.GetTypes()
                    .FirstOrDefault(t => typeof(IExtensionModule).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

                if (moduleType == null)
                    return null;

                return (IExtensionModule)Activator.CreateInstance(moduleType);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 检查扩展是否支持右键菜单
        /// </summary>
        public bool CheckExtensionHasContextMenu(Button button)
        {
            var extensionModule = LoadExtensionModule(button);
            return extensionModule?.HasContextMenu ?? false;
        }

        /// <summary>
        /// 尝试加载扩展的右键菜单
        /// </summary>
        public void TryLoadExtensionContextMenu(Button button, string tableName, Window sourceWindow)
        {
            try
            {
                if (button.Tag is not ButtonData buttonData || buttonData.ActionType != ActionType.LoadExtension)
                    return;

                if (string.IsNullOrEmpty(buttonData.Location) || !File.Exists(buttonData.Location))
                    return;

                var assembly = System.Reflection.Assembly.LoadFrom(buttonData.Location);

                var menuType = assembly.GetTypes()
                    .FirstOrDefault(t => typeof(BaseMenuWindow).IsAssignableFrom(t) && !t.IsAbstract);

                if (menuType == null) return;

                var menuInstance = (Window)Activator.CreateInstance(menuType,
                    int.Parse(button.Name.Replace(tableName, "")), tableName, sourceWindow);

                if (menuInstance is not BaseMenuWindow menu) return;

                bool isMainWindow = sourceWindow is MainWindow || sourceWindow is SearchWindow;
                if (isMainWindow)
                {
                    isClosing = true;
                    BindMainWindowClosingFlag(menu);
                }

                SetMenuPositionAndShow(menuInstance, sourceWindow);
            }
            catch
            {
                // 静默失败，让系统使用默认菜单
            }
        }

        /// <summary>
        /// 设置菜单位置并显示
        /// </summary>
        private void SetMenuPositionAndShow(Window menuWindow, Window sourceWindow)
        {
            var mousePos = System.Windows.Forms.Cursor.Position;
            menuWindow.Left = mousePos.X;
            menuWindow.Top = mousePos.Y;
            menuWindow.Show();
        }

        /// <summary>
        /// 创建菜单实例的工厂方法
        /// </summary>
        private Window CreateMenuInstance(string menuType, Button button, string tableName, Window sourceWindow)
        {
            return menuType switch
            {
                "OperationMenu" => new OperationMenu(int.Parse(button.Name.Replace(tableName, "")), tableName, sourceWindow),
                "CreatActionMenu" => new CreatActionMenu(int.Parse(button.Name.Replace(tableName, "")), tableName),
                "SelectActionPageMenu" => new SelectActionPageMenu(),
                "CustomMenu" => new CustomMenu(),
                "EditSceneMenu" => new EditSceneMenu(tableName),
                _ => throw new ArgumentException($"未知菜单类型: {menuType}")
            };
        }

        /// <summary>
        /// 处理地址文本内容（去掉首尾引号与空格）
        /// </summary>
        public static string ProcessLocation(string location)
        {
            if (string.IsNullOrEmpty(location)) return location ?? string.Empty;
            if (location.StartsWith("\"") && location.EndsWith("\""))
                return location.Substring(1, location.Length - 2).Trim();
            return location;
        }

        /// <summary>
        /// 通过拖拽删除动作
        /// </summary>
        public void DeleteActionByDrag(MainWindow mainWindow, string commonStyle)
        {
            if (SourceButton == null) return;
            string tableName = SourceTableName.StartsWith("_global") ? "_global" : commonStyle;
            int buttonID = int.Parse(SourceButton.Name.Replace($"{tableName}", ""));
            db2.DeleteAction(buttonID, tableName);
            mainWindow.UpdateButtonContent(buttonID, tableName);
        }

        // 手动释放资源
        public void Dispose()
        {
            iconManager?.Dispose();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        /// <summary>
        /// 从剪贴板创建文件动作
        /// </summary>
        public bool CreateFileActionFromClipboard(int buttonID, string tableName)
        {
            if (!Clipboard.ContainsFileDropList()) return false;

            var fileList = Clipboard.GetFileDropList();
            if (fileList.Count <= 0) return false;

            string filePath = fileList[0];
            if (IsImage(filePath))
            {
                ProcessImageDrop(filePath, buttonID, tableName);
                return true;
            }
            else if (Path.GetExtension(filePath).ToLower() == ".url")
            {
                ProcessUrlShortcutDrop(filePath, buttonID, tableName);
                return true;
            }

            ImageSource iconSource = IconManager.GetIcon(filePath);
            string iconPath = "";
            if (iconSource != null)
            {
                iconPath = iconManager.CheckCachedIcon(filePath);
                if (string.IsNullOrEmpty(iconPath))
                    iconPath = iconManager.SaveIconToFile(iconSource);
            }

            string fileName = Path.GetFileNameWithoutExtension(filePath);
            ButtonData buttonData = new ButtonData
            {
                ButtonID = buttonID,
                Title = fileName,
                Location = filePath,
                ImagePath = iconPath,
                Data1 = false.ToString(),
                Data2 = true.ToString(),
                Data3 = 0.ToString(),
                Description = $"打开文件: {fileName}",
                ActionType = ActionType.OpenFile,
            };
            ActionSaveHelper.SaveAction(buttonData, tableName, notify: false);
            return true;
        }
    }
}