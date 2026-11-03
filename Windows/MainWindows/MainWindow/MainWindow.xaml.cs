using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using VisualTreeHelper = Quicker.Helpers.VisualTreeHelper;
using DragEventArgs = System.Windows.DragEventArgs;
using Button = System.Windows.Controls.Button;
using Panel = System.Windows.Controls.Panel;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Quicker.Windows.ToolWindows;
using Quicker.Windows.AddWindows;
using System.Windows.Controls;
using System.Threading.Tasks;
using Quicker.Windows.Menus;
using Quicker.Database.Core;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Forms;
using Quicker.Managers;
using Quicker.Helpers;
using System.Windows;
using Quicker.Models;
using System.IO;

namespace Quicker.Windows.MainWindows.MainWindow
{
    public partial class MainWindow : Window
    {
        #region 字段与构造

        // 常量定义
        private const string DEFAULT_STYLE = "common";
        private const string GLOBAL_STYLE = "_global";

        private SolidColorBrush SelectedBrush => (this.DataContext as MainWindowViewModel).SelectedBrush;
        private SolidColorBrush UnSelectedBrush => (this.DataContext as MainWindowViewModel).UnSelectedBrush;
        public bool ShouldCenterAtMouseOnShow { get; set; } = false; // 本次唤起是否需要居中到鼠标
        public bool ShouldCenterOnScreenOnShow { get; set; } = false; // 本次唤起是否需要居中屏幕
        public bool IsHiddenOrInvisible => _isSilentlyHidden || !IsVisible;

        private readonly CancellationTokenSource cancellationTokenSource = new(); // 取消后台任务的令牌源
        private Quicker.Models.Settings.Appearance _latestAppearance;
        private int GloblePageIndex = 0, CommonPageIndex = 0; // 全局页面、通用页面索引
        public readonly ButtonManager buttonManager = new(); // 按钮管理器
        private readonly IconManager iconManager = new(); // 图标管理器
        private readonly ActionPageDatabase db3 = new(); // 动作页面数据库
        private readonly HashSet<string> _dirty = new(); // 脏位集合
        private readonly ButtonDatabase db2 = new(); // 按钮数据库
        private readonly object _dirtyLock = new(); // 保护 _dirty 的锁
        private bool isInitialized = false; // 初始化标志
        private bool _isShutdown = false; // 资源是否已释放
        private bool _isSilentlyHidden;
        private bool _refreshScheduled; // 刷新调度中
        private bool _isSelfTriggered; // 自身触发标志
        private string CommonStyle;

        public MainWindow()
        {
            InitializeComponent();
        }

        #endregion

        #region 初始化与入口

        /// <summary>
        /// 初始化主窗口
        /// </summary>
        public void Initialize()
        {
            if (isInitialized) return;
            isInitialized = true;

            CommonStyle = AppStateManager.CommonState ?? DEFAULT_STYLE;

            DataContext = new MainWindowViewModel();
            var vm = DataContext as MainWindowViewModel;

            iconManager.SetImageWithGifSupport(BackgroundImage, vm.BackgroundImagePath);
            SubscribeStateEvents(); // 绑定UI更新事件
            InitializePageGrids();
            CheckUpdate();
        }

        /// <summary>
        /// 通用类型改变事件
        /// </summary>
        /// <param name="style">样式名称</param>
        public void OnCommonStyleChanged(string style)
        {
            CommonStyle = db2.TableExists(style) ? style : DEFAULT_STYLE; // 设置样式
            CommonGrid.Children.Clear();
            SetCommonTextBlock(0);
            GeneratePageGrid(CommonGrid, CommonStyle, 0, 4, 4);
            GenerateButtons();

            // 不再强制锁定，改为同步现有状态
            if (DataContext is MainWindowViewModel vm)
                vm.IsLocked = AppStateManager.Locked;
        }

        // 初始化动作页面
        private void InitializePageGrids()
        {
            CommonStyle = db2.TableExists(CommonStyle) ? CommonStyle : DEFAULT_STYLE; // 设置样式
            GeneratePageGrid(GlobalGrid, GLOBAL_STYLE, 0, 3, 4);
            GeneratePageGrid(CommonGrid, CommonStyle, 0, 4, 4);
            GenerateButtons(); // 生成按钮
            SetCommonTextBlock(0);
        }

        // 检查更新
        private void CheckUpdate()
        {
            if (!AppStateManager.HasNewVersion) // 如果没有新版本
            {
                HasNewVersionTip.Visibility = Visibility.Collapsed; // 隐藏提示
                TitlePop.Height -= 25; // 减少高度
                UpdateButton.Visibility = Visibility.Collapsed; // 隐藏更新按钮
            }
        }

        /// <summary>
        /// 设置通用标签内容
        /// </summary>
        /// <param name="GridIndex"> 通用网格索引 </param>
        private void SetCommonTextBlock(int GridIndex)
        {
            var actionPageData = db3.GetActionPageData(CommonStyle, GridIndex); // 从数据库中获取通用动作页面数据
            CommonTextBlock.Text = actionPageData.ActionPageName; // 设置通用标签内容
            CommonTextBlock.ToolTip = actionPageData.ActionPageName; // 设置通用标签提示
        }

        // 设置窗口状态
        private void SetMainWindowState()
        {
            using var wm = new WindowManager(); // 创建窗口管理器
            wm.SetWindowTopmost(this); // 设置窗口置顶
        }

