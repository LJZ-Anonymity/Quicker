using Quicker.Windows.FloatingWindows.Windows;
using Quicker.Windows.MainWindows.MainWindow;
using System.Runtime.InteropServices;
using System.Windows.Media.Imaging;
using Quicker.Windows.MainWindows;
using Quicker.Windows.ToolWindows;
using Quicker.Windows.AddWindows;
using System.Windows.Threading;
using System.Windows.Controls;
using Quicker.Database.Core;
using System.Windows.Media;
using System.Windows.Input;
using Quicker.Internal;
using Quicker.Managers;
using System.Windows;
using Quicker.Models;
using System.IO;

namespace Quicker.Windows.Menus
{
    public partial class OperationMenu : BaseMenuWindow
    {
        #region Win32 API
        [DllImport("shell32.dll", ExactSpelling = true)]
        private static extern void ILFree(IntPtr pidlList); // 释放资源
        [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern IntPtr ILCreateFromPathW(string pszPath); //创建指定文件路径
        [DllImport("shell32.dll")]
        private static extern int SHOpenFolderAndSelectItems(IntPtr pidlList, uint cild, IntPtr children, uint dwFlags); // 打开文件夹并选中文件
        #endregion

        #region 属性和字段
        public int ButtonID { get; private set; } // 当前按钮
        public string TableName { get; private set; } // 表名
        public Window FatherWindow { get; private set; } // 父窗口

        public ICommand CloseFloatCommand { get; private set; }
        public ICommand EditInformationCommand { get; private set; }
        public ICommand CopyActionCommand { get; private set; }
        public ICommand CutActionCommand { get; private set; }
        public ICommand DeleteActionCommand { get; private set; }
        public ICommand PasteIconCommand { get; private set; }
        public ICommand FloatActionCommand { get; private set; }
        public ICommand FloatActionPageCommand { get; private set; }
        public ICommand ExportActionCommand { get; private set; }
        public ICommand CheckInformationCommand { get; private set; }
        public ICommand CopyActionIdCommand { get; private set; }
        public ICommand CopyActionNameCommand { get; private set; }
        public ICommand OpenLocationCommand { get; private set; }

        private readonly ButtonManager buttonManager = new(); // 按钮管理器
        private readonly ActionManager actionManager = new(); // 动作管理器
        private readonly ButtonDatabase db2 = new(); // 按钮数据库
        private bool close = true; // 是否关闭窗口
        #endregion

        #region 初始化

        /// <summary>
        /// 操作菜单
        /// </summary>
        /// <param name="buttonID">按钮ID</param>
        /// <param name="tableName">表名</param>
        public OperationMenu(int buttonID, string tableName, Window window = null)
        {
            InitializeCommands();
            InitializeComponent(); // 初始化窗口
            ChildGrid.Visibility = Visibility.Collapsed; // 隐藏悬浮子菜单
            FirstChildGrid.Visibility = Visibility.Collapsed; // 隐藏子菜单
            SecondChildGrid.Visibility = Visibility.Collapsed; // 隐藏子菜单
            ButtonID = buttonID; // 设置当前按钮
            TableName = tableName; // 设置表名
            FatherWindow = window; // 设置父窗口
            InitializeMenu(); // 初始化菜单
        }

        private void InitializeCommands()
        {
            CloseFloatCommand = new RelayCommand(_ => CloseFloatButton_Click(null, null));
            EditInformationCommand = new RelayCommand(_ => EditeInformation_Click(null, null));
            CopyActionCommand = new RelayCommand(_ => CopyAction_Click(null, null));
            CutActionCommand = new RelayCommand(_ => CutAction_Click(null, null));
            DeleteActionCommand = new RelayCommand(_ => DeleteAction_Click(null, null));
            PasteIconCommand = new RelayCommand(_ => PasteIcon_Click(null, null));
            FloatActionCommand = new RelayCommand(_ => FloatActionButton_Click(null, null));
            FloatActionPageCommand = new RelayCommand(_ => FloatActionPageButton_Click(null, null));
            ExportActionCommand = new RelayCommand(_ => ExportAction_Click(null, null));
            CheckInformationCommand = new RelayCommand(_ => CheckImformation_Click(null, null));
            CopyActionIdCommand = new RelayCommand(_ => CopyActionID_Click(null, null));
            CopyActionNameCommand = new RelayCommand(_ => CopyActionName_Click(null, null));
            OpenLocationCommand = new RelayCommand(_ => OpenLocation_Click(null, null));
        }

        // 重写基类的窗口加载方法
        protected override void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            base.OnWindowLoaded(sender, e); // 调用基类方法处理动画
            base.SetWindowPositionNearMouse(); // 设置窗口位置
            Dispatcher.BeginInvoke(new Action(SetChidGridsMargin), DispatcherPriority.Loaded);
        }

