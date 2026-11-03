/* 立项日期：2024.12.16
 *                     _ooOoo_
 *                    o8888888o
 *                    88" . "88
 *                    (| -_- |)
 *                    O\  =  /O
 *                 ____/`---'\____
 *               .'  \\|     |//  `.
 *              /  \\|||  :  |||//  \
 *             /  _||||| -:- |||||-  \
 *             |   | \\\  -  /// |   |
 *             | \_|  ''\---/''  |   |
 *             \      .-\__  `-`  ___/-. /
 *           ___`. .'  /--.--\  `. . __
 *        ."" '<  `.___\_<|>_/___.'  >'"".
 *       | | :  `- \`.;`\ _ /`;.`/ - ` : | |
 *       \  \ `-.   \_ __\ /__ _/   .-` /  /
 *  ======`-.____`-.___\_____/___.-`____.-'======
 *                    `=---='
 *  ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
 *              佛祖保佑       永无BUG
 */
using KeyboardHookEventArgs = SharpHook.KeyboardHookEventArgs;
using VisualTreeHelper = Quicker.Helpers.VisualTreeHelper;
using MouseHookEventArgs = SharpHook.MouseHookEventArgs;
using MouseButton = SharpHook.Data.MouseButton;
using Quicker.Windows.MainWindows.MainWindow;
using Hardcodet.Wpf.TaskbarNotification;
using KeyCode = SharpHook.Data.KeyCode;
using System.Runtime.InteropServices;
using Quicker.Windows.MainWindows;
using Quicker.Windows.ToolWindows;
using System.Windows.Threading;
using Quicker.Database.Upgrade;
using Quicker.Models.Settings;
using System.Windows.Controls;
using System.Windows.Interop;
using Quicker.Windows.Menus;
using Quicker.Database.Core;
using System.Windows.Media;
using System.Windows.Input;
using System.Diagnostics;
using Quicker.Managers;
using Quicker.Helpers;
using System.Windows;
using SharpHook.Data;
using SharpHook;
using System.IO;

namespace Quicker
{
    public partial class App : Application
    {
        #region 常量与字段

        // 常量定义
        private const string DEFAULT_STYLE = "common";
        private const string TASKBAR_STYLE = "taskbar";
        private const string DESKTOP_STYLE = "desktop";

        private Action<string, string> _trayIconChangedHandler; // 托盘图标改变事件
        private readonly HashSet<KeyCode> _downKeys = new(); // 按下的键集合
        private bool _isOpenKeyClickedOutside = false; // 是否在外部按下了唤出键
        private KeyCode? _lastKeyCode = null; // 上一个按键码
        private string _previewRunningPath; // 预览运行图标路径
        private string _previewPausedPath; // 预览暂停图标路径
        private TaskPoolGlobalHook? hook; // 全局钩子
        private TaskbarIcon? taskbarIcon; // 托盘图标

        #endregion