        /// <summary>
        /// 初始化窗口位置
        /// </summary>
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            if (ShouldCenterAtMouseOnShow)
            {
                using var wm = new WindowManager();
                wm.SetWindowCenterAtMouse(this);
                ShouldCenterAtMouseOnShow = false;
            }
            else if (ShouldCenterOnScreenOnShow) // 首次唤起且强制居中屏幕
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
                var wa = SystemParameters.WorkArea;
                // 首次此时尚未渲染，用 Width/Height 计算
                this.Left = wa.Left + (wa.Width - this.Width) / 2;
                this.Top = wa.Top + (wa.Height - this.Height) / 2;
                ShouldCenterOnScreenOnShow = false;
            }
        }

        #endregion

        #region 状态监听与UI刷新

        private void SubscribeStateEvents()
        {
            AppStateManager.AppearanceChanged += OnAppearanceChanged;
            AppStateManager.ConventionsChanged += OnConventionsChanged;
            AppStateManager.ActionPageChanged += OnActionPageChanged;
            AppStateManager.SceneListChanged += OnSceneListChanged;
            AppStateManager.LockStateChanged += OnLockStateChanged;
        }

        private void UnsubscribeStateEvents()
        {
            AppStateManager.AppearanceChanged -= OnAppearanceChanged;
            AppStateManager.ConventionsChanged -= OnConventionsChanged;
            AppStateManager.ActionPageChanged -= OnActionPageChanged;
            AppStateManager.SceneListChanged -= OnSceneListChanged;
            AppStateManager.LockStateChanged -= OnLockStateChanged;
        }

        private void OnAppearanceChanged(Quicker.Models.Settings.Appearance appearance)
        {
            if (appearance != null) _latestAppearance = appearance; // 记录最新对象
            MarkDirty("appearance");
            ScheduleRefresh();
        }
        private void OnConventionsChanged() { MarkDirty("conventions"); ScheduleRefresh(); }
        private void OnActionPageChanged(string table)
        {
            if (_isSelfTriggered) return;
            MarkDirty($"page:{table}");
            ScheduleRefresh();
        }

        private void OnSceneListChanged(string table)
        {
            if (_isSelfTriggered) return;
            MarkDirty($"scenes:{table}");
            ScheduleRefresh();
        }
        private void OnLockStateChanged() { MarkDirty("lock"); ScheduleRefresh(); }

        private void NotifyActionPageChanged(string table)
        {
            _isSelfTriggered = true;
            try { AppStateManager.NotifyActionPageChanged(table); }
            finally { _isSelfTriggered = false; }
        }

        private void NotifySceneListChanged(string table)
        {
            _isSelfTriggered = true;
            try { AppStateManager.NotifySceneListChanged(table); }
            finally { _isSelfTriggered = false; }
        }

        private void MarkDirty(string key)
        {
            lock (_dirtyLock) { _dirty.Add(key); }
        }

        private bool ConsumeDirty(string key)
        {
            lock (_dirtyLock) { return _dirty.Remove(key); }
        }

        private List<string> ConsumeDirtyByPrefix(string prefix)
        {
            lock (_dirtyLock)
            {
                var keys = _dirty
                    .Where(k => k.StartsWith(prefix, StringComparison.Ordinal))
                    .ToList();

                foreach (var k in keys)
                    _dirty.Remove(k);

                // 返回去掉前缀后的表名
                return keys.Select(k => k.Substring(prefix.Length)).ToList();
            }
        }

        private void ClearDirtyByPrefix(params string[] prefixes)
        {
            lock (_dirtyLock)
            {
                foreach (var p in prefixes)
                    _dirty.RemoveWhere(k => k.StartsWith(p, StringComparison.Ordinal));
            }
        }

        private void ScheduleRefresh()
        {
            // 事件可能来自后台线程，先切回 UI 线程
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(ScheduleRefresh));
                return;
            }

            // 同一波通知合并成一次刷新
            if (_refreshScheduled) return;
            _refreshScheduled = true;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                _refreshScheduled = false;

                if (!isInitialized) return; // 首次可见前不刷
                RefreshOnShow(AppStateManager.CommonState ?? DEFAULT_STYLE);
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        /// <summary>
        /// 按脏位增量刷新 UI（在 UI 线程调用）
        /// </summary>
        private void RefreshOnShow(string commonState)
        {
            var vm = DataContext as MainWindowViewModel;
            if (vm == null) return;

            // 场景变化：整块通用区重建
            if (!string.Equals(CommonStyle, commonState, StringComparison.Ordinal))
            {
                OnCommonStyleChanged(commonState);
                ClearDirtyByPrefix("page:", "scenes:"); // 场景变了，原表的脏位已无意义
            }

            // 外观变更
            if (ConsumeDirty("appearance"))
            {
                if (_latestAppearance != null)
                {
                    vm.ApplyAppearance(_latestAppearance); // 直接应用内存里的最新数据
                    _latestAppearance = null;
                }
                else
                {
                    vm.ReloadAppearance(); // 兜底从数据库读取
                }

                try { iconManager.SetImageWithGifSupport(BackgroundImage, vm.BackgroundImagePath); } catch { }

                // 无论尺寸是否变化，强制重建页面
                RebuildAllPages();
            }

            // 行为设置变更
            if (ConsumeDirty("conventions"))
                buttonManager.ReloadConventions();

            // 锁定状态
            if (ConsumeDirty("lock"))
                vm.IsLocked = AppStateManager.Locked;

            // 页面数据变更：按表精确刷新
            foreach (var table in ConsumeDirtyByPrefix("page:"))
            {
                if (string.Equals(table, GLOBAL_STYLE, StringComparison.Ordinal))
                    RebuildPages(GlobalGrid, GLOBAL_STYLE, 3);
                else if (string.Equals(table, CommonStyle, StringComparison.Ordinal))
                    RebuildPages(CommonGrid, CommonStyle, 4);
            }

            // 场景页数变更
            foreach (var table in ConsumeDirtyByPrefix("scenes:"))
            {
                if (string.Equals(table, GLOBAL_STYLE, StringComparison.Ordinal))
                {
                    GenerateButtons();
                    ShowTargetPage(GlobalGrid, GLOBAL_STYLE, GloblePageIndex, 3, 4, GlobalButtonPanel);
                }
                else if (string.Equals(table, CommonStyle, StringComparison.Ordinal))
                {
                    GenerateButtons();
                    ShowTargetPage(CommonGrid, CommonStyle, CommonPageIndex, 4, 4, CommonButtonPanel);
                }
            }
        }

        private void RebuildAllPages()
        {
            RebuildPages(GlobalGrid, GLOBAL_STYLE, 3);
            RebuildPages(CommonGrid, CommonStyle, 4);
        }

        /// <summary>
        /// 重建指定容器当前可见页（保留当前页索引）
        /// </summary>
        private void RebuildPages(Panel parent, string gridType, int rows)
        {
            int visibleIndex = GetVisiblePageGridIndex(parent, gridType);
            parent.Children.Clear();
            GeneratePageGrid(parent, gridType, visibleIndex, rows, 4);
            if (gridType != GLOBAL_STYLE) SetCommonTextBlock(visibleIndex);
        }

        #endregion

        #region 页面与按钮生成

        // 生成页面切换 Button
        private void GenerateButtons()
        {
            // 切换通用动作页时清空
            GlobalButtonPanel.Children.Clear(); // 清空全局动作页切换按钮
            CommonButtonPanel.Children.Clear(); // 清空通用动作页切换按钮

            // 使用缓存获取场景数据，减少数据库查询
            var globalSceneData = db3.GetSceneData(GLOBAL_STYLE);
            var commonSceneData = db3.GetSceneData(CommonStyle);

            // 生成页面切换按钮，保留当前选中的按钮状态
            GeneratePageButtons(GLOBAL_STYLE, globalSceneData?.SceneCount ?? 1, GlobalPageChangeButton_Click, GlobalButtonPanel, GloblePageIndex);
            GeneratePageButtons(CommonStyle, commonSceneData?.SceneCount ?? 1, CommonPageChangeButton_Click, CommonButtonPanel, CommonPageIndex);
        }

        /// <summary>
        /// 生成页面切换按钮
        /// </summary>
        /// <param name="prefix"> 前缀 </param>
        /// <param name="totalPages"> 总页数 </param>
        /// <param name="clickHandler"> 点击事件 </param>
        /// <param name="panel"> 按钮容器 </param>
        /// <param name="selectedIndex"> 当前选中的页面索引 </param>
        private void GeneratePageButtons(string prefix, int totalPages, RoutedEventHandler clickHandler, Panel panel, int selectedIndex = 0)
        {
            if (totalPages == 1) return;
            for (int i = 0; i < totalPages; i++)
            {
                Button button = new Button
                {
                    Style = (Style)FindResource("ActionPageChangeButton"),
                    Name = $"{prefix}{i}"
                };
                button.Background = (i == selectedIndex) ? SelectedBrush : UnSelectedBrush; // 根据 selectedIndex 设置按钮背景色，保留当前选中状态
                button.Click += clickHandler;
                panel.Children.Add(button);
            }
        }

        // 全局页面切换按钮点击
        private void GlobalPageChangeButton_Click(object sender, RoutedEventArgs e)
        {
            SwitchToPageGrid(sender, GlobalGrid, GLOBAL_STYLE, 3, 4, GlobalButtonPanel);
        }
        // 通用页面切换按钮点击
        private void CommonPageChangeButton_Click(object sender, RoutedEventArgs e)
        {
            SwitchToPageGrid(sender, CommonGrid, CommonStyle, 4, 4, CommonButtonPanel);
        }

        /// <summary>
        /// 显示目标页面并同步相关状态（提取的公共逻辑）
        /// </summary>
        /// <param name="parent">父容器</param>
        /// <param name="gridType">Grid类型</param>
        /// <param name="targetIndex">目标页面索引</param>
        /// <param name="rows">行数</param>
        /// <param name="cols">列数</param>
        /// <param name="buttonPanel">按钮面板</param>
        private void ShowTargetPage(Panel parent, string gridType, int targetIndex, int rows, int cols, Panel buttonPanel)
        {
            if (!parent.Children.OfType<Grid>().Any(g => g.Name == $"{gridType}{targetIndex}")) // 如果目标页面不存在
                GeneratePageGrid(parent, gridType, targetIndex, rows, cols); // 生成目标页面
            ShowPageGrid(parent, gridType, targetIndex, rows, cols); // 显示目标页面
            foreach (Button btn in buttonPanel.Children.OfType<Button>()) // 同步按钮背景色
                btn.Background = btn.Name == $"{gridType}{targetIndex}" ? SelectedBrush : UnSelectedBrush;
            if (gridType != GLOBAL_STYLE) SetCommonTextBlock(targetIndex); // 设置通用标签内容
            if (gridType == GLOBAL_STYLE) GloblePageIndex = targetIndex; else CommonPageIndex = targetIndex; // 同步页面索引
        }

        /// <summary>
        /// 切换到指定的页面Grid
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="parent">父容器</param>
        /// <param name="gridType">Grid类型</param>
        /// <param name="rows">行数</param>
        /// <param name="cols">列数</param>
        /// <param name="buttonPanel">按钮面板</param>
        private void SwitchToPageGrid(object sender, Panel parent, string gridType, int rows, int cols, Panel buttonPanel)
        {
            if (sender is Button clickedButton)
            {
                int pageIndex = int.Parse(clickedButton.Name.Replace($"{gridType}", ""));
                ShowTargetPage(parent, gridType, pageIndex, rows, cols, buttonPanel); // 使用公共方法
            }
        }

        /// <summary>
        /// 获取当前可见的页面Grid索引
        /// </summary>
        /// <param name="parent">父容器</param>
        /// <param name="gridType">Grid类型</param>
        /// <returns>当前可见的页面Grid索引</returns>
        private int GetVisiblePageGridIndex(Panel parent, string gridType)
        {
            foreach (Grid grid in parent.Children.OfType<Grid>())
            {
                if (grid.Visibility == Visibility.Visible && grid.Name.StartsWith(gridType))
                    return int.Parse(grid.Name.Replace(gridType, ""));
            }
            return 0;
        }

        /// <summary>
        /// 处理页面索引边界检查和循环翻页逻辑
        /// </summary>
        /// <param name="targetIndex">目标页面索引</param>
        /// <param name="totalPages">总页数</param>
        /// <param name="loop">是否循环翻页</param>
        /// <returns>处理后的页面索引，如果应该退出则返回 null</returns>
        private int? ValidateAndAdjustPageIndex(int targetIndex, int totalPages, bool loop)
        {
            if (targetIndex < 0) // 超出范围
            {
                if (loop) // 如果循环翻页
                    return totalPages - 1; // 循环翻页到最后一页
                else
                    return null; // 退出
            }
            if (targetIndex >= totalPages) // 超出范围
            {
                if (loop) // 如果循环翻页
                    return 0; // 循环翻页到第一页
                else
                    return null; // 退出
            }
            return targetIndex; // 索引在有效范围内
        }

        /// <summary>
        /// 翻页核心逻辑（根据滚动方向切换到上一页或下一页）
        /// </summary>
        /// <param name="parent">父容器</param>
        /// <param name="gridType">Grid类型</param>
        /// <param name="rows">行数</param>
        /// <param name="cols">列数</param>
        /// <param name="buttonPanel">按钮面板</param>
        /// <param name="delta">滚动变化量（正数向上滚动，负数向下滚动）</param>
        private void NavigatePage(Panel parent, string gridType, int rows, int cols, Panel buttonPanel, int delta)
        {
            int currentIndex = GetVisiblePageGridIndex(parent, gridType); // 获取当前可见页面索引
            int totalPages = db3.GetSceneData(gridType)?.SceneCount ?? 1; // 获取总动作页数
            bool loop = SettingDatabase.GetAllConventions().FirstOrDefault()?.LoopPageFlipping ?? true; // 获取是否循环翻页
            int targetIndex = delta > 0 ? currentIndex - 1 : currentIndex + 1;

            int? validatedIndex = ValidateAndAdjustPageIndex(targetIndex, totalPages, loop); // 验证并调整页面索引
            if (validatedIndex == null) return; // 如果应该退出，直接返回

            ShowTargetPage(parent, gridType, validatedIndex.Value, rows, cols, buttonPanel); // 使用公共方法显示目标页面
        }

        /// <summary>
        /// 鼠标滚轮切换页面
        /// </summary>
        /// <param name="e">鼠标滚轮事件参数</param>
        /// <param name="parent">父容器</param>
        /// <param name="gridType">Grid类型</param>
        /// <param name="rows">行数</param>
        /// <param name="cols">列数</param>
        /// <param name="buttonPanel">按钮面板</param>
        private void Grid_MouseWheel(MouseWheelEventArgs e, Panel parent, string gridType, int rows, int cols, Panel buttonPanel)
        {
            e.Handled = true; // 禁止默认滚轮事件
            NavigatePage(parent, gridType, rows, cols, buttonPanel, e.Delta); // 调用翻页核心逻辑
        }

        // 全局Grid滚轮事件
        private void GlobalGrid_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            Grid_MouseWheel(e, GlobalGrid, GLOBAL_STYLE, 3, 4, GlobalButtonPanel);
        }
        // 通用Grid滚轮事件
        private void CommonGrid_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            Grid_MouseWheel(e, CommonGrid, CommonStyle, 4, 4, CommonButtonPanel);
        }

        /// <summary>
        /// 动态生成一页的Grid和按钮
        /// </summary>
        /// <param name="parent">父容器（如 CommonGrid/GlobalGrid）</param>
        /// <param name="gridType">"common" 或 "_global"</param>
        /// <param name="pageIndex">页码</param>
        /// <param name="rows">行数</param>
        /// <param name="cols">列数</param>
        private Grid GeneratePageGrid(Panel parent, string gridType, int pageIndex, int rows, int cols)
        {
            var grid = new Grid { Name = $"{gridType}{pageIndex}" }; // 创建Grid
            AddGridRows(grid, rows, ((MainWindowViewModel)this.DataContext).ButtonGap); // 添加行定义
            AddGridColumns(grid, cols, ((MainWindowViewModel)this.DataContext).ButtonGap); // 添加列定义
            // 添加按钮到Grid
            AddButtonsToGrid(grid, pageIndex, rows, cols, gridType, (Style)FindResource("Button"), ((MainWindowViewModel)this.DataContext).ButtonSize);
            parent.Children.Add(grid); // 添加到父容器
            return grid;
        }

        /// <summary>
        /// 为Grid添加行定义（含间隔）
        /// </summary>
        /// <param name="grid">目标Grid</param>
        /// <param name="rows">行数</param>
        /// <param name="gap">行间距</param>
        private void AddGridRows(Grid grid, int rows, double gap)
        {
            for (int i = 0; i < rows * 2 - 1; i++)
            {
                grid.RowDefinitions.Add(new RowDefinition
                {
                    Height = (i % 2 == 0)
                        ? new GridLength(1, GridUnitType.Auto)
                        : new GridLength(gap)
                });
            }
        }

        /// <summary>
        /// 为Grid添加列定义（含间隔）
        /// </summary>
        /// <param name="grid">目标Grid</param>
        /// <param name="cols">列数</param>
        /// <param name="gap">列间距</param>
        private void AddGridColumns(Grid grid, int cols, double gap)
        {
            for (int j = 0; j < cols * 2 - 1; j++)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition
                {
                    Width = (j % 2 == 0)
                        ? new GridLength(1, GridUnitType.Auto)
                        : new GridLength(gap)
                });
            }
        }

        /// <summary>
        /// 向Grid中批量添加按钮
        /// </summary>
        /// <param name="grid">目标Grid</param>
        /// <param name="pageIndex">页码</param>
        /// <param name="rows">行数</param>
        /// <param name="cols">列数</param>
        /// <param name="gridType">Grid类型</param>
        /// <param name="style">按钮样式</param>
        /// <param name="size">按钮尺寸</param>
        private void AddButtonsToGrid(Grid grid, int pageIndex, int rows, int cols, string gridType, Style style, double size)
        {
            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < cols; col++)
                {
                    int buttonIndex = pageIndex * 100 + (row + 1) * 10 + (col + 1);
                    string buttonName = $"{gridType}{buttonIndex}";
                    Button button = CreateButton(buttonName, style, size);
                    // 设置行列（注意：有间隔，实际行列号*2）
                    Grid.SetRow(button, row * 2);
                    Grid.SetColumn(button, col * 2);
                    // 绑定数据和事件
                    BindButtonDataAndEvents(button, buttonIndex, gridType);
                    grid.Children.Add(button);
                }
            }
        }

        /// <summary>
        /// 创建一个按钮并设置基本属性
        /// </summary>
        /// <param name="name">按钮名称</param>
        /// <param name="style">按钮样式</param>
        /// <param name="size">按钮尺寸</param>
        /// <returns>新建的Button对象</returns>
        private Button CreateButton(string name, Style style, double size)
        {
            return new Button
            {
                Name = name,
                Style = style,
                Width = size,
                Height = size
            };
        }

        /// <summary>
        /// 绑定按钮的数据和事件，并刷新显示
        /// </summary>
        /// <param name="button">目标按钮</param>
        /// <param name="buttonIndex">按钮索引</param>
        /// <param name="gridType">Grid类型</param>
        private void BindButtonDataAndEvents(Button button, int buttonIndex, string gridType)
        {
            BindButtonEvents(button); // 绑定事件
            var buttonData = db2.GetButtonDataByID(buttonIndex, gridType); // 获取按钮数据
            button.Tag = buttonData; // 绑定数据
            buttonManager.RefreshButtonDisplay(button, buttonData, 60, true); // 刷新显示
        }

        /// <summary>
        /// 显示指定页的Grid
        /// </summary>
        /// <param name="parent">父容器（如 CommonGrid/GlobalGrid）</param>
        /// <param name="gridType">"common" 或 "_global"</param>
        /// <param name="pageIndex">页码</param>
        /// <param name="rows">行数</param>
        /// <param name="cols">列数</param>
        private void ShowPageGrid(Panel parent, string gridType, int pageIndex, int rows, int cols)
        {
            foreach (UIElement child in parent.Children)
            {
                if (child is Grid g && g.Name.StartsWith(gridType))
                    g.Visibility = (g.Name == $"{gridType}{pageIndex}") ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        #endregion

        #region UI 事件处理

        // 加载数据库和Button
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            SetMainWindowState(); // 需要窗口句柄
        }

        /// <summary>
        /// 失去焦点时隐藏功能面板
        /// </summary>
        private void MainWindow_Deactivated(object sender, EventArgs e)
        {
            if (buttonManager.isClosing) return; // 菜单交互中，保持显示
            if (AppStateManager.Pause || AppStateManager.MainWindowPinned) return;
            HideWindowSilently();
        }

        /// <summary>
        /// 静默隐藏
        /// </summary>
        public void HideWindowSilently()
        {
            if (_isShutdown || _isSilentlyHidden) return;

            _isSilentlyHidden = true;
            Opacity = 0;
            IsHitTestVisible = false;

            ResetToFirstPage();

            try
            {
                var controller = WpfAnimatedGif.ImageBehavior.GetAnimationController(BackgroundImage);
                controller?.Pause();
            }
            catch { }
        }

        /// <summary>
        /// 静默显示
        /// </summary>
        public void ShowWindowSilently()
        {
            if (_isShutdown) return;
            bool firstShow = !IsVisible;
            if (firstShow)
            {
                Opacity = 0;
                Show();
            }

            _isSilentlyHidden = false;
            RefreshOnShow(AppStateManager.CommonState ?? DEFAULT_STYLE);
            Opacity = 1;
            IsHitTestVisible = true;

            try
            {
                var controller = WpfAnimatedGif.ImageBehavior.GetAnimationController(BackgroundImage);
                controller?.Play();
            }
            catch { }
        }

        /// <summary>
        /// 将全局与通用动作页都切回第一页
        /// </summary>
        private void ResetToFirstPage()
        {
            if (_isShutdown) return;
            if (DataContext is not MainWindowViewModel) return;
            ShowTargetPage(GlobalGrid, GLOBAL_STYLE, 0, 3, 4, GlobalButtonPanel);
            ShowTargetPage(CommonGrid, CommonStyle, 0, 4, 4, CommonButtonPanel);
        }

        // 添加关闭标志防止报错
        private void MainWindow_Closing(object sender, EventArgs e)
        {
            buttonManager.isClosing = true; // 设置关闭标志
        }

        // 左键点击按钮时执行动作
        private async void DoAction(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button; // 获取Button对象
            string buttonType = GetButtonType(sender); // 获取按钮类型
            if (button.Tag is ButtonData data)
            {
                if (!AppStateManager.MainWindowPinned && data.ActionType != ActionType.OpenActionPage)
                    HideWindowSilently();

                if (HandleShiftKeyAction(button, buttonType)) return; // 处理Shift键动作
                using (var actionManager = new ActionManager())
                {
                    await actionManager.DoActionAsync(data, buttonType);
                }
                HandleAutoReturn(buttonType); // 处理自动返回
            }
            else
            {
                HandleEmptyButtonClick(sender, buttonType); // 处理空按钮点击
            }
        }

        /// <summary>
        /// 处理Shift键动作
        /// </summary>
        /// <param name="button">按钮对象</param>
        /// <param name="buttonType">按钮类型</param>
        /// <returns>是否已处理动作</returns>
        private bool HandleShiftKeyAction(Button button, string buttonType)
        {
            if (Keyboard.IsKeyDown(Key.LeftShift))
            {
                AddActionWindow addWindow = new(int.Parse(button.Name.Replace($"{buttonType}", "")), buttonType, 0); // 创建添加窗口
                addWindow.Show(); // 显示添加窗口
                return true; // 处理Shift键动作
            }
            return false; // 未处理Shift键动作
        }

        /// <summary>
        /// 处理自动返回第一页
        /// </summary>
        /// <param name="buttonType">按钮类型</param>
        private void HandleAutoReturn(string buttonType)
        {
            bool autoReturn = db3.GetAutoReturnToFirstPage(buttonType); // 获取是否自动返回第一页
            if (autoReturn) // 如果自动返回第一页，清空按钮所在容器
            {
                if (buttonType == GLOBAL_STYLE)
                    GlobalGrid.Children.Clear(); // 清空按钮所在容器
                else
                    CommonGrid.Children.Clear(); // 清空按钮所在容器
                GeneratePageGrid(buttonType == GLOBAL_STYLE ? GlobalGrid : CommonGrid, buttonType, 0, 3, 4); // 重新生成第一页内容
            }
        }

        /// <summary>
        /// 处理空按钮点击
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="buttonType">按钮类型</param>
        private void HandleEmptyButtonClick(object sender, string buttonType)
        {
            var Convention = SettingDatabase.GetAllConventions().FirstOrDefault(); // 获取配置信息
            if (Convention.ShowAddImage) // 如果显示添加按钮
                buttonManager.OpenMenu(sender, "CreatActionMenu", this, buttonType); // 点击打开菜单
        }

        // 右键按钮打开菜单
        public void OpenCreatActionMenu(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Button button) return;
            string buttonType = GetButtonType(button);

            // 检查动作是否为扩展类型
            if (button.Tag is ButtonData buttonData && buttonData.ActionType == ActionType.LoadExtension)
            {
                if (buttonManager.CheckExtensionHasContextMenu(button))  // 扩展是否支持右键菜单
                {
                    buttonManager.TryLoadExtensionContextMenu(button, buttonType, this);  // 尝试加载扩展菜单
                    return;
                }
            }

            string menuType = button.Tag is ButtonData ? "OperationMenu" : "CreatActionMenu";
            buttonManager.OpenMenu(button, menuType, this, buttonType);  // 使用默认菜单
        }

        // 允许拖拽
        private void Button_PreviewDragOver(object sender, DragEventArgs e)
        {
            buttonManager.Button_PreviewDragOver(sender, e); // 允许拖拽
        }

        /// <summary>
        /// 处理文件拖拽到按钮上
        /// </summary>
        public void Button_Drop(object sender, DragEventArgs e)
        {
            if (sender is Button TargetButton)
            {
                string buttonType = GetButtonType(sender);
                buttonManager.Button_Drop(sender, e, true, buttonType);
                NotifyActionPageChanged(buttonType);
            }
        }

        // 鼠标左键按下时记录初始位置
        public void Button_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Button button)
                buttonManager.Button_PreviewMouseLeftButtonDown(sender, e); // 记录初始位置
        }

        // 鼠标移动时检查是否满足拖拽条件
        public void Button_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (sender is Button button && e.LeftButton == MouseButtonState.Pressed)
                buttonManager.Button_PreviewMouseMove(sender, e, true, GetButtonType(sender)); // 检查拖拽条件
        }

        // 鼠标左键释放时重置状态
        private void Button_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            buttonManager.Button_PreviewMouseLeftButtonUp(sender, e); // 重置状态
        }

        // 打开标题菜单
        private void OpenTitleMenu(object sender, RoutedEventArgs e)
        {
            TitlePop.IsOpen = true; // 打开菜单
        }

        // 退出Quicker
        private void QuitQuicker(object sender, RoutedEventArgs e)
        {
            TitlePop.IsOpen = false; // 关闭菜单
            (System.Windows.Application.Current as App)?.Shutdown(); // 通过App类正确关闭应用，确保OnExit方法被调用
        }

        // 打开动作管理窗口
        private void OpenActionPageManageWindow(object sender, RoutedEventArgs e)
        {
            TitlePop.IsOpen = false;
            using var wm = new WindowManager();
            wm.OpenTargetWindow("ActionPageManageWindow");
        }

        /// <summary>
        /// 绑定按钮事件
        /// </summary>
        /// <param name="button">指定绑定的 Button</param>
        private void BindButtonEvents(Button button)
        {
            button.Click += DoAction; // 左键点击
            button.Drop += Button_Drop; // 拖拽
            button.PreviewDragOver += Button_PreviewDragOver; // 添加拖拽
            button.MouseRightButtonDown += OpenCreatActionMenu; // 右键点击
            button.PreviewMouseMove += Button_PreviewMouseMove; // 鼠标移动
            button.PreviewMouseLeftButtonUp += Button_PreviewMouseLeftButtonUp; // 鼠标左键释放
            button.PreviewMouseLeftButtonDown += Button_PreviewMouseLeftButtonDown; // 鼠标左键按下
            button.MouseEnter += Button_MouseEnter; // 鼠标移入
            button.MouseLeave += Button_MouseLeave; // 鼠标移出
        }

        // 锁定通用动作页
        private void LockCommonActionPage(object sender, RoutedEventArgs e)
        {
            AppStateManager.Locked = !AppStateManager.Locked; // 切换锁定状态
            ((MainWindowViewModel)this.DataContext).IsLocked = AppStateManager.Locked; // 更新数据绑定
        }

        // 点击标签打开动作页管理窗口
        private void CommonTextBlock_MouseDown(object sender, MouseButtonEventArgs e)
        {
            ActionPageManageWindow actionPageManageWindow = new(CommonStyle); // 创建动作页管理窗口
            actionPageManageWindow.Show(); // 显示窗口
        }

        // 右键锁定 Button 切换菜单
        private void OpenSelectActionPageMenu(object sender, MouseButtonEventArgs e)
        {
            buttonManager.OpenMenu(sender, "SelectActionPageMenu", this, GetButtonType(sender)); // 打开菜单
        }

        /// <summary>
        /// 获取按钮类型
        /// </summary>
        /// <param name="sender"> 按钮对象 </param>
        /// <returns> 按钮类型 </returns>
        private string GetButtonType(object sender)
        {
            Button button = sender as Button; // 获取按钮
            return button.Name.StartsWith(GLOBAL_STYLE) ? GLOBAL_STYLE : CommonStyle; // 获取按钮类型
        }

        // 点击按钮更新Quicker
        private void UpdateQuicker(object sender, RoutedEventArgs e)
        {
            UpdateWindow updateWindow = new(); // 创建更新窗口
            updateWindow.Show(); // 显示窗口
        }

        /// <summary>
        /// 更新按钮内容
        /// </summary>
        /// <param name="buttonID"> 按钮ID </param>
        /// <param name="tableName"> 数据库表名 </param>
        public void UpdateButtonContent(int buttonID, string tableName)
        {
            Grid fatherGrid = tableName == GLOBAL_STYLE ? GlobalGrid : CommonGrid; // 根据表名选择父网格
            int pageIndex = buttonID / 100; // 计算按钮索引
            string buttonName = $"{tableName}{buttonID}"; // 生成按钮名称
            var button = fatherGrid.Children.OfType<Grid>()
                .Where(ug => ug.Name == $"{tableName}{pageIndex}")
                .SelectMany(ug => ug.Children.OfType<Button>())
                .FirstOrDefault(b => b.Name == buttonName); // 查找按钮

            if (button == null) return; // 如果按钮不存在，直接返回
            var buttonData = db2.GetButtonDataByID(buttonID, tableName); // 从数据库中获取按钮数据
            buttonManager.RefreshButtonDisplay(button, buttonData, 60, true); // 更新按钮内容
            NotifyActionPageChanged(tableName);
        }

        // 拖拽动作按钮到上面删除动作
        private void CloseMainWindowButton_Drop(object sender, DragEventArgs e)
        {
            string commonStyle = CommonStyle;
            buttonManager.DeleteActionByDrag(this, commonStyle);
            NotifyActionPageChanged(commonStyle);
        }

        /// <summary>
        /// 鼠标左键拖动窗口
        /// </summary>
        private void MoveMainWindow(object sender, EventArgs e)
        {
            DragMove(); // 触发窗口拖动
        }

        /// <summary>
        /// 打开设置窗口
        /// </summary>
        private void OpenSettingWindow(object sender, RoutedEventArgs e)
        {
            using var wm = new WindowManager(); // 创建窗口管理器
            wm.OpenTargetWindow("SettingWindow"); // 打开设置窗口
        }

        /// <summary>
        /// 订住功能面板
        /// </summary>
        private void BookQuicker(object sender, EventArgs e)
        {
            AppStateManager.MainWindowPinned = !AppStateManager.MainWindowPinned; // 反转 AppStateManager.Pinned
            ((MainWindowViewModel)this.DataContext).IsPinned = AppStateManager.MainWindowPinned; // 反转 ViewModel 的 IsPinned
        }

        /// <summary>
        /// 关闭功能面板
        /// </summary>
        private void HideMainWindow(object sender, EventArgs e)
        {
            HideWindowSilently();
        }

        /// <summary>
        /// 切换动作页（根据按钮数据，切换到指定类型和索引的动作页）
        /// </summary>
        /// <param name="data"> 按钮数据 </param>
        public void OpenActionPage(ButtonData data)
        {
            string type = data.Data1; // 获取动作页类型
            int targetIndex = int.Parse(data.Data2); // 获取目标动作页索引
            if (type != GLOBAL_STYLE) OnCommonStyleChanged(type); // 如果切换到非全局动作页，更新样式

            Panel parent = type == GLOBAL_STYLE ? GlobalGrid : CommonGrid; // 获取当前Grid容器
            int currentGridIndex = GetVisiblePageGridIndex(parent, type); // 获取当前可见的Grid编号
            /* 可有可无的逻辑
                if (currentGridIndex == targetIndex) return; // 已在目标页，无需后续操作
             */

            bool scrollUp = currentGridIndex > targetIndex; // 判断滚动方向
            int steps = Math.Abs(currentGridIndex - targetIndex); // 需要滚动的次数
            int delta = scrollUp ? Mouse.MouseWheelDeltaForOneLine : -Mouse.MouseWheelDeltaForOneLine; // 计算滚动变化量

            // 根据类型确定参数
            int rows = type == GLOBAL_STYLE ? 3 : 4;
            int cols = 4;
            Panel buttonPanel = type == GLOBAL_STYLE ? GlobalButtonPanel : CommonButtonPanel;

            // 循环调用翻页方法
            for (int i = 0; i < steps; i++)
            {
                NavigatePage(parent, type, rows, cols, buttonPanel, delta);
            }
        }

        // 点击按钮打开搜索窗口
        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            SearchWindow searchWindow = System.Windows.Application.Current.Windows.OfType<SearchWindow>().FirstOrDefault(); // 获取搜索窗口
            if (searchWindow == null)
            {
                searchWindow = new SearchWindow(); // 创建搜索窗口
                searchWindow.Show(); // 显示窗口
            }
            else
            {
                searchWindow.Close(); // 关闭窗口
            }
        }

        #endregion

        #region 样式与动画特效

        /// <summary>
        /// 鼠标移入按钮时的处理：
        /// 1. 有Tag（有数据）的按钮执行放大动画（如允许）。
        /// 2. 无Tag（空按钮）显示添加图片（如允许）。
        /// </summary>
        private void Button_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is Button btn)
            {
                var appearance = SettingDatabase.GetAppearanceSettings().FirstOrDefault();
                if (btn.Tag != null) // 有数据的按钮，执行放大动画
                {
                    AnimateButtonScale(btn, appearance);
                }
                else // 空按钮，显示添加图片
                {
                    var conventions = SettingDatabase.GetAllConventions().FirstOrDefault();
                    ShowAddImageOnButton(btn, appearance, conventions);
                }
            }
        }

        /// <summary>
        /// 对有Tag的按钮执行放大动画（如果设置允许）。
        /// </summary>
        /// <param name="btn">目标按钮</param>
        /// <param name="appearance">外观设置</param>
        private void AnimateButtonScale(Button btn, dynamic appearance)
        {
            if (appearance == null || !appearance.ShowActionButtonMouseOver) return; // 不显示动画
            var border = VisualTreeHelper.FindVisualChild<Border>(btn); // 获取按钮边框
            var scale = new ScaleTransform(1, 1); // 创建缩放动画
            border.RenderTransformOrigin = new Point(0.5, 0.5); // 设置缩放中心
            border.RenderTransform = scale; // 设置缩放动画

            var animX = new DoubleAnimation(1.05, TimeSpan.FromMilliseconds(100)); // 创建X轴缩放动画
            var animY = new DoubleAnimation(1.05, TimeSpan.FromMilliseconds(100));
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, animX); // 开始动画
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, animY);
        }

        /// <summary>
        /// 对无Tag的按钮显示添加图片（如果设置允许）。
        /// </summary>
        /// <param name="btn">目标按钮</param>
        /// <param name="appearance">外观设置（用于获取按钮尺寸）</param>
        /// <param name="conventions">通用设置（用于判断是否显示图片）</param>
        private void ShowAddImageOnButton(Button btn, dynamic appearance, dynamic conventions)
        {
            if (conventions == null || !conventions.ShowAddImage) return; // 不显示图片
            double btnSize = appearance.ButtonSize; // 获取按钮尺寸
            double imageSize = btnSize / 2.0; // 计算图片尺寸
            btn.Content = new Image
            {
                Source = new BitmapImage(new Uri("pack://application:,,,/Resources/Images/Add.png")),
                Stretch = Stretch.Uniform,
                Height = imageSize,
                Width = imageSize,
                Opacity = 0.5
            };
        }

        /// <summary>
        /// 鼠标移出按钮时的处理：
        /// 1. 有Tag（有数据）的按钮还原缩放动画。
        /// 2. 无Tag（空按钮）移除添加图片。
        /// </summary>
        private void Button_MouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is Button btn)
            {
                if (btn.Tag != null) // 有数据的按钮，还原缩放动画
                {
                    RestoreButtonScale(btn);
                }
                else // 空按钮，移除图片
                {
                    btn.Content = null;
                }
            }
        }

        /// <summary>
        /// 还原有Tag按钮的缩放动画。
        /// </summary>
        /// <param name="btn">目标按钮</param>
        private void RestoreButtonScale(Button btn)
        {
            var border = VisualTreeHelper.FindVisualChild<Border>(btn); // 获取按钮边框
            if (border != null)
            {
                var scale = border.RenderTransform as ScaleTransform; // 获取缩放动画
                if (scale != null)
                {
                    var animX = new DoubleAnimation(1, TimeSpan.FromMilliseconds(100)); // 缩放动画
                    var animY = new DoubleAnimation(1, TimeSpan.FromMilliseconds(100));
                    scale.BeginAnimation(ScaleTransform.ScaleXProperty, animX); // 开始动画
                    scale.BeginAnimation(ScaleTransform.ScaleYProperty, animY);
                }
            }
        }

        #endregion

        #region 资源释放与生命周期

        protected override void OnClosed(EventArgs e)
        {
            ShutdownResources();
            base.OnClosed(e);
        }

        /// <summary>
        /// 资源释放
        /// </summary>
        public void ShutdownResources()
        {
            if (_isShutdown) return;
            _isShutdown = true;

            UnsubscribeStateEvents();

            try { cancellationTokenSource.Cancel(); } catch { }
            try { cancellationTokenSource.Dispose(); } catch { }

            CleanUpResources();
            CommonStyle = null;

            try { iconManager.Dispose(); } catch { }
            try { buttonManager.Dispose(); } catch { }

            Task.Run(() =>
            {
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Optimized);
            });
        }

        /// <summary>
        /// 清理指定Grid中的所有Grid及其子元素
        /// </summary>
        /// <param name="grid">要清理的Grid</param>
        private void CleanUpGrid(Grid grid)
        {
            foreach (Grid Grid in VisualTreeHelper.FindVisualChildren<Grid>(grid))
            {
                foreach (Button button in VisualTreeHelper.FindVisualChildren<Button>(Grid))
                {
                    // 移除所有事件处理器
                    button.Click -= DoAction; // 左键点击
                    button.Drop -= Button_Drop; // 拖拽
                    button.PreviewDragOver -= Button_PreviewDragOver; // 添加拖拽
                    button.MouseRightButtonDown -= OpenCreatActionMenu; // 右键点击
                    button.PreviewMouseMove -= Button_PreviewMouseMove; // 鼠标移动
                    button.PreviewMouseLeftButtonUp -= Button_PreviewMouseLeftButtonUp; // 鼠标左键释放
                    button.PreviewMouseLeftButtonDown -= Button_PreviewMouseLeftButtonDown; // 鼠标左键按下
                    button.MouseEnter -= Button_MouseEnter; // 鼠标移入
                    button.MouseLeave -= Button_MouseLeave; // 鼠标移出

                    // 清理按钮内容和资源
                    button.Content = null; // 清理按钮内容
                    button.Tag = null; // 清理按钮标签
                    button.Background = null; // 清理按钮背景
                }
                Grid.Children.Clear();
            }
            grid.Children.Clear();
        }

        // 清理所有动态添加的事件处理器
        private void CleanUpEventHandlers()
        {
            foreach (Button button in GlobalButtonPanel.Children.OfType<Button>()) // 清理全局按钮面板事件
            {
                button.Click -= GlobalPageChangeButton_Click; // 全局页面切换按钮点击
            }

            foreach (Button button in CommonButtonPanel.Children.OfType<Button>()) // 清理公共按钮面板事件
            {
                button.Click -= CommonPageChangeButton_Click; // 通用页面切换按钮点击
            }
        }

        /// <summary>
        /// 清理窗口资源（在窗口变为不可见时调用）
        /// </summary>
        private void CleanUpResources()
        {
            CleanUpEventHandlers(); // 清理事件处理器
            CleanUpGrid(GlobalGrid); // 清理全局网格
            CleanUpGrid(CommonGrid); // 清理通用网格

            // 清理背景图片
            if (BackgroundImage != null)
            {
                BackgroundImage.Source = null;
            }

            // 清理 DataContext
            this.DataContext = null;
        }

        #endregion
    }
}