        // 设置子菜单位置
        private void SetChidGridsMargin()
        {
            // 使用PointToScreen获取屏幕坐标，然后转换为相对于窗口的坐标
            Point windowScreen = base.PointToScreen(new Point(0, 0));
            if (FatherWindow is not FloatingActionWindow)
            {
                Point suspendActionScreen = SuspendAction.PointToScreen(new Point(0, 0));
                Point floatActionButtonScreen = FloatActionButton.PointToScreen(new Point(0, 0));
                Point suspendAction = new Point(suspendActionScreen.X - windowScreen.X, suspendActionScreen.Y - windowScreen.Y);
                Point floatActionButton = new Point(floatActionButtonScreen.X - windowScreen.X, floatActionButtonScreen.Y - windowScreen.Y);
                double deltaY1 = suspendAction.Y - floatActionButton.Y;
                ChiildGrid1.Margin = new Thickness(219, 91 + deltaY1, 0, 0); // 设置子菜单位置
            }

            // 获取按钮的绝对位置
            Point otherFunctionScreen = OtherFunction.PointToScreen(new Point(0, 0));
            Point exportActionScreen = ExportAction.PointToScreen(new Point(0, 0));

            Point otherFunctionPoint = new Point(otherFunctionScreen.X - windowScreen.X, otherFunctionScreen.Y - windowScreen.Y);
            Point exportActionPoint = new Point(exportActionScreen.X - windowScreen.X, exportActionScreen.Y - windowScreen.Y);
            double deltaY2 = otherFunctionPoint.Y - exportActionPoint.Y;
            ChiildGrid2.Margin = new Thickness(219, 235 + deltaY2, 0, 0); // 设置子菜单位置
        }

        // 初始化菜单
        private void InitializeMenu()
        {
            AdjustUIForPreviousWindow(); // 根据上一个窗口调整界面
            ButtonData buttonData = db2.GetButtonDataByID(ButtonID, TableName); // 获取按钮数据
            if (buttonData == null) // 按钮数据不存在
            {
                UpdateMainWindowButton(); // 更新主窗口按钮
                return; // 退出
            }
            AdjustUIForButtonType(buttonData); // 根据按钮类型调整界面
            AdjustUIForClipboard(); // 根据剪贴板内容调整界面
        }

        // 更新主窗口按钮
        private void UpdateMainWindowButton()
        {
            MainWindow mainWindow = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault(); // 尝试查找主窗口
            if (mainWindow != null)
                mainWindow.UpdateButtonContent(ButtonID, TableName); // 更新主窗口按钮
        }

        /// <summary>
        /// 根据按钮类型调整界面
        /// </summary>
        /// <param name="buttonData">按钮数据</param>
        private void AdjustUIForButtonType(ButtonData buttonData)
        {
            if (buttonData.ActionType != ActionType.OpenFile &&
                buttonData.ActionType != ActionType.OpenFiles &&
                buttonData.ActionType != ActionType.LoadExtension) 
            {
                MainStackPanel.Children.Remove(OpenLocation); // 移除打开文件或文件夹按钮
                MainGrid.Height -= 25;
            }
        }

        // 根据剪贴板内容调整界面
        private void AdjustUIForClipboard()
        {
            if (!Clipboard.ContainsImage()) // 剪贴板不包含图像
            {
                MainStackPanel.Children.Remove(PasteIcon); // 移除粘贴图标按钮
                MainGrid.Height -= 25; // 设置网格高度
            }
        }

        // 根据上一个窗口调整界面
        private void AdjustUIForPreviousWindow()
        {
            if (FatherWindow is MainWindow || FatherWindow is ActionPageManageWindow || FatherWindow is SearchWindow)
            {
                AdjustUIForMainWindow();
            }
            else if (FatherWindow is FloatingActionPageWindow)
            {
                AdjustUIForFloatingActionPageWindow();
            }
            else
            {
                AdjustUIForOtherWindows();
            }
        }

        /// <summary>
        /// 为主窗口调整界面
        /// </summary>
        private void AdjustUIForMainWindow()
        {
            MainStackPanel.Children.Remove(CloseFloatButton); // 移除关闭浮动按钮
            MainStackPanel.Children.Remove(Rectangle1); // 移除分割线
            EditeInformation.Margin = new Thickness(0, 5, 0, 0); // 调整编辑信息按钮位置
            MainGrid.Height -= 32; // 设置网格高度
        }