        #region 启动与初始化

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e); // 调用基类方法
            InitializeAppDirectories(); // 初始化应用程序目录
            EnsureSingleInstance(); // 确保单例运行
            CheckAppUpdate(); // 检查应用更新
            CheckAndUpdateDatabase(); // 检查并升级数据库
            RestoreGlobalFontFamilyFromDatabase(); // 恢复全局字体设置
            InitializeMainWindow();
            InitializeTimer(); // 初始化定时器
            InitializeTaskbar(); // 初始化托盘图标
            InitializeHookAsync(); // 初始化钩子
            this.MainWindow = null; // 设置主窗口为null，避免应用程序在没有窗口时退出
        }

        /// <summary>
        /// 初始化应用程序目录
        /// </summary>
        private void InitializeAppDirectories()
        {
            AppPathHelper.EnsureAllDirectoriesExist();
        }

        /// <summary>
        /// 互斥锁，确保单例运行
        /// </summary>
        private void EnsureSingleInstance()
        {
            string mutexName = AppVersionHelper.CurrentVersion; // 互斥锁唯一标识
            bool isNewInstance = SingleInstanceManager.CheckForOtherInstances(mutexName, out _); // 检查是否是新实例
            if (!isNewInstance)
            {
                MessageWindow messageWindow = new("Quicker", "Quicker已经在运行，不能启动多个实例！"); // 创建消息窗口
                messageWindow.ShowInTaskbar = true; // 显示在任务栏
                messageWindow.ShowDialog(); // 显示消息窗口
                Environment.Exit(0);
            }
        }

        /// <summary>
        /// 检查并升级数据库
        /// </summary>
        private void CheckAndUpdateDatabase()
        {
            using var databaseUpdater = new DatabaseUpdateManager();
            databaseUpdater.CheckAndUpgradeDatabase(); // 检查并升级数据库
        }

        /// <summary>
        /// 检查应用更新
        /// </summary>
        private async void CheckAppUpdate()
        {
            await Task.Run(() =>
            {
                using var updateManager = new AppUpdateManager(); // 创建更新管理器
                updateManager.CheckForUpdate(); // 检查并安装更新
            });
        }

        /// <summary>
        /// 初始化托盘图标
        /// </summary>
        private void InitializeTaskbar()
        {
            var icon = AppStateManager.TrayIcon; // 设置托盘图标
            Current.Resources["AppIcon"] = icon; // 设置应用图标
            taskbarIcon = new TaskbarIcon // 创建托盘图标
            {
                IconSource = icon, // 设置图标
                ToolTipText = "Quicker" // 设置提示文本
            }; // 创建托盘图标

            taskbarIcon.TrayLeftMouseDown += ShowMainWindow; // 左键单击弹出功能面板
            taskbarIcon.TrayRightMouseDown += ShowCustomMenu; // 右键单击弹出菜单栏
            taskbarIcon.TrayMouseDoubleClick += PauseQuicker; // 双击暂停Quicker

            _trayIconChangedHandler = (running, paused) =>
            {
                _previewRunningPath = running;
                _previewPausedPath = paused;
                taskbarIcon.IconSource = AppStateManager.GetTrayIcon(_previewRunningPath, _previewPausedPath, AppStateManager.Pause);
            };
            AppStateManager.TrayIconChanged += _trayIconChangedHandler;
        }

        /// <summary>
        /// 弹出消息提醒
        /// </summary>
        private void ShowNotification()
        {
            var Convention = SettingDatabase.GetAllConventions().FirstOrDefault(); // 获取设置
            if (Convention.ShowNotification) // 如果设置中允许显示消息提醒
            {
                using var toast = new ToastManager(); // 消息提醒管理器
                toast.Show("成功启动！", ToastType.Common); // 弹出消息提醒
            }
        }

        /// <summary>
        /// 启动时预初始化主窗口
        /// </summary>
        private void InitializeMainWindow()
        {
            // 使用 Loaded 优先级，比 Background 快得多，能在界面框架渲染后立刻执行
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                // 用户可能已经抢先唤起了主窗口，不再重复创建
                if (AppStateManager.MainWindowInstance != null) return;
                try
                {
                    var win = new MainWindow();
                    AppStateManager.MainWindowInstance = win;
                    win.Initialize();
                }
                catch
                {
                    AppStateManager.MainWindowInstance = null;
                }
                finally
                {
                    ShowNotification();
                }
            }));
        }

        /// <summary>
        /// 重载全局字体设置
        /// </summary>
        private void RestoreGlobalFontFamilyFromDatabase()
        {
            // 读取 Appearance 设置
            var appearance = SettingDatabase.GetAppearanceSettings().FirstOrDefault();
            if (appearance == null)
            {
                using var toast = new ToastManager();
                toast.Show("获取字体设置失败！", ToastType.Error);
                return;
            }

            // 获取系统字体列表
            var fontFamilies = Fonts.SystemFontFamilies.Select(f => f.Source).OrderBy(f => f).ToList();
            fontFamilies.Add("(系统默认)"); // 保持和界面一致

            // 判断索引是否为最后一项（即"系统默认"）
            string font1 = (appearance.Font1 >= 0 && appearance.Font1 < fontFamilies.Count - 1) ? fontFamilies[appearance.Font1] : null;
            string font2 = (appearance.Font2 >= 0 && appearance.Font2 < fontFamilies.Count - 1) ? fontFamilies[appearance.Font2] : null;
            var fontFamily = (!string.IsNullOrEmpty(font1), !string.IsNullOrEmpty(font2)) switch
            {
                (true, true) => new FontFamily($"{font1}, {font2}"),
                (true, false) => new FontFamily(font1),
                (false, true) => new FontFamily(font2),
                _ => new FontFamily("微软雅黑")
            };
            Current.Resources["GlobalFontFamily"] = fontFamily; // 设置全局资源
        }

        /// <summary>
        /// 初始化定时器
        /// </summary>
        private void InitializeTimer()
        {
            AppStateManager.StartTime = DateTime.Now; // 记录应用启动时间
            AppStateManager.RecordedTime = AppStateManager.StartTime; // 记录应用记录时间

            // 初始化定时器
            AppStateManager.Timer.Interval = TimeSpan.FromMinutes(5); // 每 5 分钟更新一次
            AppStateManager.Timer.Tick += Timer_Tick; // 每 5 分钟触发一次
            AppStateManager.Timer.Start(); // 启动定时器

            // 初始化按键计时器
            AppStateManager.PressTimer.Interval = TimeSpan.FromMilliseconds(10); // 每 10 毫秒检查一次
            AppStateManager.PressTimer.Tick += PressTimer_Tick; // 计时器回调
        }

        #endregion

        #region 全局钩子与按键判定

        // 初始化钩子
        private async Task InitializeHookAsync()
        {
            hook = new(); // 创建钩子
            hook.KeyPressed += Hook_KeyPressed; // 按键按下事件
            hook.KeyReleased += Hook_KeyReleased; // 按键松开事件
            hook.MousePressed += Hook_MousePressed; // 鼠标按下事件
            hook.MouseReleased += Hook_MouseReleased; // 鼠标松开事件
            await hook.RunAsync().ConfigureAwait(false); // 启动钩子
        }

        // 鼠标按下事件
        private void Hook_MousePressed(object? sender, MouseHookEventArgs e)
        {
            AppStateManager.MousePressStartTime = DateTime.Now;
            IsMouseClickOutsideMainWindow(e); // 点击落在已显示的 MainWindow 外部则隐藏
            if (!CanProcessHook()) return;

            var openMainWindowConditions = AppStateManager.OpenMainWindowConditions;
            if (GetCtrlKeyState() &&
                ((e.Data.Button == MouseButton.Button2 && openMainWindowConditions.OpenMainWindowByCtrl_RightMouseClick) ||
                 (e.Data.Button == MouseButton.Button3 && openMainWindowConditions.OpenMainWindowByCtrl_MiddleMouseClick)))
            {
                HideOrShowMainWindow();
                return;
            }

            ProcessMouseButtonPress(e.Data.Button, openMainWindowConditions);
        }

        /// <summary>
        /// 检测鼠标点击是否在MainWindow外部
        /// </summary>
        private void IsMouseClickOutsideMainWindow(MouseHookEventArgs e)
        {
            _isOpenKeyClickedOutside = false; // 每次按下重置标志位
            var win = AppStateManager.MainWindowInstance;
            if (win == null || win.IsHiddenOrInvisible || AppStateManager.MainWindowPinned || AppStateManager.Pause)
                return;

            bool isInside = Dispatcher.Invoke(() =>
            {
                if (win.IsHiddenOrInvisible || !win.IsLoaded) return true;
                return win.IsPointInsideMainWindowOrMenus(e.Data.X, e.Data.Y);
            });

            if (!isInside)
            {
                // 判断是否是唤出键
                var conditions = AppStateManager.OpenMainWindowConditions;
                bool isOpenKey = (e.Data.Button == MouseButton.Button5 && conditions.OpenMainWindowByX2MouseClick) ||
                                 (e.Data.Button == MouseButton.Button4 && conditions.OpenMainWindowByX1MouseClick) ||
                                 (e.Data.Button == MouseButton.Button3 && conditions.OpenMainWindowByMiddleMouseClick);

                if (isOpenKey)
                {
                    _isOpenKeyClickedOutside = true;
                }
                else
                {
                    // 其他按键点击外部：照常隐藏并清理状态
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (!win.IsHiddenOrInvisible)
                        {
                            win.HideWindowSilently();
                            ResetInteractionState();
                        }
                    }));
                }
            }
        }

        // 鼠标松开事件
        private void Hook_MouseReleased(object? sender, MouseHookEventArgs e)
        {
            AppStateManager.PressTimer?.Stop();
            if (!AppStateManager.MousePressStartTime.HasValue) return;

            var conventions = AppStateManager.Conventions;
            var openMainWindowConditions = AppStateManager.OpenMainWindowConditions;

            // 当场计算精确的鼠标按住时长，并立刻清空
            TimeSpan pressDuration = DateTime.Now - AppStateManager.MousePressStartTime.Value;
            AppStateManager.MousePressStartTime = null;

            ProcessMouseButtonRelease(e.Data.Button, openMainWindowConditions, conventions, pressDuration);
        }

        // 按键按下事件
        private void Hook_KeyPressed(object sender, KeyboardHookEventArgs e)
        {
            if (_downKeys.Contains(e.Data.KeyCode))
                return;

            _downKeys.Add(e.Data.KeyCode);
            _lastKeyCode = e.Data.KeyCode;
            AppStateManager.KeyPressStartTime = DateTime.Now;

            if (!CanProcessHook()) return;

            var openMainWindowConditions = AppStateManager.OpenMainWindowConditions;
            ProcessControlKeyPress(e.Data.KeyCode, openMainWindowConditions);
        }

        // 按键松开事件
        private void Hook_KeyReleased(object sender, KeyboardHookEventArgs e)
        {
            _downKeys.Remove(e.Data.KeyCode);

            if (_lastKeyCode == e.Data.KeyCode)
                _lastKeyCode = null;

            if (!AppStateManager.KeyPressStartTime.HasValue) return;

            TimeSpan pressDuration = DateTime.Now - AppStateManager.KeyPressStartTime.Value;
            AppStateManager.KeyPressStartTime = null;

            if (!CanProcessHook()) return;

            var conventions = AppStateManager.Conventions;
            var openMainWindowConditions = AppStateManager.OpenMainWindowConditions;

            if (!IsOtherKeyDown())
                ProcessControlKeyRelease(e.Data.KeyCode, openMainWindowConditions, conventions, pressDuration);
        }

        /// <summary>
        /// 判断是否有除Ctrl以外的其他任意按键被按下
        /// </summary>
        private bool IsOtherKeyDown()
        {
            foreach (var key in _downKeys)
            {
                if (key != KeyCode.VcLeftControl && key != KeyCode.VcRightControl)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 判断是否可以处理钩子
        /// </summary>
        private bool CanProcessHook()
        {
            return !IsBannedFormQuicker() && !FullScreenDisable() && !IsMouseOnTestButton(); // 如果禁用Quicker或全屏禁用Quicker或鼠标在测试按键区域上，返回
        }

        /// <summary>
        /// 检查鼠标是否在测试按键区域上
        /// </summary>
        private bool IsMouseOnTestButton()
        {
            bool isMouseOnTestButton = false;
            try
            {
                // 使用 BeginInvoke 避免阻塞调用线程，如果 UI 线程繁忙则直接返回 false
                var operation = this.Dispatcher.BeginInvoke(new Action(() =>
                {
                    var mousePosition = System.Windows.Forms.Cursor.Position; // 获取当前鼠标位置
                    var settingWindows = Application.Current.Windows.OfType<SettingWindow>(); // 检查是否有设置窗口打开
                    foreach (var settingWindow in settingWindows)
                    {
                        if (settingWindow.IsVisible)
                        {
                            var testButton = VisualTreeHelper.FindButtonByName(settingWindow, "TestButton"); // 递归查找名为 "TestButton" 的按钮
                            if (testButton != null)
                            {
                                var buttonPoint = testButton.PointFromScreen(new System.Windows.Point(mousePosition.X, mousePosition.Y)); // 将屏幕坐标转换为按钮坐标
                                if (buttonPoint.X >= 0 && buttonPoint.X <= testButton.ActualWidth &&
                                    buttonPoint.Y >= 0 && buttonPoint.Y <= testButton.ActualHeight) // 检查鼠标是否在按钮范围内
                                {
                                    isMouseOnTestButton = true; // 设置鼠标在测试按钮上
                                    break; // 找到后退出循环
                                }
                            }
                        }
                    }
                }), DispatcherPriority.Normal);

                // 等待操作完成，但设置超时时间避免长时间阻塞
                if (operation.Status == DispatcherOperationStatus.Pending)
                {
                    // 如果操作还在等待，等待最多 50ms
                    operation.Wait(TimeSpan.FromMilliseconds(50));
                }
            }
            catch { }
            return isMouseOnTestButton; // 返回鼠标是否在测试按钮上
        }

        /// <summary>
        /// 获取 Ctrl 键状态
        /// </summary>
        private bool GetCtrlKeyState()
        {
            return _downKeys.Contains(KeyCode.VcLeftControl) ||
                   _downKeys.Contains(KeyCode.VcRightControl);
        }

        #endregion

        #region 计时器与长按处理

        /// <summary>
        /// 按键计时器的回调
        /// </summary>
        private void PressTimer_Tick(object sender, EventArgs e)
        {
            // 只要鼠标或键盘有一个有时间记录，就继续处理
            if (!AppStateManager.KeyPressStartTime.HasValue && !AppStateManager.MousePressStartTime.HasValue)
            {
                AppStateManager.PressTimer.Stop();
                return;
            }
            var conventions = AppStateManager.Conventions;
            var openMainWindowConditions = AppStateManager.OpenMainWindowConditions;
            ProcessLongPressAndMouseMove(conventions, openMainWindowConditions);
        }

        /// <summary>
        /// 处理长按和鼠标移动
        /// </summary>
        /// <param name="conventions"> 设置 </param>
        /// <param name="conditions"> 设置 </param>
        private void ProcessLongPressAndMouseMove(Convention conventions, OpenMainWindow conditions)
        {
            if (conditions.OpenMainWindowByMiddleMouseClickLonger ||
                conditions.OpenMainWindowByRightMouseClickLonger)
            {
                ProcessLongPress(conventions); // 处理长按事件
            }
            else if (conditions.OpenMainWindowByRightMouseClick_Move)
            {
                ProcessMouseMove(conventions); // 处理鼠标移动事件
            }
        }

        /// <summary>
        /// 处理长按事件
        /// </summary>
        /// <param name="conventions"> 设置 </param>
        private void ProcessLongPress(Convention conventions)
        {
            // 检查鼠标长按
            if (AppStateManager.MousePressStartTime.HasValue)
            {
                TimeSpan duration = DateTime.Now - AppStateManager.MousePressStartTime.Value;
                if (duration.TotalMilliseconds >= conventions.LongPressThreshold)
                {
                    HideOrShowMainWindow();
                    AppStateManager.MousePressStartTime = null; // 清空防止重复触发
                    AppStateManager.PressTimer.Stop();
                    return;
                }
            }

            // 检查键盘长按
            if (AppStateManager.KeyPressStartTime.HasValue)
            {
                TimeSpan duration = DateTime.Now - AppStateManager.KeyPressStartTime.Value;
                if (duration.TotalMilliseconds >= conventions.LongPressThreshold)
                {
                    HideOrShowMainWindow();
                    AppStateManager.KeyPressStartTime = null; // 清空防止重复触发
                    AppStateManager.PressTimer.Stop();
                    return;
                }
            }
        }

        /// <summary>
        /// 处理鼠标移动事件
        /// </summary>
        /// <param name="conventions"> 设置 </param>
        private void ProcessMouseMove(Convention conventions)
        {
            var currentPosition = new System.Windows.Point(
                System.Windows.Forms.Cursor.Position.X,
                System.Windows.Forms.Cursor.Position.Y
            ); // 获取当前鼠标位置

            double offsetX = currentPosition.X - AppStateManager.StartPosition.X; // 计算水平偏移量
            double offsetY = currentPosition.Y - AppStateManager.StartPosition.Y; // 计算垂直偏移量
            double distance = Math.Sqrt(offsetX * offsetX + offsetY * offsetY); // 计算移动距离
            if (distance > conventions.MouseMovePixels) // 如果移动距离大于设置像素值
            {
                HideOrShowMainWindow();
            }
        }

        // 定时器每5min保存使用时长
        private void Timer_Tick(object sender, EventArgs e)
        {
            // 使用实际经过的时间，避免固定300秒带来的显示回退
            var now = DateTime.Now;
            var elapsedSeconds = (now - AppStateManager.RecordedTime).TotalSeconds;
            if (elapsedSeconds < 0) elapsedSeconds = 0; // 防御系统时间被回拨

            // 在后台线程异步执行数据库操作，避免阻塞 UI 线程
            _ = Task.Run(() =>
            {
                try
                {
                    var convention = SettingDatabase.GetAllConventions().FirstOrDefault(); // 获取设置
                    if (convention != null)
                    {
                        convention.TotalUsageTime += elapsedSeconds; // 增加实际经过秒数
                        SettingDatabase.SaveTotalUsageTime(convention.TotalUsageTime); // 保存总使用时长到数据库
                    }
                }
                catch { } // 忽略数据库操作异常，避免影响定时器
            });

            AppStateManager.RecordedTime = now; // 更新记录时间（立即更新，不等待数据库操作）
        }

        #endregion

        #region 动作响应逻辑

        /// <summary>
        /// 处理鼠标按下事件
        /// </summary>
        private void ProcessMouseButtonPress(MouseButton button, OpenMainWindow conditions)
        {
            switch (button)
            {
                case MouseButton.Button2:
                    ProcessRightMouseButtonPress(conditions);
                    break; // 右键按下
                case MouseButton.Button3:
                    ProcessMiddleMouseButtonPress(conditions);
                    break; // 中键按下
                case MouseButton.Button4:
                    ProcessX1MouseButtonPress(conditions);
                    break; // X1鼠标按下
                case MouseButton.Button5:
                    ProcessX2MouseButtonPress(conditions);
                    break; // X2鼠标按下
            }
        }

        /// <summary>
        /// 处理右键按下事件
        /// </summary>
        private void ProcessRightMouseButtonPress(OpenMainWindow conditions)
        {
            if (conditions.OpenMainWindowByRightMouseClick_Move)
            {
                RecordMousePosition();
                _ = PrepareMainWindowAsync(startTimer: true);
            }
            else if (conditions.OpenMainWindowByRightMouseClickLonger)
            {
                _ = PrepareMainWindowAsync(startTimer: true);
            }
        }

        /// <summary>
        /// 处理中键按下事件
        /// </summary>
        private void ProcessMiddleMouseButtonPress(OpenMainWindow conditions)
        {
            if (conditions.OpenMainWindowByMiddleMouseClick)
                _ = PrepareMainWindowAsync();
            else if (conditions.OpenMainWindowByMiddleMouseClickLonger)
                _ = PrepareMainWindowAsync(startTimer: true);
        }

        /// <summary>
        /// 处理 X1 鼠标按下事件
        /// </summary>
        private void ProcessX1MouseButtonPress(OpenMainWindow conditions)
        {
            if (conditions.OpenMainWindowByX1MouseClick)
                _ = PrepareMainWindowAsync();
        }

        /// <summary>
        /// 处理 X2 鼠标按下事件
        /// </summary>
        private void ProcessX2MouseButtonPress(OpenMainWindow conditions)
        {
            if (conditions.OpenMainWindowByX2MouseClick)
                _ = PrepareMainWindowAsync();
        }

        /// <summary>
        /// 处理鼠标松开事件
        /// </summary>
        private void ProcessMouseButtonRelease(MouseButton button, OpenMainWindow conditions, Convention conventions, TimeSpan pressDuration)
        {
            switch (button)
            {
                case MouseButton.Button3:
                    ProcessMiddleMouseButtonRelease(conditions, conventions, pressDuration);
                    break; // 中键松开
                case MouseButton.Button4:
                case MouseButton.Button5:
                    ProcessXMouseButtonRelease(conditions, conventions, pressDuration);
                    break; // X鼠标松开
            }
        }

        /// <summary>
        /// 处理中键松开事件
        /// </summary>
        private void ProcessMiddleMouseButtonRelease(OpenMainWindow conditions, Convention conventions, TimeSpan pressDuration)
        {
            if (pressDuration.TotalMilliseconds <= conventions.LongPressThreshold &&
                conditions.OpenMainWindowByMiddleMouseClick)
            {
                if (_isOpenKeyClickedOutside)
                {
                    var win = AppStateManager.MainWindowInstance;
                    if (win != null) Dispatcher.BeginInvoke(new Action(() => ShowMainWindowInstance(win)), DispatcherPriority.Normal);
                }
                else
                {
                    HideOrShowMainWindow();
                }
            }
        }

        /// <summary>
        /// 处理 X 鼠标按下事件
        /// </summary>
        private void ProcessXMouseButtonRelease(OpenMainWindow conditions, Convention conventions, TimeSpan pressDuration)
        {
            if ((conditions.OpenMainWindowByX1MouseClick ||
                 conditions.OpenMainWindowByX2MouseClick) &&
                pressDuration.TotalMilliseconds <= conventions.LongPressThreshold)
            {
                if (_isOpenKeyClickedOutside)
                {
                    var win = AppStateManager.MainWindowInstance;
                    if (win != null) Dispatcher.BeginInvoke(new Action(() => ShowMainWindowInstance(win)), DispatcherPriority.Normal);
                }
                else
                {
                    HideOrShowMainWindow();
                }
            }
        }

        /// <summary>
        /// 处理 Ctrl 按键按下事件
        /// </summary>
        private void ProcessControlKeyPress(KeyCode keyCode, OpenMainWindow conditions)
        {
            if ((keyCode == KeyCode.VcLeftControl || keyCode == KeyCode.VcRightControl) &&
                (conditions.OpenMainWindowByCtrl_MiddleMouseClick ||
                 conditions.OpenMainWindowByCtrl_RightMouseClick ||
                 conditions.OpenMainWindowByCtrl))
            {
                var win = AppStateManager.MainWindowInstance;
                if (win == null || win.IsHiddenOrInvisible)
                {
                    _ = PrepareMainWindowAsync();
                }
            }
        }

        /// <summary>
        /// 处理 Ctrl 按键松开事件
        /// </summary>
        private void ProcessControlKeyRelease(KeyCode keyCode, OpenMainWindow conditions, Convention conventions, TimeSpan pressDuration)
        {
            if ((keyCode == KeyCode.VcLeftControl ||
                 keyCode == KeyCode.VcRightControl) &&
                conditions.OpenMainWindowByCtrl &&
                pressDuration.TotalMilliseconds <= conventions.LongPressThreshold)
            {
                HideOrShowMainWindow();
            }
        }

        // 记录鼠标位置
        private void RecordMousePosition()
        {
            AppStateManager.StartPosition = new System.Windows.Point(
                System.Windows.Forms.Cursor.Position.X, // 鼠标位置
                System.Windows.Forms.Cursor.Position.Y // 鼠标位置
            ); // 记录鼠标位置
        }

        #endregion

        #region 窗口控制与位置判定

        /// <summary>
        /// 判断是否全屏禁用Quicker
        /// </summary>
        private bool FullScreenDisable()
        {
            var blacklistSettings = AppStateManager.BlacklistSettings; // 获取设置
            if (!blacklistSettings.IsFullScreenDisabled) return false; // 如果全屏禁用Quicker，返回

            using var windowManager = new WindowManager(); // 创建窗口管理器
            if (!windowManager.IsFullScreen()) return false; // 如果窗口不是全屏，返回

            string processName = windowManager.GetProcessName(); // 获取进程名称
            var blacklistApplications = AppStateManager.BlacklistApplications; // 获取设置

            if (blacklistApplications.Count == 0) return true;

            return !blacklistApplications.Any(p => p.ProcessName == processName && !p.IsInBlacklist);
        }

        /// <summary>
        /// 判断是否禁用Quicker
        /// </summary>
        private bool IsBannedFormQuicker()
        {
            try
            {
                using var windowManager = new WindowManager(); // 创建窗口管理器
                nint foregroundWindow = windowManager.GetCurrentForegroundWindow(); // 获取当前前台窗口
                if (foregroundWindow == IntPtr.Zero) return false; // 如果当前前台窗口为空，返回

                uint processId = windowManager.GetWindowProcessId(foregroundWindow); // 获取窗口进程ID
                string processName = Process.GetProcessById((int)processId).ProcessName; // 获取进程名称

                return AppStateManager.BlacklistApplications
                    .Any(p => p.ProcessName == processName && p.IsInBlacklist); // 如果进程名称在黑名单中，返回
            }
            catch { return false; }
        }

        // 弹出主窗口
        public async void ShowMainWindow(object sender, RoutedEventArgs e)
        {
            var win = await PrepareMainWindowAsync(forceCondition: 2);
            ShowMainWindowInstance(win, 2);
        }

        /// <summary>
        /// 准备主窗口：决定场景类型、定位、兜底创建实例。不显示
        /// startTimer=true 时启动长按计时器（用于长按/移动打开场景）
        /// 可在任意线程调用，返回时已回到 UI 线程。
        /// </summary>
        private async Task<MainWindow> PrepareMainWindowAsync(bool startTimer = false, int? forceCondition = null)
        {
            if (startTimer) AppStateManager.PressTimer.Start();

            string windowType = await Task.Run(() => DetermineWindowType() ?? DEFAULT_STYLE)
                .ConfigureAwait(false);

            return await Dispatcher.InvokeAsync(() =>
            {
                var win = AppStateManager.MainWindowInstance;
                if (win == null)
                {
                    win = new MainWindow();
                    try { win.Initialize(); } catch { }
                    AppStateManager.MainWindowInstance = win;
                }

                AppStateManager.CommonState = windowType;

                int conditions = forceCondition ?? AppStateManager.OpenMainWindowConditions.WindowStartupLocation;

                // 只有首次创建句柄前，才在此时设置初始位置
                if (new WindowInteropHelper(win).Handle == IntPtr.Zero)
                {
                    if (conditions == 1)
                        win.ShouldCenterAtMouseOnShow = true;
                    else if (conditions == 2)
                        win.ShouldCenterOnScreenOnShow = true;
                    else
                        SetMainWindowPosition(win, conditions);
                }

                return win;
            });
        }

        /// <summary>
        /// 显示并激活主窗口。要求 UI 线程调用
        /// </summary>
        private void ShowMainWindowInstance(MainWindow win, int? forceCondition = null)
        {
            if (win == null) return;

            // 【关键修改】在 Show 之前，瞬间把窗口移动到正确位置
            if (new WindowInteropHelper(win).Handle != IntPtr.Zero)
            {
                int conditions = forceCondition ?? AppStateManager.OpenMainWindowConditions.WindowStartupLocation;
                SetMainWindowPosition(win, conditions);
            }

            win.ShowWindowSilently();

            if (!double.IsNaN(win.Left)) AppStateManager.Left = (float)win.Left;
            if (!double.IsNaN(win.Top)) AppStateManager.Top = (float)win.Top;

            ResetInteractionState();
        }

        /// <summary>
        /// 切换主窗口显示状态。要求 UI 线程调用
        /// </summary>
        private void ToggleMainWindow()
        {
            var win = AppStateManager.MainWindowInstance;
            if (win == null) return;
            if (!win.IsHiddenOrInvisible)
            {
                if (AppStateManager.MainWindowPinned) return;
                win.HideWindowSilently();
                ResetInteractionState();
            }
            else
            {
                ShowMainWindowInstance(win);
            }
        }

        /// <summary>
        /// 关闭或显示主窗口。可从任意线程调用。
        /// </summary>
        public void HideOrShowMainWindow()
        {
            if (Dispatcher.CheckAccess()) ToggleMainWindow();
            else Dispatcher.BeginInvoke(new Action(ToggleMainWindow), DispatcherPriority.Normal);
        }

        /// <summary>
        /// 重置所有按键/鼠标的计时与状态，结束本次交互
        /// </summary>
        private void ResetInteractionState()
        {
            AppStateManager.KeyPressStartTime = null;
            AppStateManager.MousePressStartTime = null;
            _lastKeyCode = null;
            AppStateManager.PressTimer?.Stop();
        }

        /// <summary>
        /// 确定窗口类型
        /// </summary>
        private string DetermineWindowType()
        {
            if (AppStateManager.Locked)
            {
                return AppStateManager.CommonState ?? DEFAULT_STYLE;
            }

            using var wm = new WindowManager();
            // 先检查进程场景类型
            string processSceneType = GetProcessSceneType(wm);
            if (!string.IsNullOrEmpty(processSceneType))
                return processSceneType;

            // 再检查鼠标位置类型
            string mouseLocationType = GetMouseLocationType(wm);
            return mouseLocationType;
        }

        /// <summary>
        /// 获取鼠标位置类型
        /// </summary>
        private string GetMouseLocationType(WindowManager wm)
        {
            if (wm.IsMouseOnTaskbar())
                return TASKBAR_STYLE; // 鼠标在任务栏上
            else if (wm.IsMouseOnDesktop())
                return DESKTOP_STYLE; // 鼠标在桌面上
            else
                return DEFAULT_STYLE; // 鼠标在其他窗口上
        }

        /// <summary>
        /// 获取进程对应的场景类型
        /// </summary>
        private string GetProcessSceneType(WindowManager wm)
        {
            string result = string.Empty;
            IntPtr foregroundWindow = wm.GetCurrentForegroundWindow(); // 获取当前前台窗口
            if (foregroundWindow == IntPtr.Zero) return result; // 无前台窗口
            try
            {
                uint processId = wm.GetWindowProcessId(foregroundWindow); // 获取窗口进程ID
                using var process = Process.GetProcessById((int)processId); // 获取进程

                // 访问 MainModule 可能因权限不足抛出 Win32Exception（拒绝访问）或进程退出等异常
                string processFilePath = string.Empty;
                try
                {
                    processFilePath = process.MainModule.FileName; // 获取进程文件路径
                }
                catch // 无法访问主模块则保持默认结果
                {
                    return result;
                }

                if (!string.IsNullOrEmpty(processFilePath))
                {
                    string processFileName = Path.GetFileNameWithoutExtension(processFilePath).ToLower(); // 获取进程文件名（不含后缀）
                    try
                    {
                        // 使用超时任务避免数据库操作阻塞过久
                        var dbTask = Task.Run(() =>
                        {
                            try
                            {
                                var db = new ActionPageDatabase();
                                if (db.SceneExists(processFileName))
                                {
                                    var scene = db.GetSceneData(processFileName);
                                    if (scene != null)
                                    {
                                        if (string.Equals(scene.SceneProcess, processFilePath, StringComparison.OrdinalIgnoreCase) && scene.SceneCount > 0)
                                        {
                                            return processFileName;
                                        }
                                    }
                                }
                            }
                            catch { }
                            return string.Empty;
                        });

                        // 等待最多 500ms，超时则返回默认结果
                        if (dbTask.Wait(TimeSpan.FromMilliseconds(500)))
                        {
                            result = dbTask.Result;
                        }
                    }
                    catch { } // 忽略超时或其他异常
                }
            }
            catch { } // 忽略所有异常
            return result;
        }

        /// <summary>
        /// 设置主窗口位置
        /// </summary>
        private void SetMainWindowPosition(MainWindow mainWindow, int conditions)
        {
            mainWindow.WindowStartupLocation = WindowStartupLocation.Manual;
            switch (conditions)
            {
                case 0: // 保持当前位置
                    break;
                case 1: // 鼠标位置
                    {
                        using var wm = new WindowManager();
                        wm.SetWindowCenterAtMouse(mainWindow, mainWindow.GetFunctionBarCenterOffsetFromTop());
                    }
                    break;
                case 2: // 屏幕中心（主屏幕工作区）
                    {
                        var wa = SystemParameters.WorkArea;
                        double w = mainWindow.ActualWidth > 0 ? mainWindow.ActualWidth : mainWindow.Width;
                        double h = mainWindow.ActualHeight > 0 ? mainWindow.ActualHeight : mainWindow.Height;
                        mainWindow.Left = wa.Left + (wa.Width - w) / 2;
                        mainWindow.Top = wa.Top + (wa.Height - h) / 2;
                    }
                    break;
                case 3: // 前台窗口中心
                    {
                        bool positioned = false;
                        try
                        {
                            using var wm = new WindowManager();
                            positioned = wm.CenterWindowOnForeground(mainWindow);
                        }
                        catch { }
                        if (!positioned) goto case 2;
                    }
                    break;
                case 4: // 上次位置
                    if (AppStateManager.Left != 0 || AppStateManager.Top != 0)
                    {
                        mainWindow.Left = AppStateManager.Left;
                        mainWindow.Top = AppStateManager.Top;
                    }
                    break;
            }
        }

        // 弹出菜单栏
        private void ShowCustomMenu(object sender, RoutedEventArgs e)
        {
            var customMenu = new CustomMenu(); // 创建新菜单
            customMenu.ShowWithAnimation(); // 使用淡入动画显示菜单栏
            customMenu.Activate(); // 激活菜单
        }

        // 暂停Quicker
        public async void PauseQuicker(object sender, RoutedEventArgs e)
        {
            using var toast = new ToastManager(); // 消息提醒管理器
            try
            {
                hook?.Dispose(); // 销毁当前钩子
                hook = null; // 清空钩子
                if (AppStateManager.Pause) InitializeHookAsync(); // 重新初始化钩子
            }
            catch
            {
                toast.Show("请勿频繁操作！", ToastType.Warning); // 弹出消息提醒
            }

            var toastMessage = AppStateManager.Pause ? "Quicker已恢复" : "Quicker已暂停"; // 消息提醒
            var text = AppStateManager.Pause ? "暂停" : "恢复"; // 消息提醒
            AppStateManager.Pause = !AppStateManager.Pause; // 切换暂停状态
            taskbarIcon.IconSource = AppStateManager.TrayIcon; // 切换托盘图标
            toast.Show(toastMessage, AppStateManager.Pause ? ToastType.Common : ToastType.Success); // 弹出消息提醒
        }

        #endregion

        #region 资源释放

        // 退出应用释放资源
        protected override void OnExit(ExitEventArgs e)
        {
            // 先释放关键资源，避免阻塞退出流程
            DisposeTaskbarIcon();
            DisposeHook();

            try
            {
                double currentSessionTime = (DateTime.Now - AppStateManager.RecordedTime).TotalSeconds;
                if (currentSessionTime > 0)
                {
                    var convention = SettingDatabase.GetAllConventions().FirstOrDefault();
                    if (convention != null)
                    {
                        convention.TotalUsageTime += currentSessionTime;
                        SettingDatabase.SaveTotalUsageTime(convention.TotalUsageTime);
                    }
                }
            }
            catch { }

            AppStateManager.Dispose();
            SingleInstanceManager.ReleaseMutex();
            base.OnExit(e);
        }

        // 释放托盘图标绑定的事件
        private void DisposeTaskbarIcon()
        {
            taskbarIcon.TrayLeftMouseDown -= ShowMainWindow; // 移除左键单击弹出功能面板事件
            taskbarIcon.TrayRightMouseDown -= ShowCustomMenu; // 移除右键单击弹出菜单栏事件
            taskbarIcon.TrayMouseDoubleClick -= PauseQuicker; // 移除双击暂停Quicker事件
            if (_trayIconChangedHandler != null)
                AppStateManager.TrayIconChanged -= _trayIconChangedHandler; // 解绑托盘图标事件
            taskbarIcon?.Dispose(); // 释放托盘图标
            taskbarIcon = null; // 清空托盘图标
        }

        // 释放钩子绑定的事件
        private void DisposeHook()
        {
            if (hook != null)
            {
                hook.MouseReleased -= Hook_MouseReleased; // 移除鼠标松开事件处理器
                hook.MousePressed -= Hook_MousePressed; // 移除鼠标按下事件处理器
                hook.KeyReleased -= Hook_KeyReleased; // 移除按键松开事件处理器
                hook.KeyPressed -= Hook_KeyPressed; // 移除按键按下事件处理器
                hook.Dispose(); // 释放钩子
                hook = null; // 清空钩子
            }
        }

        #endregion
    }
}