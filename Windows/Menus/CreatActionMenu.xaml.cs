using System;
using Quicker.Windows.MainWindows.MainWindow;
using System.Windows.Media.Imaging;
using Quicker.Windows.MainWindows;
using System.Collections.Generic;
using Quicker.Windows.AddWindows;
using System.Windows.Controls;
using Quicker.Database.Core;
using System.Globalization;
using System.Windows.Media;
using Quicker.Internal;
using Quicker.Managers;
using Quicker.Helpers;
using System.Windows;
using Quicker.Models;
using System.IO;

namespace Quicker.Windows.Menus
{
    public partial class CreatActionMenu : BaseMenuWindow
    {
        #region 字段和属性
        private readonly ButtonManager buttonManager = new(); // 按钮管理器
        private readonly ButtonDatabase db2 = new(); // 设置管理器
        private readonly InternalCommandManager internalCommandManager = InternalCommandManager.Instance; // 内部命令管理器
        private InternalCommand currentInternalCommand; // 当前内部命令
        private bool hasChanged = false; // 是否已检查
        public int ButtonID { get; private set; } // 当前按钮
        public string TableName { get; private set; } // 表名
        private bool haveAction = false; // 是否有动作
        private bool close = true; // 是否正在关闭
        private readonly IconManager iconManager = new(); // 图标管理器
        #endregion

        #region 初始化
        public CreatActionMenu(int buttonID, string tableName)
        {
            InitializeComponent();
            ButtonID = buttonID; // 设置当前按钮
            TableName = tableName; // 设置表名
            base.SetWindowTopmost(); // 设置窗口置顶
            internalCommandManager.CommandPublished += InternalCommandManager_CommandPublished; // 监听内部命令
        }

        // 重写基类的窗口加载方法
        protected override void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            base.OnWindowLoaded(sender, e); // 调用基类方法处理动画
            base.SetWindowPositionNearMouse(); // 设置窗口位置
            SetButtonVisbility(); // 设置按钮可见性
        }
        #endregion

        #region 按钮可见性管理
        // 设置按钮可见性
        private void SetButtonVisbility()
        {
            SetPasteActionButtonVisibility();
            SetCreatOpenFileActionButtonVisibility();
        }

        // 设置粘贴动作按钮可见性
        private void SetPasteActionButtonVisibility(InternalCommand command = null)
        {
            if (command == null)
                internalCommandManager.TryGetLatestCommand(out command); // 获取内部命令

            currentInternalCommand = command;

            if (command == null)
            {
                HidePasteActionButton(); // 隐藏按钮
                return;
            }

            switch (command.CommandType)
            {
                case InternalCommandType.OpenActionPage:
                    HandleOpenActionPageVisibility(command); // 处理打开动作页按钮可见性
                    break;
                case InternalCommandType.CopyAction:
                case InternalCommandType.CutAction:
                    HandleCopyOrCutActionVisibility(command); // 处理复制或剪切动作按钮可见性
                    break;
                default:
                    HidePasteActionButton(); // 隐藏按钮
                    break;
            }
        }