        /// <summary>
        /// 为浮动动作页面窗口调整界面
        /// </summary>
        private void AdjustUIForFloatingActionPageWindow()
        {
            if (ButtonID == 0)
            {
                AdjustUIForFloatingActionPageWindowWithButtonIDZero();
            }
            else
            {
                AdjustUIForFloatingActionPageWindowWithNonZeroButtonID();
            }
        }

        /// <summary>
        /// 为浮动动作页面窗口（ButtonID为0）调整界面
        /// </summary>
        private void AdjustUIForFloatingActionPageWindowWithButtonIDZero()
        {
            // 移除除了CloseFloatButton之外的所有元素
            var elementsToRemove = GetElementsToRemoveExceptCloseFloatButton();
            RemoveElementsFromMainStackPanel(elementsToRemove);

            // 调整CloseFloatButton的位置和MainGrid的高度
            CloseFloatButton.Margin = new Thickness(0, 5, 0, 5);
            MainGrid.Height = 35;
        }

        /// <summary>
        /// 为浮动动作页面窗口（ButtonID不为0）调整界面
        /// </summary>
        private void AdjustUIForFloatingActionPageWindowWithNonZeroButtonID()
        {
            MainStackPanel.Children.Remove(CloseFloatButton); // 移除关闭浮动按钮
            MainStackPanel.Children.Remove(Rectangle1); // 移除分割线
            EditeInformation.Margin = new Thickness(0, 5, 0, 0); // 调整编辑信息按钮位置
            MainGrid.Height -= 32; // 设置网格高度
        }

        /// <summary>
        /// 为其他窗口调整界面
        /// </summary>
        private void AdjustUIForOtherWindows()
        {
            MainStackPanel.Children.Remove(SuspendAction); // 移除悬浮动按钮
            MainStackPanel.Children.Remove(Rectangle2); // 移除分割线
            MainGrid.Height -= 32; // 设置网格高度
        }

        /// <summary>
        /// 获取除了CloseFloatButton之外需要移除的元素
        /// </summary>
        /// <returns>需要移除的元素列表</returns>
        private List<UIElement> GetElementsToRemoveExceptCloseFloatButton()
        {
            var elementsToRemove = new List<UIElement>();
            foreach (object element in MainStackPanel.Children)
            {
                if (element is Button button)
                {
                    // 如果是Button，判断是否为CloseFloatButton
                    if (button != CloseFloatButton)
                    {
                        elementsToRemove.Add(button);
                    }
                }
                else if (element is UIElement uiElement)
                {
                    // 如果不是Button但是UIElement，直接添加到移除列表
                    elementsToRemove.Add(uiElement);
                }
            }
            return elementsToRemove;
        }

        /// <summary>
        /// 从MainStackPanel中移除指定的元素
        /// </summary>
        /// <param name="elementsToRemove">需要移除的元素列表</param>
        private void RemoveElementsFromMainStackPanel(List<UIElement> elementsToRemove)
        {
            foreach (var element in elementsToRemove)
            {
                MainStackPanel.Children.Remove(element);
            }
        }

        #endregion

        #region 动作管理
        // 编辑动作信息
        private void EditeInformation_Click(object sender, RoutedEventArgs e)
        {
            base.HideMainWindow(); // 隐藏操作菜单窗口
            AddActionWindow addWindow = new(ButtonID, TableName, 0); // 创建添加动作窗口
            addWindow.Show(); // 显示添加动作窗口
            base.CloseMainWindow(); // 关闭操作菜单窗口
        }

        // 复制动作
        private void CopyAction_Click(object sender, RoutedEventArgs e)
        {
            base.Visibility = Visibility.Hidden; // 隐藏窗口
            InternalCommandManager.Instance.PublishCommand(new InternalCommand
            {
                CommandType = InternalCommandType.CopyAction,
                TableName = TableName,
                ButtonId = ButtonID
            });
            base.Close(); // 关闭窗口
        }

        // 剪切动作
        private void CutAction_Click(object sender, RoutedEventArgs e)
        {
            base.Visibility = Visibility.Hidden; // 隐藏窗口
            InternalCommandManager.Instance.PublishCommand(new InternalCommand
            {
                CommandType = InternalCommandType.CutAction,
                TableName = TableName,
                ButtonId = ButtonID
            });
            base.Close(); // 关闭窗口
        }

        // 删除动作
        private void DeleteAction_Click(object sender, RoutedEventArgs e)
        {
            base.Visibility = Visibility.Hidden; // 隐藏窗口
            db2.DeleteAction(ButtonID, TableName); // 删除动作
            UpdateUIAfterActionDelete(); // 更新UI
            base.Close(); // 关闭窗口
        }

