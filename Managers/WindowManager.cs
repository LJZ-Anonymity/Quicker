using Quicker.Windows.MainWindows.MainWindow;
using System.Runtime.InteropServices;
using Quicker.Windows.MainWindows;
using System.Windows.Interop;
using System.Diagnostics;
using System.Windows;
using System.Text;

namespace Quicker.Managers
{
    public class WindowManager : IDisposable
    {
        #region 一些Dll函数
        // 设置窗口位置和大小
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out POINT lpPoint); // 获取鼠标位置

        // 设置窗口位置和大小
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags); // 设置窗口位置和大小

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X; // 鼠标X坐标
            public int Y; // 鼠标Y坐标
        }

        // 窗口置顶相关常量
        private const int HWND_TOPMOST = -1; // 置顶
        private const int SWP_NOSIZE = 0x0001; // 不调整大小
        private const int SWP_NOMOVE = 0x0002; // 不调整位置
        private const int SWP_NOZORDER = 0x0004; // 不调整Z轴顺序

        // 查找窗口句柄
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern nint FindWindow(string lpClassName, string lpWindowName); // 查找窗口句柄

        // 设置前台窗口
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(nint hWnd); // 设置前台窗口

        // 获取当前活动窗口句柄
        [DllImport("user32.dll")]
        private static extern nint GetForegroundWindow(); // 当前活动窗口句柄

        // 获取窗口标题
        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowText(nint hWnd, StringBuilder text, int count); // 窗口标题

        // 获取窗口进程ID
        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId); // 窗口进程ID

        // 判断窗口是否最大化
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsZoomed(nint hWnd); // 窗口最大化

        // 获取窗口样式
        [DllImport("user32.dll")]
        public static extern uint GetWindowLong(IntPtr hWnd, int nIndex); // 窗口样式

        // 获取系统参数
        [DllImport("user32.dll")]
        public static extern uint GetSystemMetrics(int nIndex); // 屏幕宽度

        // 判断是否全屏
        [DllImport("user32.dll")]
        public static extern bool GetClientRect(IntPtr hWnd, out RECT rect); // 窗口客户区矩形

        [DllImport("user32.dll")]
        private static extern IntPtr GetParent(IntPtr hWnd); // 获取父窗口句柄

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr hWnd);

        [DllImport("dwmapi.dll")]
        private static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out RECT pvAttribute, int cbAttribute);

        private const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int left; // 窗口左边缘
            public int top;  // 窗口上边缘
            public int right; // 窗口右边缘
            public int bottom; // 窗口下边缘
        }

        // 窗口样式常量
        private const uint WS_CAPTION = 0x00C00000; // 标题栏
        private const uint WS_POPUP = 0x80000000; // 弹出窗口
        private const int GWL_STYLE = -20; // 窗口样式
        private const int SM_CXSCREEN = 0; // 屏幕宽度
        private const int SM_CYSCREEN = 1; // 屏幕高度

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam); // 枚举窗口

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd); // 判断窗口是否可见

        [DllImport("user32.dll")]
        private static extern int GetWindowTextLength(IntPtr hWnd); // 获取窗口标题长度

        // 获取鼠标下的窗口句柄
        [DllImport("user32.dll")]
        private static extern IntPtr WindowFromPoint(System.Drawing.Point point);

        // 获取窗口类名
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        // 获取桌面窗口句柄
        [DllImport("user32.dll")]
        private static extern IntPtr GetDesktopWindow();

        // 获取Shell窗口句柄
        [DllImport("user32.dll")]
        private static extern IntPtr GetShellWindow();

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam); // 枚举窗口委托
        #endregion

        /// <summary>
        /// 将窗口置顶
        /// </summary>
        /// <param name="window"> 窗口对象 </param>
        public void SetWindowTopmost(Window window)
        {
            nint hWnd = new WindowInteropHelper(window).Handle; // 获取窗口句柄
            SetWindowPos(hWnd, new nint(HWND_TOPMOST), 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE); // 设置窗口置顶
        }

        /// <summary>
        /// 尝试打开已经存在的窗口
        /// </summary>
        /// <param name="windowTitle"> 窗口标题 </param>
        /// <param name="windowHandle"> 窗口句柄 </param>
        public void TryToOpenExitingWindow(string windowTitle, nint windowHandle = default)
        {
            if (windowHandle != nint.Zero) // 窗口句柄不为空
                SetForegroundWindow(windowHandle); // 尝试打开已存在的窗口
            else
            {
                nint hWnd = FindWindow(null, windowTitle); // 查找窗口句柄
                if (hWnd != nint.Zero) SetForegroundWindow(hWnd); // 尝试打开已存在的窗口
            }
        }

        /// <summary>
        /// 获取当前活动窗口句柄
        /// </summary>
        /// <returns> 当前活动窗口句柄 </returns>
        public nint GetCurrentForegroundWindow()
        {
            return GetForegroundWindow(); // 获取当前活动窗口句柄
        }

        /// <summary>
        /// 获取窗口进程ID
        /// </summary>
        /// <param name="hWnd"> 窗口句柄 </param>
        /// <returns> 窗口进程ID </returns>
        public uint GetWindowProcessId(nint hWnd)
        {
            GetWindowThreadProcessId(hWnd, out uint processId); // 获取窗口进程ID
            return processId; // 返回窗口进程ID
        }

        /// <summary>
        /// 打开目标窗口
        /// </summary>
        /// <param name="targetWindow"> 窗口对象 </param>
        public void OpenTargetWindow(string targetWindow)
        {
            Window window = null; // 窗口对象
            
            // 优化窗口查找：直接遍历而不是使用 OfType，避免多次枚举
            try
            {
                foreach (Window w in Application.Current.Windows)
                {
                    if (targetWindow == "SettingWindow" && w is SettingWindow sw)
                    {
                        window = sw;
                        break;
                    }
                    else if (targetWindow == "ActionPageManageWindow" && w is ActionPageManageWindow apmw)
                    {
                        window = apmw;
                        break;
                    }
                }
            }
            catch { } // 忽略枚举异常

            if (window != null) // 窗口对象不为空
            {
                if (window.WindowState == WindowState.Minimized) // 窗口最小化
                    window.WindowState = WindowState.Normal; // 窗口恢复
                nint windowHandle = new WindowInteropHelper(window).Handle; // 获取窗口句柄
                TryToOpenExitingWindow(null, windowHandle); // 尝试打开已存在的窗口
            }
            else
            {
                window = CreateWindow(targetWindow); // 创建窗口对象
                if (window != null)
                    window.Show(); // 显示窗口
            }
            window?.Activate(); // 激活窗口
        }

        /// <summary>
        /// 创建窗口对象
        /// </summary>
        /// <param name="windowType"> 窗口类型 </param>
        private Window CreateWindow(string windowType)
        {
            switch (windowType)
            {
                case "SettingWindow":
                    return new SettingWindow(); // 创建设置窗口
                case "ActionPageManageWindow":
                    return new ActionPageManageWindow(); // 创建动作页面管理窗口
                default:
                    return null; // 窗口类型不存在
            }
        }

        /// <summary>
        /// 判断是否全屏
        /// </summary>
        /// <returns> 是否全屏 </returns>
        public bool IsFullScreen()
        {
            IntPtr hWnd = GetForegroundWindow(); // 获取当前活动窗口句柄
            uint style = GetWindowLong(hWnd, GWL_STYLE); // 获取窗口样式
            int screenWidth = (int)GetSystemMetrics(SM_CXSCREEN); // 获取屏幕宽度
            int screenHeight = (int)GetSystemMetrics(SM_CYSCREEN); // 获取屏幕高度

            if (!GetClientRect(hWnd, out RECT rect))
                return false; // 窗口客户区矩形获取失败
            int windowWidth = rect.right - rect.left; // 窗口宽度
            int windowHeight = rect.bottom - rect.top; // 窗口高度

            bool isFullScreenCondition1 = (style & WS_POPUP) == WS_POPUP && (style & WS_CAPTION) != WS_CAPTION; // 窗口为弹出窗口且无标题栏
            bool isFullScreenCondition2 = windowWidth == screenWidth && windowHeight == screenHeight; // 窗口大小等于屏幕大小
            bool isFullScreenCondition3 = IsZoomed(hWnd); // 窗口最大化
            return isFullScreenCondition1 || isFullScreenCondition2 || isFullScreenCondition3; // 返回是否全屏
        }

        /// <summary>
        /// 获取焦点窗口的进程名称
        /// </summary>
        public string GetProcessName()
        {
            nint foregroundWindow = GetCurrentForegroundWindow(); // 获取当前活动窗口句柄
            uint processId = GetWindowProcessId(foregroundWindow); // 获取窗口进程ID
            Process process = Process.GetProcessById((int)processId); // 获取进程对象
            return process.ProcessName; // 返回进程名称
        }

        // 设置主窗口焦点
        public void SetMainWindowFocused()
        {
            MainWindow mainWindow = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault(); // 获取主窗口对象
            if (mainWindow != null) // 主窗口对象不为空
                mainWindow.Activate(); // 激活主窗口
        }

        // 延时关闭窗口
        public async Task CloseMenuAsync(Window window)
        {
            await Task.Delay(1000); // 延迟100毫秒
            window.Close(); // 关闭窗口
        }

        /// <summary>
        /// 获取鼠标位置并设置窗口位置
        /// </summary>
        /// <param name="window"> 要定位的窗口 </param>
        /// <param name="left"> 窗口左边界 </param>
        /// <param name="top"> 窗口上边界 </param>
        private void SetWindowPosition(Window window, int left, int top)
        {
            nint hWnd = new WindowInteropHelper(window).Handle; // 获取窗口句柄
            SetWindowPos(hWnd, IntPtr.Zero, left, top, 0, 0, SWP_NOSIZE | SWP_NOZORDER); // 设置窗口位置
        }

        /// <summary>
        /// 将窗口定位到鼠标位置附近
        /// </summary>
        /// <param name="window"> 要定位的窗口 </param>
        public void SetWindowPositionNearMouse(Window window)
        {
            GetCursorPos(out POINT cursorPos); // 获取鼠标位置
            SetWindowPosition(window, cursorPos.X, cursorPos.Y); // 设置窗口位置
        }

        /// <summary>
        /// 将窗口左下角定位到鼠标位置附近
        /// </summary>
        /// <param name="window"> 要定位的窗口 </param>
        public void SetWindowBottomLeftNearMouse(Window window)
        {
            GetCursorPos(out POINT cursorPos); // 获取鼠标位置

            // 计算窗口左下角的位置
            int left = cursorPos.X - 20; // 窗口左边界
            int top = cursorPos.Y - 290; // 窗口上边界（鼠标Y坐标减去窗口高度）

            SetWindowPosition(window, left, top); // 设置窗口位置
        }

        /// <summary>
        /// 将窗口中心定位到鼠标位置
        /// </summary>
        public void SetWindowCenterAtMouse(Window window, double? centerOffsetYFromTop = null)
        {
            if (!GetCursorPos(out POINT cursorPos)) return;

            // 首次 Show 前 ActualWidth/Height=0，回退到 XAML 里的 Width/Height
            double w = window.ActualWidth > 0 ? window.ActualWidth : window.Width;
            double h = window.ActualHeight > 0 ? window.ActualHeight : window.Height;
            if (double.IsNaN(w) || w <= 0) w = 450;
            if (double.IsNaN(h) || h <= 0) h = 850;

            // 参考点：默认窗口中心；否则用调用方给的"距顶距离"
            double anchorX = w / 2.0;
            double anchorY = centerOffsetYFromTop ?? (h / 2.0);

            // 物理像素 → DIP（未显示时 PresentationSource 为 null，按 100% 处理）
            double scaleX = 1.0, scaleY = 1.0;
            var src = PresentationSource.FromVisual(window);
            if (src?.CompositionTarget != null)
            {
                scaleX = src.CompositionTarget.TransformFromDevice.M11;
                scaleY = src.CompositionTarget.TransformFromDevice.M22;
            }

            // 鼠标物理像素 → DIP
            double mouseDipX = cursorPos.X * scaleX;
            double mouseDipY = cursorPos.Y * scaleY;

            // 直接设 Left/Top（DIP），Show 时会应用
            window.Left = mouseDipX - anchorX;
            window.Top = mouseDipY - anchorY;
        }

        /// <summary>
        /// 获取窗口 DPI 缩放系数（1.0 = 96 DPI）
        /// </summary>
        private static double GetWindowDpiScale(IntPtr hWnd)
        {
            try
            {
                uint dpi = GetDpiForWindow(hWnd);
                if (dpi > 0) return dpi / 96.0;
            }
            catch { }
            return 1.0;
        }

        /// <summary>
        /// 获取窗口矩形并转换为 WPF 的 DIP 坐标（支持高 DPI、最大化窗口、多屏）
        /// </summary>
        public bool TryGetWindowRectInDips(IntPtr hWnd,
            out double left, out double top, out double width, out double height)
        {
            left = top = width = height = 0;
            if (hWnd == IntPtr.Zero) return false;

            RECT rect;
            bool ok;
            // 最大化窗口：GetWindowRect 会包含屏幕外的边框阴影，用 DWM 扩展边框修正
            if (IsZoomed(hWnd) &&
                DwmGetWindowAttribute(hWnd, DWMWA_EXTENDED_FRAME_BOUNDS, out rect,
                    Marshal.SizeOf<RECT>()) == 0)
            {
                ok = true;
            }
            else
            {
                ok = GetWindowRect(hWnd, out rect);
            }
            if (!ok) return false;

            double scale = GetWindowDpiScale(hWnd);
            left = rect.left / scale;
            top = rect.top / scale;
            width = (rect.right - rect.left) / scale;
            height = (rect.bottom - rect.top) / scale;
            return true;
        }

        /// <summary>
        /// 将窗口居中到当前前台窗口（按 DIP 对齐，夹取到前台窗口所在屏幕的工作区）
        /// </summary>
        public bool CenterWindowOnForeground(Window window)
        {
            IntPtr fg = GetCurrentForegroundWindow();
            if (fg == IntPtr.Zero) return false;

            // 排除自身，避免"居中到自己的旧位置"
            IntPtr self = new WindowInteropHelper(window).EnsureHandle();
            if (fg == self) return false;

            if (!TryGetWindowRectInDips(fg,
                out double fgLeft, out double fgTop, out double fgWidth, out double fgHeight))
                return false;

            // 未布局前 ActualWidth 为 0，回退到 Width；两者都无效则放弃
            double w = window.ActualWidth > 0 ? window.ActualWidth : window.Width;
            double h = window.ActualHeight > 0 ? window.ActualHeight : window.Height;
            if (double.IsNaN(w) || w <= 0) return false;
            if (double.IsNaN(h) || h <= 0) return false;

            double left = fgLeft + (fgWidth - w) / 2;
            double top = fgTop + (fgHeight - h) / 2;

            // 夹取到前台窗口所在屏幕的工作区（DIP）
            try
            {
                var screen = System.Windows.Forms.Screen.FromHandle(fg);
                var wa = screen.WorkingArea;
                double scale = GetWindowDpiScale(fg);
                double waLeft = wa.Left / scale;
                double waTop = wa.Top / scale;
                double waWidth = wa.Width / scale;
                double waHeight = wa.Height / scale;

                left = Math.Max(waLeft, Math.Min(left, waLeft + waWidth - w));
                top = Math.Max(waTop, Math.Min(top, waTop + waHeight - h));
            }
            catch { } // 屏幕查询失败就保持居中值

            window.Left = left;
            window.Top = top;
            return true;
        }

        // 释放资源
        public void Dispose()
        {
            // 释放资源
        }

        /// <summary>
        /// 判断鼠标是否在任务栏上
        /// </summary>
        public bool IsMouseOnTaskbar()
        {
            try
            {
                GetCursorPos(out POINT cursorPos); // 获取鼠标位置
                // 使用WindowFromPoint获取鼠标下的窗口句柄
                IntPtr windowHandle = WindowFromPoint(new System.Drawing.Point(cursorPos.X, cursorPos.Y));
                if (windowHandle == IntPtr.Zero) return false;

                // 获取窗口类名
                StringBuilder className = new StringBuilder(256);
                GetClassName(windowHandle, className, className.Capacity);
                string classNameStr = className.ToString().ToLower();

                // 检查是否是任务栏窗口类
                return classNameStr.Contains("shell_traywnd") || 
                       classNameStr.Contains("shell_secondarytraywnd") ||
                       classNameStr.Contains("taskbar");
            }
            catch
            {
                // 如果API调用失败，回退到坐标比较方法
                GetCursorPos(out POINT cursorPos);
                Rect workArea = SystemParameters.WorkArea;
                return cursorPos.Y >= workArea.Height;
            }
        }

        /// <summary>
        /// 判断鼠标是否在桌面上
        /// </summary>
        public bool IsMouseOnDesktop()
        {
            try
            {
                GetCursorPos(out POINT cursorPos);
                IntPtr hWnd = WindowFromPoint(new System.Drawing.Point(cursorPos.X, cursorPos.Y));
                if (hWnd == IntPtr.Zero) return false;

                // 桌面图标窗口可能挂在 WorkerW / Progman 下面，需要沿父窗口链向上找
                IntPtr current = hWnd;
                for (int depth = 0; depth < 8 && current != IntPtr.Zero; depth++)
                {
                    StringBuilder className = new StringBuilder(256);
                    GetClassName(current, className, className.Capacity);
                    string cls = className.ToString().ToLowerInvariant();

                    if (cls == "progman" || cls == "workerw")
                        return true;

                    if (cls == "shell_traywnd" || cls == "shell_secondarytraywnd")
                        return false; // 鼠标在任务栏上，不算桌面

                    current = GetParent(current);
                }
                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}