        private void InternalCommandManager_CommandPublished(object sender, InternalCommand command)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => SetPasteActionButtonVisibility(command));
                return;
            }

            SetPasteActionButtonVisibility(command);
        }

        // 处理打开动作页按钮可见性
        private void HandleOpenActionPageVisibility(InternalCommand command)
        {
            if (command == null || string.IsNullOrWhiteSpace(command.ActionPageType) || string.IsNullOrWhiteSpace(command.ActionPageIndex))
            {
                HidePasteActionButton();
                return;
            }

            PasteActionTextBlock.Text = $"粘贴动作：{command.ActionPageType}{command.ActionPageIndex}"; // 设置文本
            ShowPasteActionButton(); // 显示按钮
        }

        // 处理复制或剪切动作按钮可见性
        private void HandleCopyOrCutActionVisibility(InternalCommand command)
        {
            if (command == null || string.IsNullOrWhiteSpace(command.TableName))
            {
                HidePasteActionButton();
                return;
            }

            var buttonData = db2.GetButtonDataByID(command.ButtonId, command.TableName); // 获取按钮数据
            if (buttonData != null)
            {
                PasteActionTextBlock.Text = $"粘贴动作：{buttonData.Title}"; // 设置文本
                ShowPasteActionButton(); // 显示按钮
            }
            else
            {
                HidePasteActionButton(); // 隐藏按钮
            }
        }

        // 显示粘贴动作按钮
        private void ShowPasteActionButton()
        {
            if (hasChanged)
            {
                MainGrid.Height += 32; // 增加高度
                Line1.Visibility = Visibility.Visible; // 显示分割线
                PasteActionButton.Visibility = Visibility.Visible; // 显示粘贴按钮
                hasChanged = !hasChanged;
            }
        }

        // 隐藏粘贴动作按钮
        private void HidePasteActionButton()
        {
            if (!hasChanged)
            {
                MainGrid.Height -= 32; // 减少高度
                Line1.Visibility = Visibility.Collapsed; // 隐藏分割线
                PasteActionButton.Visibility = Visibility.Collapsed; // 隐藏粘贴按钮
                base.Top -= 24; // 向上移动
                hasChanged = !hasChanged;
            }
        }

        // 设置创建打开文件动作按钮可见性
        private void SetCreatOpenFileActionButtonVisibility()
        {
            if (!Clipboard.ContainsFileDropList()) // 判断是否有文件
            {
                HideCreatOpenFileActionButton(); // 隐藏按钮
                return;
            }

            var fileList = Clipboard.GetFileDropList(); // 获取文件列表
            string filePath = fileList[0]; // 获取第一个文件路径
            string fileName = Path.GetFileName(filePath); // 获取文件名

            // 处理文件名并设置按钮文本
            string processedFileName = ProcessFileNameForDisplay(fileName); // 处理文件名
            string buttonText = $"创建动作：打开[{processedFileName}]";

            // 设置按钮属性
            SetCreatOpenFileActionButtonProperties(buttonText, filePath);
        }

        /// <summary>
        /// 隐藏创建打开文件动作按钮
        /// </summary>
        private void HideCreatOpenFileActionButton()
        {
            CreatOpenFileActionButton.Visibility = Visibility.Collapsed; // 隐藏按钮
            MainGrid.Height -= 25; // 减少高度
        }

        /// <summary>
        /// 处理文件名用于显示，如果过长则裁剪
        /// </summary>
        /// <param name="fileName">原始文件名</param>
        /// <returns>处理后的文件名</returns>
        private string ProcessFileNameForDisplay(string fileName)
        {
            const double maxTextWidth = 140; // 最大文本宽度
            const string prefix = "创建动作：打开["; // 前缀
            const string suffix = "]"; // 后缀

            // 创建FormattedText的公共参数
            var typeface = GetTextTypeface(); // 字体信息
            var culture = CultureInfo.CurrentCulture; // 文化信息
            // 获取系统DPI，避免类型转换问题
            double dpi = GetSystemDpi(); // DPI信息

            // 计算完整文本的宽度
            string fullText = $"{prefix}{fileName}{suffix}"; // 构造完整文本
            double textWidth = CalculateTextWidth(fullText, typeface, culture, dpi); // 计算宽度
            if (textWidth <= maxTextWidth) // 如果文本宽度在限制范围内，直接返回原文件名
            {
                return fileName;
            }

            // 文本过长，需要裁剪
            return TruncateFileName(fileName, prefix, suffix, maxTextWidth, typeface, culture, dpi);
        }

        /// <summary>
        /// 获取系统DPI，避免类型转换问题
        /// </summary>
        /// <returns>DPI缩放因子</returns>
        private double GetSystemDpi()
        {
            try
            {
                // 使用Graphics获取系统DPI
                using (var graphics = System.Drawing.Graphics.FromHwnd(IntPtr.Zero))
                {
                    return graphics.DpiX / 96.0; // 96是标准DPI
                }
            }
            catch
            {
                // 如果无法获取，返回默认值1.0
                return 1.0;
            }
        }

        /// <summary>
        /// 获取文本字体信息
        /// </summary>
        /// <returns>字体信息</returns>
        private Typeface GetTextTypeface()
        {
            return new Typeface(
                CreatOpenFileActionTextBlock.FontFamily, 
                CreatOpenFileActionTextBlock.FontStyle, 
                CreatOpenFileActionTextBlock.FontWeight, 
                CreatOpenFileActionTextBlock.FontStretch); // 字体信息
        }

        /// <summary>
        /// 计算文本宽度
        /// </summary>
        /// <param name="text">要计算的文本</param>
        /// <param name="typeface">字体信息</param>
        /// <param name="culture">文化信息</param>
        /// <param name="dpi">DPI信息</param>
        /// <returns>文本宽度</returns>
        private double CalculateTextWidth(string text, Typeface typeface, CultureInfo culture, double dpi)
        {
            var formattedText = new FormattedText(
                text,
                culture,
                FlowDirection.LeftToRight,
                typeface,
                CreatOpenFileActionTextBlock.FontSize,
                Brushes.Black,
                dpi); // 创建FormattedText

            return formattedText.Width; // 返回宽度
        }

        /// <summary>
        /// 裁剪文件名以适应最大宽度限制
        /// </summary>
        /// <param name="fileName">原始文件名</param>
        /// <param name="prefix">前缀文本</param>
        /// <param name="suffix">后缀文本</param>
        /// <param name="maxTextWidth">最大文本宽度</param>
        /// <param name="typeface">字体信息</param>
        /// <param name="culture">文化信息</param>
        /// <param name="dpi">DPI信息</param>
        /// <returns>裁剪后的文件名</returns>
        private string TruncateFileName(string fileName, string prefix, string suffix, double maxTextWidth, Typeface typeface, CultureInfo culture, double dpi)
        {
            // 二分查找合适的文件名长度
            int left = 1; // 左边界
            int right = fileName.Length; // 右边界
            int bestLength = 0; // 最佳长度
            while (left <= right) // 二分查找
            {
                int mid = (left + right) / 2; // 中间值
                string testFileName = fileName.Substring(0, mid) + "..."; // 裁剪文件名
                string testText = prefix + testFileName + suffix; // 构造测试文本
                double testWidth = CalculateTextWidth(testText, typeface, culture, dpi); // 计算宽度
                if (testWidth <= maxTextWidth) // 如果宽度在限制范围内
                {
                    bestLength = mid;
                    left = mid + 1;
                }
                else
                {
                    right = mid - 1;
                }
            }
            return fileName.Substring(0, bestLength) + "..."; // 返回裁剪后的文件名
        }

        /// <summary>
        /// 设置创建打开文件动作按钮的属性
        /// </summary>
        /// <param name="buttonText">按钮文本</param>
        /// <param name="filePath">文件路径</param>
        private void SetCreatOpenFileActionButtonProperties(string buttonText, string filePath)
        {
            CreatOpenFileActionButton.ToolTip = $"创建打开文件或文件夹{filePath}的动作"; // 设置提示
            CreatOpenFileActionTextBlock.Text = buttonText; // 设置文本
        }
        #endregion

        #region 动作管理
        // 粘贴动作
        private void PasteActionButton_Click(object sender, RoutedEventArgs e)
        {
            base.Visibility = Visibility.Hidden; // 隐藏窗口
            PasteAction(); // 粘贴动作
            UpdateUIAfterActionChange(); // 更新UI
            base.Close(); // 关闭窗口
        }

        // 粘贴动作
        private void PasteAction()
        {
            if (currentInternalCommand == null)
                return;

            switch (currentInternalCommand.CommandType)
            {
                case InternalCommandType.CopyAction:
                case InternalCommandType.CutAction:
                    HandleCopyOrCutAction(currentInternalCommand);
                    break;
                case InternalCommandType.OpenActionPage:
                    HandleOpenActionPage(currentInternalCommand);
                    break;
            }
        }

        // 处理复制或剪切动作
        private void HandleCopyOrCutAction(InternalCommand command)
        {
            if (command == null || string.IsNullOrWhiteSpace(command.TableName))
                return;

            var buttonData = db2.GetButtonDataByID(command.ButtonId, command.TableName); // 获取按钮数据
            ButtonData buttonData1 = new()
            {
                ButtonID = ButtonID,
                Title = buttonData.Title,
                Location = buttonData.Location,
                ImagePath = buttonData.ImagePath,
                Data1 = buttonData.Data1,
                Data2 = buttonData.Data2,
                Data3 = buttonData.Data3,
                Description = buttonData.Description,
                CreateTime = DateTime.Now,
                ActionType = buttonData.ActionType,
                UsedTimes = buttonData.UsedTimes
            }; // 创建按钮数据
            db2.UpdateAction(buttonData1, TableName); // 保存按钮数据
            if(command.CommandType == InternalCommandType.CutAction)
            {
                db2.DeleteAction(command.ButtonId, command.TableName); // 删除动作
                UpdateMainWindowButton(command.ButtonId, command.TableName); // 更新主窗口按钮
            }
        }

        // 处理打开动作页
        private void HandleOpenActionPage(InternalCommand command)
        {
            if (command == null || string.IsNullOrWhiteSpace(command.ActionPageType) || string.IsNullOrWhiteSpace(command.ActionPageIndex))
                return;

            ButtonData buttonData3 = new()
            {
                ButtonID = ButtonID,
                Title = command.ActionPageType + command.ActionPageIndex,
                Location = "",
                Data1 = command.ActionPageType,
                Data2 = command.ActionPageIndex,
                ImagePath = "",
                Description = $"打开动作页{command.ActionPageType}{command.ActionPageIndex}",
                CreateTime = DateTime.Now,
                ActionType = ActionType.OpenActionPage,
                UsedTimes = 0
            }; // 创建按钮数据
            db2.UpdateAction(buttonData3, TableName); // 保存按钮数据
        }

        // 更新UI（动作改变后）
        private void UpdateUIAfterActionChange()
        {
            ActionPageManageWindow actionPageManageWindow = Application.Current.Windows.OfType<ActionPageManageWindow>().FirstOrDefault(); // 尝试查找现有的菜单栏
            if (actionPageManageWindow != null)
                actionPageManageWindow.UpdateButton(ButtonID); // 更新菜单栏按钮
            UpdateMainWindowButton(ButtonID, TableName); // 更新主窗口按钮
        }

        // 更新主窗口按钮
        private void UpdateMainWindowButton(int buttonID, string tableName)
        {
            var mainWindowList = Application.Current.Windows.OfType<MainWindow>(); // 尝试查找主窗口
            if (mainWindowList != null)
            {
                foreach (MainWindow mainWindow in mainWindowList) // 遍历主窗口列表
                {
                    mainWindow.UpdateButtonContent(buttonID, tableName); // 更新主窗口按钮
                }
            }
        }
        #endregion

        #region 动作创建
        // 启动软件
        private void StartApp(object sender, RoutedEventArgs e)
        {
            haveAction = true; // 有动作
            base.HideMainWindow(); // 隐藏操作菜单窗口
            AddActionWindow addWindow = new(ButtonID, TableName, 1); // 传递当前按钮和类型
            addWindow.Show(); // 显示窗口
        }

        // 打开文件
        private void OpenFile(object sender, RoutedEventArgs e)
        {
            haveAction = true; // 有动作
            base.HideMainWindow(); // 隐藏操作菜单窗口
            AddActionWindow addWindow = new(ButtonID, TableName, 2); // 传递当前按钮和类型
            addWindow.Show(); // 显示窗口
        }

        // 打开文件夹
        private void OpenFolder(object sender, RoutedEventArgs e)
        {
            haveAction = true; // 有动作
            base.HideMainWindow(); // 隐藏操作菜单窗口
            AddActionWindow addWindow = new(ButtonID, TableName, 3); // 传递当前按钮和类型
            addWindow.Show(); // 显示窗口
        }

        // 打开网址
        private void OpenWebsite(object sender, RoutedEventArgs e)
        {
            haveAction = true; // 有动作
            base.HideMainWindow(); // 隐藏操作菜单窗口
            AddActionWindow addWindow = new(ButtonID, TableName, 4); // 传递当前按钮和类型
            addWindow.Show(); // 显示窗口
        }

        // 创建打开文件动作
        private void CreatOpenFileAction(object sender, RoutedEventArgs e)
        {
            if (buttonManager.CreateFileActionFromClipboard(ButtonID, TableName))
            {
                UpdateUIAfterActionChange(); // 更新UI
                base.Close(); // 关闭窗口
            }
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
        private void ProcessImageDrop(string filePath)
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
                ButtonID = ButtonID,
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
            db2.UpdateAction(buttonData, TableName); // 添加按钮数据到数据库
            UpdateUIAfterActionChange(); // 更新UI
        }

        // 点击按钮导入动作数据
        private void ImportActionData(object sender, RoutedEventArgs e)
        {
            close = false;
            Microsoft.Win32.OpenFileDialog openFileDialog = new(); // 创建文件对话框
            openFileDialog.Filter = "动作数据文件|*.json"; // 设置文件类型
            if (openFileDialog.ShowDialog() == true) // 显示文件对话框并选择文件
                db2.ImportJsonDataToList(TableName, openFileDialog.FileName, ButtonID); // 导入动作数据
            UpdateUIAfterActionChange(); // 更新UI
            close = true; // 关闭窗口
            base.Close(); // 关闭窗口
        }

        // 加载扩展
        private void LoadExtension(object sender, RoutedEventArgs e)
        {
            haveAction = true; // 有动作
            base.HideMainWindow(); // 隐藏操作菜单窗口
            AddActionWindow addWindow = new(ButtonID, TableName, 5); // 传递当前按钮和类型
            addWindow.Show(); // 显示窗口
        }
        #endregion

        #region 窗口事件处理
        // 重写基类的失焦处理方法
        protected override void HandleDeactivated()
        {
            if (close) 
            {
                if (!haveAction)
                {
                    using var windowMananger = new WindowManager(); // 创建窗口管理器
                    windowMananger.SetMainWindowFocused(); // 关闭窗口
                }
            }
            // 调用基类方法以触发ClosingOrHiding事件
            base.HandleDeactivated();
        }
        #endregion

        #region 资源释放
        // 关闭窗口前释放资源
        protected override void OnClosed(EventArgs e)
        {
            // 清理特定资源
            currentInternalCommand = null; // 清理内部命令
            internalCommandManager.CommandPublished -= InternalCommandManager_CommandPublished; // 解绑事件
            hasChanged = false; // 清理检查状态
            buttonManager.Dispose(); // 释放按钮管理器
            ButtonID = 0; // 清理当前按钮
            TableName = null; // 清理表名
            haveAction = false; // 清理是否有动作
            close = false; // 清理关闭状态

            GC.Collect(); // 强制垃圾回收
            GC.WaitForPendingFinalizers(); // 等待垃圾回收完成
            GC.Collect(); // 再次强制垃圾回收

            base.OnClosed(e); // 调用基类的 OnClosed 方法
        }
        #endregion
    }
}