        // 更新UI（删除动作后）
        private void UpdateUIAfterActionDelete()
        {
            ActionPageManageWindow actionPageManageWindow = Application.Current.Windows.OfType<ActionPageManageWindow>().FirstOrDefault(); // 尝试查找现有的菜单栏
            if (actionPageManageWindow != null)
                actionPageManageWindow.UpdateButton(ButtonID); // 更新菜单栏按钮

            var mainWindowList = Application.Current.Windows.OfType<MainWindow>(); // 尝试查找主窗口
            if (mainWindowList != null)
            {
                foreach (MainWindow mainWindow in mainWindowList) // 遍历主窗口列表
                {
                    mainWindow.UpdateButtonContent(ButtonID, TableName); // 更新主窗口按钮
                }
            }

            var floatingActionPageWindowList = Application.Current.Windows.OfType<FloatingActionPageWindow>();
            if (floatingActionPageWindowList != null)
            {
                foreach (FloatingActionPageWindow floatingActionPageWindow in floatingActionPageWindowList) // 遍历浮动动作页面窗口列表
                {
                    floatingActionPageWindow.LoadButtonData(); // 重新加载Button数据
                }
            }

            var searchWindow = Application.Current.Windows.OfType<SearchWindow>().FirstOrDefault(); // 尝试查找搜索窗口
            if (searchWindow != null)
                searchWindow.DeleteButton(); // 更新搜索窗口
        }

        // 导出动作数据到指定文件夹
        private void ExportAction_Click(object sender, RoutedEventArgs e)
        {
            close = false; // 屏蔽失焦
            try
            {
                base.HideMainWindow(); // 隐藏操作菜单窗口

                // 默认文件名：动作标题_yyyyMMdd_HHmmss.json
                ButtonData buttonData = db2.GetButtonDataByID(ButtonID, TableName);
                string defaultFileName = $"{(buttonData?.Title ?? "Action")}_{DateTime.Now:yyyyMMdd_HHmmss}.json";

                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "导出动作",
                    Filter = "json文件 (*.json)|*.json|所有文件 (*.*)|*.*",
                    DefaultExt = ".json",
                    FileName = defaultFileName,
                    AddExtension = true,
                    OverwritePrompt = true
                };

                if (dialog.ShowDialog() == true)
                {
                    string folderPath = Path.GetDirectoryName(dialog.FileName);
                    db2.ExportActionDataToJson(TableName, ButtonID, folderPath);
                }
            }
            finally // 恢复
            {
                close = true;
            }

            base.Close();
        }

        // 点击按钮关闭悬浮动作窗口
        private void CloseFloatButton_Click(object sender, RoutedEventArgs e)
        {
            FatherWindow.Close(); // 关闭父窗口
            base.Close(); // 关闭窗口
        }

        private void FloatActionButton_Click(object sender, RoutedEventArgs e)
        {
            base.Visibility = Visibility.Hidden; // 隐藏窗口
            ButtonData buttonData = db2.GetButtonDataByID(ButtonID, TableName); // 获取按钮数据
            if (buttonData != null) // 按钮数据不为空
            {
                FloatingActionWindow floatingActionWindow = new(ButtonID, TableName); // 创建悬浮动作窗口
                floatingActionWindow.Show(); // 显示悬浮动作窗口
            }
            base.Close(); // 关闭窗口
        }

        private void FloatActionPageButton_Click(object sender, RoutedEventArgs e)
        {
            base.Visibility = Visibility.Hidden; // 隐藏窗口
            int actionPageIndex = ButtonID / 100; // 获取动作页面索引
            FloatingActionPageWindow floatingActionPageWindow = new(actionPageIndex, TableName); // 创建悬浮动作窗口
            floatingActionPageWindow.Show(); // 显示悬浮动作窗口
            base.Close(); // 关闭窗口
        }

        #endregion

        #region 信息复制
        // 粘贴剪贴板图标为动作图标
        private void PasteIcon_Click(object sender, RoutedEventArgs e)
        {
            if (Clipboard.ContainsImage()) // 剪贴板包含图像
            {
                BitmapSource bitmapSource = Clipboard.GetImage(); // 获取图像
                if (bitmapSource != null) // 图像不为空
                {
                    var iconManager = new IconManager(); // 创建图标管理器
                    string iconPath = iconManager.SaveIconToFile(bitmapSource); // 保存图像到文件
                    if (!string.IsNullOrEmpty(iconPath)) // 图像路径不为空
                    {
                        ButtonData buttonData = db2.GetButtonDataByID(ButtonID, TableName); // 获取按钮数据
                        if (buttonData != null) // 按钮数据不为空
                        {
                            buttonData.ImagePath = iconPath; // 更新按钮数据
                            db2.UpdateAction(buttonData, TableName); // 更新按钮数据
                            UpdateUIAfterActionDelete(); // 更新UI
                        }
                    }
                    base.Close(); // 关闭窗口
                }
            }
        }
        
