using System.Runtime.InteropServices;
using System.Windows.Media.Imaging;
using System.Windows.Interop;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows;

namespace Quicker.Windows.ToolWindows
{
    public partial class ColorPickerMagnifier : Window
    {
        #region Win32 API 声明

        // 获取设备上下文
        [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hWnd);

        // 释放设备上下文
        [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        // 获取像素颜色
        [DllImport("gdi32.dll")] private static extern uint GetPixel(IntPtr hdc, int nXPos, int nYPos);

        // 创建兼容的设备上下文
        [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        // 创建兼容的位图
        [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

        // 选择对象
        [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

        // 删除对象
        [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr hObject);

        // 删除设备上下文
        [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr hdc);

        // 复制位图
        [DllImport("gdi32.dll")] private static extern bool BitBlt(IntPtr hObject, int nXDest, int nYDest, int nWidth, int nHeight, IntPtr hObjectSource, int nXSrc, int nYSrc, int dwRop);

        #endregion

        #region 常量声明

        private const int OFFSET_DISTANCE = 20; // 窗口距离鼠标的偏移距离
        private const int SRCCOPY = 0x00CC0020; // 位图操作常量，表示直接复制
        private const int MAGNIFICATION = 8; // 放大倍数
        private const int CAPTURE_SIZE = 25; // 捕获区域大小（像素），显示 25x25 像素区域

        #endregion

        #region 属性声明

        public Color SelectedColor { get; private set; } // 选中的颜色

        private bool _positionInitialized = false; // 位置是否已初始化
        private double _lastLeft = double.NaN; // 上次窗口位置
        private double _lastTop = double.NaN;

        #endregion

        public ColorPickerMagnifier()
        {
            InitializeComponent();
        }

        private void ColorPickerMagnifier_Loaded(object sender, RoutedEventArgs e) => UpdatePosition(); // 设置窗口位置在鼠标旁边

        /// <summary>
        /// 更新窗口位置
        /// </summary>
        private void UpdatePosition()
        {
            // 获取物理像素坐标 (来自 Windows Forms)
            var mousePos = System.Windows.Forms.Control.MousePosition;

            // 获取当前屏幕的 DPI 缩放比例
            var source = PresentationSource.FromVisual(this);
            if (source == null || source.CompositionTarget == null) return; // 窗口未完全加载或没有可视目标，跳过更新

            // TransformFromDevice.M11/M22 给出的是将物理像素转换为设备独立像素的因子
            double dpiScaleX = source.CompositionTarget.TransformFromDevice.M11;
            double dpiScaleY = source.CompositionTarget.TransformFromDevice.M22;

            // 将物理像素坐标转换为设备独立像素 (WPF 单位)
            double wpfMouseX = mousePos.X * dpiScaleX;
            double wpfMouseY = mousePos.Y * dpiScaleY;

            // 获取屏幕的设备独立像素尺寸
            var screenWidth = SystemParameters.PrimaryScreenWidth;
            var screenHeight = SystemParameters.PrimaryScreenHeight;

            // 计算理想位置（鼠标右下方）
            double idealLeft = wpfMouseX + OFFSET_DISTANCE;
            double idealTop = wpfMouseY + OFFSET_DISTANCE;

            // 如果窗口超出右边界，放在鼠标左侧
            if (idealLeft + Width > screenWidth)
            {
                idealLeft = wpfMouseX - Width - OFFSET_DISTANCE;
            }

            // 如果窗口超出下边界，放在鼠标上方
            if (idealTop + Height > screenHeight)
            {
                idealTop = wpfMouseY - Height - OFFSET_DISTANCE;
            }

            // 确保窗口不超出左边界和上边界
            idealLeft = Math.Max(0, idealLeft);
            idealTop = Math.Max(0, idealTop);

            // 只在位置真正改变时才更新，避免频繁跳动
            if (!_positionInitialized ||
                Math.Abs(idealLeft - _lastLeft) > 1 ||
                Math.Abs(idealTop - _lastTop) > 1)
            {
                Left = idealLeft;
                Top = idealTop;
                _lastLeft = idealLeft;
                _lastTop = idealTop;
                _positionInitialized = true;
            }
        }

        /// <summary>
        /// 更新颜色和放大镜图像（优化：图像生成移到后台）
        /// </summary>
        public void UpdateColor()
        {
            var mousePos = System.Windows.Forms.Control.MousePosition;

            // 1. 快速更新位置和颜色信息（保持同步，确保 UI 响应）
            UpdatePosition();
            var (R, G, B) = GetPixelColor(mousePos.X, mousePos.Y);
            SelectedColor = Color.FromRgb(R, G, B);

            // 更新预览
            ColorPreviewBorder.Background = new SolidColorBrush(SelectedColor);
            HexTextBlock.Text = $"#{SelectedColor.R:X2}{SelectedColor.G:X2}{SelectedColor.B:X2}";
            RgbTextBlock.Text = $"RGB({SelectedColor.R}, {SelectedColor.G}, {SelectedColor.B})";

            // 2. 将耗时的图像捕获和放大操作移到后台线程执行
            int centerX = mousePos.X;
            int centerY = mousePos.Y;

            Task.Run(() =>
            {
                var bitmapSource = GenerateMagnifierBitmapSource(centerX, centerY); // 在后台线程中生成 BitmapSource
                Dispatcher.Invoke(() =>
                {
                    if (bitmapSource != null)
                    {
                        MagnifierImage.Source = bitmapSource;
                    }
                }); // 使用 Dispatcher 将更新 UI 的操作（MagnifierImage.Source）调度回主线程
            });
        }

        /// <summary>
        /// 在后台线程中执行屏幕捕获和放大逻辑
        /// </summary>
        /// <param name="centerX"> 屏幕中心x坐标（物理像素）</param>
        /// <param name="centerY"> 屏幕中心y坐标（物理像素）</param>
        private BitmapSource GenerateMagnifierBitmapSource(int centerX, int centerY)
        {
            IntPtr screenDC = IntPtr.Zero;
            IntPtr memDC = IntPtr.Zero;
            IntPtr bitmap = IntPtr.Zero;
            IntPtr oldBitmap = IntPtr.Zero;

            try
            {
                int size = CAPTURE_SIZE;
                int halfSize = size / 2;
                int magnifiedSize = size * MAGNIFICATION;

                // GDI/Win32 捕获屏幕区域
                screenDC = GetDC(IntPtr.Zero);
                memDC = CreateCompatibleDC(screenDC);
                bitmap = CreateCompatibleBitmap(screenDC, size, size);
                oldBitmap = SelectObject(memDC, bitmap);

                // 复制屏幕区域到内存位图
                BitBlt(memDC, 0, 0, size, size, screenDC, centerX - halfSize, centerY - halfSize, SRCCOPY);

                // 读取原始位图数据
                byte[] sourceData = new byte[size * size * 3];
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        uint pixel = GetPixel(memDC, x, y);
                        byte r = (byte)(pixel & 0x000000FF);
                        byte g = (byte)((pixel & 0x0000FF00) >> 8);
                        byte b = (byte)((pixel & 0x00FF0000) >> 16);

                        int sourceIndex = (y * size + x) * 3;
                        sourceData[sourceIndex] = b; // BGR 格式
                        sourceData[sourceIndex + 1] = g;
                        sourceData[sourceIndex + 2] = r;
                    }
                }

                // 放大像素（最近邻插值），耗时操作
                byte[] magnifiedData = new byte[magnifiedSize * magnifiedSize * 3];
                for (int y = 0; y < magnifiedSize; y++)
                {
                    int sourceY = y / MAGNIFICATION;
                    for (int x = 0; x < magnifiedSize; x++)
                    {
                        int sourceX = x / MAGNIFICATION;
                        int sourceIndex = (sourceY * size + sourceX) * 3;
                        int destIndex = (y * magnifiedSize + x) * 3;
                        magnifiedData[destIndex] = sourceData[sourceIndex];
                        magnifiedData[destIndex + 1] = sourceData[sourceIndex + 1];
                        magnifiedData[destIndex + 2] = sourceData[sourceIndex + 2];
                    }
                }

                // 创建 BitmapSource
                BitmapSource result = BitmapSource.Create(
                    magnifiedSize, magnifiedSize,
                    96, 96, // 默认 DPI
                    PixelFormats.Bgr24, null,
                    magnifiedData, magnifiedSize * 3);

                result.Freeze(); // 冻结 BitmapSource 以使其能够安全地在 UI 线程中使用
                return result; // 返回结果
            }
            catch
            {
                return null;
            }
            finally // 清理 GDI 资源
            {
                if (oldBitmap != IntPtr.Zero) SelectObject(memDC, oldBitmap);
                if (bitmap != IntPtr.Zero) DeleteObject(bitmap);
                if (memDC != IntPtr.Zero) DeleteDC(memDC);
                if (screenDC != IntPtr.Zero) ReleaseDC(IntPtr.Zero, screenDC);
            }
        }

        /// <summary>
        /// 获取屏幕像素颜色
        /// </summary>
        /// <param name="x"> 像素x坐标 </param>
        /// <param name="y"> 像素y坐标 </param>
        private (byte r, byte g, byte b) GetPixelColor(int x, int y)
        {
            IntPtr hdc = GetDC(IntPtr.Zero);
            try
            {
                uint pixel = GetPixel(hdc, x, y);
                ReleaseDC(IntPtr.Zero, hdc);

                // GetPixel 返回的 COLORREF 格式是 0x00BBGGRR（BGR格式）
                // 低字节 = R（红色），中字节 = G（绿色），高字节 = B（蓝色）
                byte r = (byte)(pixel & 0x000000FF);        // 低字节 = R
                byte g = (byte)((pixel & 0x0000FF00) >> 8);  // 中字节 = G
                byte b = (byte)((pixel & 0x00FF0000) >> 16); // 高字节 = B

                // 返回 RGB 格式：(R, G, B)
                return (r, g, b);
            }
            catch
            {
                ReleaseDC(IntPtr.Zero, hdc);
                return (255, 255, 255); // 默认白色
            }
        }
    }
}