        // 查看动作信息
        private void CheckImformation_Click(object sender, RoutedEventArgs e)
        {
            ActionInfoWindow actionInfoWindow = new(ButtonID, TableName); // 创建动作信息窗口
            MainWindow mainWindow = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault(); // 尝试查找主窗口
            actionInfoWindow.ShowDialog(); // 显示动作信息窗口
            base.Close(); // 关闭操作菜单窗口
        }

        // 复制动作名称
        private void CopyActionName_Click(object sender, RoutedEventArgs e)
        {
            ButtonData data = db2.GetButtonDataByID(ButtonID, TableName); // 获取按钮数据
            Clipboard.SetText(data.Title); // 复制文本到剪贴板
            actionManager.ShowToast("已复制。", ToastType.Success); // 弹出消息提醒
            base.Close(); // 关闭窗口
        }

        // 复制动作ID
        private void CopyActionID_Click(object sender, RoutedEventArgs e)
        {
            ButtonData data = db2.GetButtonDataByID(ButtonID, TableName); // 获取按钮数据
            Clipboard.SetText($"{data.ButtonID}"); // 复制文本到剪贴板
            actionManager.ShowToast("动作ID已复制。", ToastType.Success); // 弹出消息提醒
            base.Close(); // 关闭窗口
        }
        #endregion

        #region 文件操作
        // 在资源管理器中打开文件或文件夹
        private void OpenLocation_Click(object sender, RoutedEventArgs e)
        {
            base.HideMainWindow(); // 隐藏操作菜单窗口
            ButtonData buttonData = db2.GetButtonDataByID(ButtonID, TableName); // 获取按钮数据
            if (buttonData == null)
            {
                actionManager.ShowToast("按钮数据不存在", ToastType.Error);
                return;
            }

            try
            {
                // 根据动作类型调用ActionManager的相应方法
                switch (buttonData.ActionType)
                {
                    case ActionType.OpenFile:
                    case ActionType.LoadExtension:
                        OpenFileInExplorer(buttonData.Location);
                        break;
                    case ActionType.OpenFiles:
                        OpenMultiplePathsInExplorer(buttonData.Location);
                        break;
                    default:
                        actionManager.ShowToast($"不支持此动作类型：{buttonData.ActionType}", ToastType.Error);
                        break;
                }
            }
            catch (Exception ex)
            {
                actionManager.ShowToast($"打开路径失败：{ex.Message}", ToastType.Error);
            }
            finally
            {
                base.CloseMainWindow(); // 关闭操作菜单窗口
            }
        }

        /// <summary>
        /// 在资源管理器中打开单个文件或文件夹
        /// </summary>
        /// <param name="path">文件或文件夹路径</param>
        private void OpenFileInExplorer(string path)
        {
            // 检查路径是否存在（文件或文件夹）
            if (!File.Exists(path) && !Directory.Exists(path))
            {
                actionManager.ShowToast($"路径不存在：{path}", ToastType.Error);
                return;
            }

            IntPtr pidlList = ILCreateFromPathW(path);
            try
            {
                Marshal.ThrowExceptionForHR(SHOpenFolderAndSelectItems(pidlList, 0, IntPtr.Zero, 0));
            }
            finally
            {
                ILFree(pidlList);
            }
        }

        /// <summary>
        /// 在资源管理器中打开多个文件或文件夹
        /// </summary>
        /// <param name="paths">文件或文件夹路径字符串，用分号分隔</param>
        private void OpenMultiplePathsInExplorer(string paths)
        {
            var pathList = paths.Split(';').Select(p => p.Trim()).Where(p => !string.IsNullOrEmpty(p)).ToList();
            
            if (pathList.Count == 0)
            {
                actionManager.ShowToast("没有有效的路径", ToastType.Error);
                return;
            }

            // 检查路径是否在同一目录
            if (ArePathsInSameDirectory(pathList))
            {
                OpenMultiplePathsInSameDirectory(pathList);
            }
            else
            {
                OpenMultiplePathsInDifferentDirectories(pathList);
            }
        }

        /// <summary>
        /// 检查文件或文件夹是否在同一目录
        /// </summary>
        /// <param name="paths">文件或文件夹路径列表</param>
        /// <returns>是否在同一目录</returns>
        private bool ArePathsInSameDirectory(List<string> paths)
        {
            if (paths.Count == 0)
                return false;

            // 获取所有路径的父目录
            var parentDirectories = new List<string>();
            foreach (string path in paths)
            {
                if (File.Exists(path))
                {
                    parentDirectories.Add(Path.GetDirectoryName(path));
                }
                else if (Directory.Exists(path))
                {
                    parentDirectories.Add(Path.GetDirectoryName(path));
                }
                else
                {
                    return false; // 路径不存在
                }
            }

            if (parentDirectories.Count == 0)
                return false;

            // 检查所有父目录是否相同
            string firstParentDirectory = parentDirectories[0];
            foreach (string parentDirectory in parentDirectories)
            {
                if (!string.Equals(parentDirectory, firstParentDirectory, StringComparison.OrdinalIgnoreCase))
                {
                    return false; // 不在同一目录
                }
            }

            return true;
        }

        /// <summary>
        /// 打开同一目录下的多个文件或文件夹并在资源管理器中选中
        /// </summary>
        /// <param name="paths">文件或文件夹路径列表</param>
        private void OpenMultiplePathsInSameDirectory(List<string> paths)
        {
            try
            {
                // 获取公共目录
                string commonDirectory = GetCommonDirectory(paths);
                if (string.IsNullOrEmpty(commonDirectory))
                {
                    actionManager.ShowToast("无法确定公共目录", ToastType.Error);
                    return;
                }

                // 创建并打开资源管理器窗口
                OpenExplorerWithSelectedItems(commonDirectory, paths);
            }
            catch (Exception ex)
            {
                actionManager.ShowToast($"打开路径失败：{ex.Message}", ToastType.Error);
            }
        }

        /// <summary>
        /// 创建并打开资源管理器窗口，选中指定项目
        /// </summary>
        /// <param name="folderPath">要打开的文件夹路径</param>
        /// <param name="itemPaths">要选中的项目路径列表</param>
        private void OpenExplorerWithSelectedItems(string folderPath, List<string> itemPaths)
        {
            IntPtr pidlFolder = ILCreateFromPathW(folderPath);
            try
            {
                // 创建项目PIDL列表
                List<IntPtr> pidlItems = CreatePidlList(itemPaths);
                if (pidlItems.Count == 0)
                {
                    actionManager.ShowToast("没有有效的路径", ToastType.Error);
                    return;
                }

                // 打开资源管理器并选中项目
                OpenExplorerAndSelectItems(pidlFolder, pidlItems);
            }
            finally
            {
                ILFree(pidlFolder);
            }
        }

        /// <summary>
        /// 创建项目PIDL列表
        /// </summary>
        /// <param name="paths">项目路径列表</param>
        /// <returns>PIDL列表</returns>
        private List<IntPtr> CreatePidlList(List<string> paths)
        {
            List<IntPtr> pidlItems = new List<IntPtr>();
            
            foreach (string path in paths)
            {
                IntPtr pidlItem = CreatePidlForPath(path);
                if (pidlItem != IntPtr.Zero)
                {
                    pidlItems.Add(pidlItem);
                }
            }
            
            return pidlItems;
        }

        /// <summary>
        /// 为单个路径创建PIDL
        /// </summary>
        /// <param name="path">路径</param>
        /// <returns>PIDL指针</returns>
        private IntPtr CreatePidlForPath(string path)
        {
            // 检查路径是否存在
            if (!File.Exists(path) && !Directory.Exists(path))
            {
                actionManager.ShowToast($"路径不存在：{path}", ToastType.Error);
                return IntPtr.Zero;
            }
            
            IntPtr pidlItem = ILCreateFromPathW(path);
            if (pidlItem == IntPtr.Zero)
            {
                actionManager.ShowToast($"无法获取路径的 PIDL：{path}", ToastType.Error);
                return IntPtr.Zero;
            }
            
            return pidlItem;
        }

        /// <summary>
        /// 打开资源管理器并选中指定项目
        /// </summary>
        /// <param name="pidlFolder">文件夹PIDL</param>
        /// <param name="pidlItems">项目PIDL列表</param>
        private void OpenExplorerAndSelectItems(IntPtr pidlFolder, List<IntPtr> pidlItems)
        {
            IntPtr pidlArray = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(IntPtr)) * pidlItems.Count);
            try
            {
                // 将PIDL列表写入内存
                WritePidlArrayToMemory(pidlArray, pidlItems);
                
                // 打开资源管理器并选中项目
                Marshal.ThrowExceptionForHR(SHOpenFolderAndSelectItems(pidlFolder, (uint)pidlItems.Count, pidlArray, 0));
            }
            finally
            {
                // 释放资源
                Marshal.FreeHGlobal(pidlArray);
                FreePidlItems(pidlItems);
            }
        }

        /// <summary>
        /// 将PIDL列表写入内存
        /// </summary>
        /// <param name="pidlArray">内存指针</param>
        /// <param name="pidlItems">PIDL列表</param>
        private void WritePidlArrayToMemory(IntPtr pidlArray, List<IntPtr> pidlItems)
        {
            for (int i = 0; i < pidlItems.Count; i++)
            {
                Marshal.WriteIntPtr(pidlArray, i * Marshal.SizeOf(typeof(IntPtr)), pidlItems[i]);
            }
        }

        /// <summary>
        /// 释放PIDL项目列表
        /// </summary>
        /// <param name="pidlItems">PIDL列表</param>
        private void FreePidlItems(List<IntPtr> pidlItems)
        {
            foreach (IntPtr pidlItem in pidlItems)
            {
                ILFree(pidlItem);
            }
        }

        /// <summary>
        /// 获取多个路径的公共目录
        /// </summary>
        /// <param name="paths">路径列表</param>
        /// <returns>公共目录</returns>
        private string GetCommonDirectory(List<string> paths)
        {
            if (paths.Count == 0)
                return string.Empty;

            // 获取所有路径的父目录
            var parentDirectories = new List<string>();
            foreach (string path in paths)
            {
                if (File.Exists(path))
                {
                    parentDirectories.Add(Path.GetDirectoryName(path));
                }
                else if (Directory.Exists(path))
                {
                    parentDirectories.Add(Path.GetDirectoryName(path));
                }
            }

            if (parentDirectories.Count == 0)
                return string.Empty;

            // 检查所有父目录是否相同
            string firstParentDirectory = parentDirectories[0];
            foreach (string parentDirectory in parentDirectories)
            {
                if (!string.Equals(parentDirectory, firstParentDirectory, StringComparison.OrdinalIgnoreCase))
                {
                    // 如果不相同，需要找到公共父目录
                    return FindCommonPrefix(parentDirectories);
                }
            }

            return firstParentDirectory;
        }

        /// <summary>
        /// 找到多个路径的公共前缀
        /// </summary>
        /// <param name="paths">路径列表</param>
        /// <returns>公共前缀</returns>
        private string FindCommonPrefix(List<string> paths)
        {
            if (paths.Count == 0)
                return string.Empty;

            string commonPrefix = paths[0];
            
            foreach (string path in paths.Skip(1))
            {
                int commonLength = 0;
                int minLength = Math.Min(commonPrefix.Length, path.Length);
                
                for (int i = 0; i < minLength; i++)
                {
                    if (char.ToUpperInvariant(commonPrefix[i]) == char.ToUpperInvariant(path[i]))
                    {
                        commonLength++;
                    }
                    else
                    {
                        break;
                    }
                }
                
                // 确保在目录分隔符处截断
                while (commonLength > 0 && commonPrefix[commonLength - 1] != '\\')
                {
                    commonLength--;
                }
                
                commonPrefix = commonPrefix.Substring(0, commonLength);
            }
            
            return commonPrefix;
        }

        /// <summary>
        /// 分别打开不同目录下的文件或文件夹并在各自的资源管理器窗口中选中
        /// </summary>
        /// <param name="paths">文件或文件夹路径列表</param>
        private void OpenMultiplePathsInDifferentDirectories(List<string> paths)
        {
            try
            {
                foreach (string path in paths) // 遍历所有路径
                {
                    // 检查路径是否存在
                    if (!File.Exists(path) && !Directory.Exists(path))
                    {
                        actionManager.ShowToast($"路径不存在：{path}", ToastType.Error);
                        continue; // 跳过当前路径
                    }
                    
                    IntPtr pidlList = ILCreateFromPathW(path); // 获取路径的 PIDL
                    try
                    {
                        Marshal.ThrowExceptionForHR(SHOpenFolderAndSelectItems(pidlList, 0, IntPtr.Zero, 0)); // 打开路径所在目录并选中路径
                    }
                    catch (Exception ex)
                    {
                        actionManager.ShowToast($"打开路径失败：{ex.Message}", ToastType.Error); // 显示错误消息
                    }
                    finally
                    {
                        ILFree(pidlList); // 释放 PIDL 资源
                    }
                }
            }
            catch (Exception ex)
            {
                actionManager.ShowToast($"打开路径失败：{ex.Message}", ToastType.Error); // 显示错误消息
            }
        }
        #endregion

        #region UI交互

        // 重写基类的失焦处理方法
        protected override void HandleDeactivated()
        {
            if (!close) return;

            using var windowMananger = new WindowManager();
            windowMananger.SetMainWindowFocused();
            base.HandleDeactivated();
        }

        // 鼠标移入显示子菜单
        private void SuspendAction_MouseEnter(object sender, MouseEventArgs e)
        {
            SuspendAction.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFEAEAEA")); // 改变背景颜色
            ChildGrid.Visibility = Visibility.Visible; // 显示子菜单
        }

        // 鼠标移出关闭子菜单
        private void SuspendAction_MouseLeave(object sender, MouseEventArgs e)
        {
            if (ChildGrid.IsMouseOver) return;
            SuspendAction.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("White"));
            ChildGrid.Visibility = Visibility.Collapsed; // 隐藏子菜单
        }

        // 鼠标移出关闭子菜单
        private void ChildGrid_MouseLeave(object sender, MouseEventArgs e)
        {
            if (!MainGrid.IsMouseOver && !ChildGrid.IsMouseOver && !OtherFunction.IsMouseOver)
            {
                SuspendAction.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("White")); // 还原背景颜色
                ChildGrid.Visibility = Visibility.Collapsed; // 隐藏子菜单
            }
        }

        // 鼠标移入显示子菜单
        private void OtherFunction_MouseEnter(object sender, MouseEventArgs e)
        {
            OtherFunction.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFEAEAEA")); // 改变背景颜色
            FirstChildGrid.Visibility = Visibility.Visible; // 显示子菜单
        }

        // 鼠标移出关闭子菜单
        private void OtherFunction_MouseLeave(object sender, MouseEventArgs e)
        {
            if (FirstChildGrid.IsMouseOver) return;
            OtherFunction.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("White"));
            FirstChildGrid.Visibility = Visibility.Collapsed; // 隐藏子菜单
        }

        // 鼠标移入显示子菜单
        private void CopyActionInfo_MouseEnter(object sender, MouseEventArgs e)
        {
            CopyActionInfo.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFEAEAEA")); // 改变背景颜色
            SecondChildGrid.Visibility = Visibility.Visible; // 显示子菜单
        }

        // 鼠标移出关闭子菜单
        private void CopyActionInfo_MouseLeave(object sender, MouseEventArgs e)
        {
            if (!SecondChildGrid.IsMouseOver)
            {
                CopyActionInfo.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("White")); // 还原背景颜色
                SecondChildGrid.Visibility = Visibility.Collapsed; // 隐藏子菜单
            }
        }

        // 鼠标移出关闭子菜单
        private void SecondChildGrid_MouseLeave(object sender, MouseEventArgs e)
        {
            CopyActionInfo.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("White")); // 还原背景颜色
            SecondChildGrid.Visibility = Visibility.Collapsed; // 隐藏子菜单
        }

        // 鼠标移出关闭子菜单
        private void FirstChildGrid_MouseLeave(object sender, MouseEventArgs e)
        {
            if (!MainGrid.IsMouseOver && !SecondChildGrid.IsMouseOver && !OtherFunction.IsMouseOver)
            {
                OtherFunction.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("White")); // 还原背景颜色
                FirstChildGrid.Visibility = Visibility.Collapsed; // 隐藏子菜单
            }
        }

        #endregion

        #region 资源释放
        // 关闭窗口前释放资源
        protected override void OnClosed(EventArgs e)
        {
            // 清理特定资源
            buttonManager.Dispose(); // 释放按钮管理器资源
            actionManager.Dispose(); // 释放动作管理器资源
            close = false; // 设置关闭标识符
            GC.Collect(); // 强制垃圾回收
            GC.WaitForPendingFinalizers(); // 等待垃圾回收完成
            GC.Collect(); // 再次强制垃圾回收

            base.OnClosed(e); // 调用基类的 OnClosed 方法
        }
        #endregion
    }

    internal sealed class RelayCommand : ICommand
    {
        private readonly Action<object> execute; // 定义执行命令的操作
        private readonly Predicate<object> canExecute; // 定义判断命令是否可执行的条件

        /// <summary>
        /// 构造函数，初始化执行命令的操作和可执行条件
        /// </summary>
        /// <param name="execute"> 执行命令的操作 </param>
        /// <param name="canExecute"> 判断命令是否可执行的条件 </param>
        /// <exception cref="ArgumentNullException"> execute 为 null </exception>
        public RelayCommand(Action<object> execute, Predicate<object> canExecute = null)
        {
            this.execute = execute ?? throw new ArgumentNullException(nameof(execute));
            this.canExecute = canExecute;
        }

        public bool CanExecute(object parameter) => canExecute?.Invoke(parameter) ?? true; // 判断命令是否可执行
        public void Execute(object parameter) => execute(parameter); // 执行命令

        // 命令可执行状态改变事件，使用CommandManager.RequerySuggested事件
        public event EventHandler CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }
}