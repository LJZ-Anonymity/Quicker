using ImageSharpImage = SixLabors.ImageSharp.Image;
using Rectangle = System.Windows.Shapes.Rectangle;
using Quicker.UserControls.SettingWindow;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Processing;
using Point = System.Windows.Point;
using System.Windows.Media.Imaging;
using System.Windows.Controls;
using Path = System.IO.Path;
using System.Windows.Shapes;
using SixLabors.ImageSharp;
using System.Windows.Input;
using System.Windows.Media;
using Quicker.Managers;
using Quicker.Helpers;
using System.Windows;
using WpfAnimatedGif;
using System.IO;

namespace Quicker.Windows.ToolWindows
{
    public partial class ImageCropWindow : Window
    {
        // ================== 控件相关 ==================
        public string CroppedImagePath { get; private set; } // 裁剪后图片的保存路径。
        public event Action<object, string> CropCompleted; // 裁剪完成时触发的事件，第一个参数为事件源，第二个参数为裁剪后图片的路径。

        // ================== 图片与裁剪相关 ==================
        private BitmapSource _originalBitmapSource; // 原始图片源。
        private double BorderHeight;// 裁剪框高度。
        private double aspectRatio; // 裁剪框宽高比。
        private double BorderWidth; // 裁剪框宽度。

        // ================== 状态变量 ==================
        private bool isDragging; // 是否正在拖动裁剪框。
        private bool isResizing;// 是否正在调整裁剪框大小。

        private string resizeHandle; // 当前正在调整的锚点名称。
        private Point mouseOffset; // 鼠标拖动时的偏移量。
        private Point resizeStartPoint; // 调整大小起始点。
        private double resizeStartWidth; // 调整大小起始宽度。
        private double resizeStartHeight; // 调整大小起始高度。
        private double resizeStartLeft; // 调整大小起始左侧位置。
        private double resizeStartTop; // 调整大小起始顶部位置。

        // ================== 颜色相关 ==================
        private Button _currentColorButton; // 当前正在选择颜色的按钮。

        // ================== 其它常量 ==================
        private const double MinCropSize = 50; // 裁剪框最小尺寸。
        private const double ScaleStep = 0.1; // 图片缩放步长。
        private const double MaxScale = 10.0; // 图片最大缩放比例。
        private const double MinScale = 0.1; // 图片最小缩放比例。

        /// <summary>
        /// 裁剪完成时调用的方法，触发 CropCompleted 事件并关闭窗口。
        /// </summary>
        /// <param name="path">裁剪后图片的保存路径</param>
        private void OnCropCompleted(string path)
        {
            CropCompleted?.Invoke(this, path);
            this.Close();
        }

        /// <summary>
        /// 图片裁剪窗口，传递宽高比
        /// </summary>
        /// <param name="filePath">图片路径</param>
        /// <param name="aspectRatio">宽高比（宽/高）</param>
        /// <param name="cornerRadius">边框圆角</param>
        public ImageCropWindow(string filePath, double aspectRatio, CornerRadius cornerRadius)
        {
            InitializeComponent();
            this.aspectRatio = aspectRatio;
            CropBorder.CornerRadius = cornerRadius;
            CropBorder.Width = 200;
            CropBorder.Height = 200 / aspectRatio;
            DataContext = this;

            LoadAndDisplayImage(filePath); // 加载图片并设置到控件
            InitColorButtonsAndControls(); // 初始化颜色按钮和控件
        }

        /// <summary>
        /// 加载图片并显示到CropImage控件，同时处理GIF动图和提示
        /// </summary>
        /// <param name="filePath">图片路径</param>
        private void LoadAndDisplayImage(string filePath)
        {
            var iconManager = new IconManager(); // 创建IconManager实例
            var result = iconManager.ProcessIcon(filePath); // 处理图片
            string ext = Path.GetExtension(filePath).ToLower(); // 获取文件扩展名
            if (result is BitmapImage bitmapImage) // 静态图片或静态GIF
            {
                ImageBehavior.SetAnimatedSource(CropImage, bitmapImage); // 显示静态图片或静态GIF
                _originalBitmapSource = bitmapImage; // 保存原图
            }
            else if (result is BitmapFrame[] frames && frames.Length > 1) // 动图GIF，直接用WpfAnimatedGif显示
            {
                var gifImage = new BitmapImage(new Uri(filePath, UriKind.Absolute)); // 创建BitmapImage实例
                ImageBehavior.SetAnimatedSource(CropImage, gifImage); // 显示动图GIF
                _originalBitmapSource = gifImage; // 保存原图
            }
        }

        /// <summary>
        /// 初始化颜色相关按钮和控件的Tag、背景、边框色
        /// </summary>
        private void InitColorButtonsAndControls()
        {
            var handleBrush = HandleColorButton.Tag as SolidColorBrush; // 获取锚点颜色
            UpdateAllResizeHandles(handleBrush); // 初始化锚点颜色
        }

        // 监听键盘方向键，微调裁剪框位置
        private void ImageCropWindow_KeyDown(object sender, KeyEventArgs e)
        {
            double left = Canvas.GetLeft(CropBorder);
            double top = Canvas.GetTop(CropBorder);
            switch (e.Key)
            {
                case Key.Up:
                    Canvas.SetTop(CropBorder, top - 1);
                    break;
                case Key.Down:
                    Canvas.SetTop(CropBorder, top + 1);
                    break;
                case Key.Left:
                    Canvas.SetLeft(CropBorder, left - 1);
                    break;
                case Key.Right:
                    Canvas.SetLeft(CropBorder, left + 1);
                    break;
                default:
                    return;
            }
            UpdateMask(); // 移动后刷新遮罩
        }

        private void CropBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is Rectangle) return; // 如果鼠标点击的是调整手柄，不处理拖动
            isDragging = true;
            // 记录鼠标在Border内的偏移
            mouseOffset = e.GetPosition(CropBorder);
            CropBorder.CaptureMouse();
            HideResizeHandles(); // 拖动时隐藏调整手柄
        }

        // 更新蒙版
        private void UpdateMask()
        {
            // 获取Grid在Canvas中的位置
            double gridLeft = Canvas.GetLeft(ImageGrid);
            double gridTop = Canvas.GetTop(ImageGrid);

            // 获取CropBorder在Canvas中的位置
            double borderLeft = Canvas.GetLeft(CropBorder);
            double borderTop = Canvas.GetTop(CropBorder);

            // 计算CropBorder在Grid内的相对位置
            double left = borderLeft - gridLeft;
            double top = borderTop - gridTop;
            double width = CropBorder.ActualWidth;
            double height = CropBorder.ActualHeight;

            // 整个Grid的区域
            var gridRect = new RectangleGeometry(new Rect(0, 0, ImageGrid.ActualWidth, ImageGrid.ActualHeight));
            // 镂空区域
            var holeRect = new RectangleGeometry(new Rect(left, top, width, height), CropBorder.CornerRadius.TopLeft, CropBorder.CornerRadius.TopLeft);

            // 合成：大矩形减去小矩形
            var combined = new CombinedGeometry(GeometryCombineMode.Exclude, gridRect, holeRect);

            // 用DrawingBrush实现镂空
            var drawing = new GeometryDrawing
            {
                Geometry = combined,
                Brush = Brushes.White // OpacityMask的白色部分为不透明，黑色为透明
            };
            var brush = new DrawingBrush(drawing);
            MaskRect.OpacityMask = brush;
        }

        // 在窗口加载和每次拖动后都调用
        private void ImageCropWindow_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateMask(); // 更新蒙版
        }

        // 移动鼠标时，更新CropBorder的位置
        private void CropBorder_MouseMove(object sender, MouseEventArgs e)
        {
            if (isResizing) return; // 如果正在调整大小，不处理拖动
            if (isDragging)
            {
                // 获取Grid在Canvas中的位置
                double gridLeft = Canvas.GetLeft(ImageGrid);
                double gridTop = Canvas.GetTop(ImageGrid);

                // 鼠标在Canvas中的位置
                var position = e.GetPosition((Canvas)CropBorder.Parent);

                // 计算新的Left/Top
                double newLeft = position.X - mouseOffset.X;
                double newTop = position.Y - mouseOffset.Y;

                // 限制在Grid区域内
                newLeft = Math.Max(gridLeft, Math.Min(newLeft, gridLeft + ImageGrid.ActualWidth - CropBorder.ActualWidth));
                newTop = Math.Max(gridTop, Math.Min(newTop, gridTop + ImageGrid.ActualHeight - CropBorder.ActualHeight));

                Canvas.SetLeft(CropBorder, newLeft);
                Canvas.SetTop(CropBorder, newTop);

                UpdateMask(); // 更新蒙版
            }
        }

        // 鼠标松开时，停止拖动
        private void CropBorder_MouseLeftButtonUp(object sender, MouseEventArgs e)
        {
            isDragging = false;
            CropBorder.ReleaseMouseCapture();
            ShowResizeHandles(); // 拖动结束后显示调整手柄
            UpdateMask();
        }

        // 调整大小手柄的鼠标按下事件
        private void ResizeHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            isResizing = true; // 开始调整大小
            var handle = sender as FrameworkElement; // 获取调整手柄
            resizeHandle = handle.Tag.ToString(); // 获取手柄位置

            // 记录开始调整时的状态
            resizeStartPoint = e.GetPosition((Canvas)CropBorder.Parent);
            resizeStartWidth = CropBorder.ActualWidth;
            resizeStartHeight = CropBorder.ActualHeight;
            resizeStartLeft = Canvas.GetLeft(CropBorder);
            resizeStartTop = Canvas.GetTop(CropBorder);

            handle.CaptureMouse(); // 捕获鼠标
            e.Handled = true; // 阻止事件传播到CropBorder
        }

        // 鼠标移动时，调整裁剪框大小和位置
        private void ResizeHandle_MouseMove(object sender, MouseEventArgs e)
        {
            if (!isResizing) return; // 如果没有开始调整大小，不处理

            var currentPoint = e.GetPosition((Canvas)CropBorder.Parent); // 当前鼠标位置
            double deltaX = currentPoint.X - resizeStartPoint.X; // 鼠标水平方向移动距离
            double deltaY = currentPoint.Y - resizeStartPoint.Y; // 鼠标垂直方向移动距离

            double newWidth, newHeight, newLeft, newTop; // 新的宽高和位置
            CalculateNewSizeAndPosition(deltaX, deltaY, out newWidth, out newHeight, out newLeft, out newTop); // 根据手柄类型和鼠标移动距离计算新的宽高和位置
            FixSizeAndPositionWithinGrid(ref newWidth, ref newHeight, ref newLeft, ref newTop); // 边界检查和修正
            ApplyCropBorderSizeAndPosition(newWidth, newHeight, newLeft, newTop); // 应用新的尺寸和位置
            UpdateMask(); // 更新遮罩
        }

        /// <summary>
        /// 根据手柄类型和鼠标移动距离，计算新的宽高和位置
        /// </summary>
        /// <param name="deltaX">水平方向移动距离</param>
        /// <param name="deltaY">垂直方向移动距离</param>
        /// <param name="newWidth">新的宽</param>
        /// <param name="newHeight">新的高</param>
        /// <param name="newLeft">新的左边距</param>
        /// <param name="newTop">新的上边距</param>
        private void CalculateNewSizeAndPosition(double deltaX, double deltaY, out double newWidth, out double newHeight, out double newLeft, out double newTop)
        {
            newWidth = resizeStartWidth;
            newHeight = resizeStartHeight;
            newLeft = resizeStartLeft;
            newTop = resizeStartTop;
            switch (resizeHandle)
            {
                case "TopLeft":
                    newWidth = Math.Max(MinCropSize, resizeStartWidth - deltaX);
                    newHeight = newWidth / aspectRatio;
                    newLeft = resizeStartLeft + (resizeStartWidth - newWidth);
                    newTop = resizeStartTop + (resizeStartHeight - newHeight);
                    break;
                case "TopRight":
                    newWidth = Math.Max(MinCropSize, resizeStartWidth + deltaX);
                    newHeight = newWidth / aspectRatio;
                    newTop = resizeStartTop + (resizeStartHeight - newHeight);
                    break;
                case "BottomLeft":
                    newWidth = Math.Max(MinCropSize, resizeStartWidth - deltaX);
                    newHeight = newWidth / aspectRatio;
                    newLeft = resizeStartLeft + (resizeStartWidth - newWidth);
                    break;
                case "BottomRight":
                    newWidth = Math.Max(MinCropSize, resizeStartWidth + deltaX);
                    newHeight = newWidth / aspectRatio;
                    break;
                case "Top":
                    newHeight = Math.Max(MinCropSize, resizeStartHeight - deltaY);
                    newWidth = newHeight * aspectRatio;
                    newTop = resizeStartTop + (resizeStartHeight - newHeight);
                    break;
                case "Bottom":
                    newHeight = Math.Max(MinCropSize, resizeStartHeight + deltaY);
                    newWidth = newHeight * aspectRatio;
                    break;
                case "Left":
                    newWidth = Math.Max(MinCropSize, resizeStartWidth - deltaX);
                    newHeight = newWidth / aspectRatio;
                    newLeft = resizeStartLeft + (resizeStartWidth - newWidth);
                    break;
                case "Right":
                    newWidth = Math.Max(MinCropSize, resizeStartWidth + deltaX);
                    newHeight = newWidth / aspectRatio;
                    break;
            }
        }

        /// <summary>
        /// 检查并修正裁剪框不超出ImageGrid边界，并保持宽高比和最小尺寸
        /// </summary>
        /// <param name="newWidth">新的宽</param>
        /// <param name="newHeight">新的高</param>
        /// <param name="newLeft">新的左边距</param>
        /// <param name="newTop">新的上边距</param>
        private void FixSizeAndPositionWithinGrid(ref double newWidth, ref double newHeight, ref double newLeft, ref double newTop)
        {
            double gridLeft = Canvas.GetLeft(ImageGrid); // Grid在Canvas中的位置
            double gridTop = Canvas.GetTop(ImageGrid); // Grid在Canvas中的位置
            double gridRight = gridLeft + ImageGrid.ActualWidth; // Grid右边界
            double gridBottom = gridTop + ImageGrid.ActualHeight; // Grid下边界
            if (newLeft < gridLeft) // 左边界
            {
                newLeft = gridLeft;
                newWidth = resizeStartWidth - (gridLeft - resizeStartLeft);
                newHeight = newWidth / aspectRatio;
            }
            if (newTop < gridTop) // 上边界
            {
                newTop = gridTop;
                newHeight = resizeStartHeight - (gridTop - resizeStartTop);
                newWidth = newHeight * aspectRatio;
            }
            if (newLeft + newWidth > gridRight) // 右边界
            {
                newWidth = gridRight - newLeft;
                newHeight = newWidth / aspectRatio;
            }
            if (newTop + newHeight > gridBottom) // 下边界
            {
                newHeight = gridBottom - newTop;
                newWidth = newHeight * aspectRatio;
            }

            if (newWidth < MinCropSize)
            {
                newWidth = MinCropSize;
                newHeight = newWidth / aspectRatio;
            }
            if (newHeight < MinCropSize)
            {
                newHeight = MinCropSize;
                newWidth = newHeight * aspectRatio;
            }
        }

        /// <summary>
        /// 应用新的裁剪框尺寸和位置，并同步绑定属性
        /// </summary>
        /// <param name="newWidth">新的宽</param>
        /// <param name="newHeight">新的高</param>
        /// <param name="newLeft">新的左边距</param>
        /// <param name="newTop">新的上边距</param>
        private void ApplyCropBorderSizeAndPosition(double newWidth, double newHeight, double newLeft, double newTop)
        {
            CropBorder.Width = newWidth; // 先设置宽高，再设置Left/Top，避免闪烁
            CropBorder.Height = newHeight;
            Canvas.SetLeft(CropBorder, newLeft); // 再设置Left/Top，避免闪烁
            Canvas.SetTop(CropBorder, newTop);

            BorderWidth = newWidth;
            BorderHeight = newHeight;
        }

        // 调整大小手柄的鼠标松开事件
        private void ResizeHandle_MouseLeftButtonUp(object sender, MouseEventArgs e)
        {
            isResizing = false;
            var handle = sender as FrameworkElement;
            handle.ReleaseMouseCapture();
            UpdateMask();
        }

        /// <summary>
        /// 获取所有调整手柄的集合
        /// </summary>
        private Rectangle[] GetAllResizeHandles()
        {
            return new[]
            {
                ResizeHandleTopLeft,
                ResizeHandleTopRight,
                ResizeHandleBottomLeft,
                ResizeHandleBottomRight,
                ResizeHandleTop,
                ResizeHandleBottom,
                ResizeHandleLeft,
                ResizeHandleRight
            };
        }

        /// <summary>
        /// 更新所有调整手柄的填充颜色
        /// </summary>
        /// <param name="brush">要应用的画刷</param>
        private void UpdateAllResizeHandles(Brush brush)
        {
            foreach (var handle in GetAllResizeHandles())
            {
                handle.Fill = brush;
            }
        }

        // 隐藏所有调整手柄
        private void HideResizeHandles()
        {
            UpdateAllResizeHandles(Brushes.Transparent);
        }

        // 显示所有调整手柄
        private void ShowResizeHandles()
        {
            var brush = HandleColorButton.Tag as SolidColorBrush ?? new SolidColorBrush(Colors.Red);
            UpdateAllResizeHandles(brush);
        }

        // 顺时针旋转90°
        private void RotateRightButton_Click(object sender, RoutedEventArgs e)
        {
            ApplyTransform(new RotateTransform(90, 0.5, 0.5), true);
        }

        // 逆时针旋转90°
        private void RotateLeftButton_Click(object sender, RoutedEventArgs e)
        {
            ApplyTransform(new RotateTransform(-90, 0.5, 0.5), true);
        }

        // 垂直翻转
        private void FlipVerticalButton_Click(object sender, RoutedEventArgs e)
        {
            ApplyTransform(new ScaleTransform(1, -1, 0.5, 0.5), false);
        }

        // 水平翻转
        private void FlipHorizontalButton_Click(object sender, RoutedEventArgs e)
        {
            ApplyTransform(new ScaleTransform(-1, 1, 0.5, 0.5), false);
        }

        /// <summary>
        /// 通用变换方法
        /// </summary>
        /// <param name="transform"> 变换对象 </param>
        /// <param name="isRotate"> 是否旋转 </param>
        private void ApplyTransform(Transform transform, bool isRotate)
        {
            // 防止空引用
            BitmapSource source = CropImage.Source as BitmapSource ?? _originalBitmapSource;
            if (source == null) return;

            // 组合上一次的变换
            var group = new TransformGroup(); // 组合变换
            if (CropImage.LayoutTransform is TransformGroup oldGroup)
            {
                foreach (var t in oldGroup.Children)
                    group.Children.Add(t);
            }
            else if (CropImage.LayoutTransform != null && !(CropImage.LayoutTransform is MatrixTransform mt && mt.Matrix.IsIdentity))
            {
                group.Children.Add(CropImage.LayoutTransform);
            }

            group.Children.Add(transform); // 旋转时，叠加旋转；翻转时，叠加翻转
            CropImage.LayoutTransform = group; // 应用变换
            if (isRotate) // 旋转90°后，宽高要互换
            {
                double temp = BorderWidth;
                BorderWidth = BorderHeight;
                BorderHeight = temp;
            }

            UpdateMask(); // 更新蒙版
        }

        // 点击按钮保存图片
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            BitmapSource bitmapSource = CropImage.Source as BitmapSource; // 图片源
            string dir = AppPathHelper.BackgroundImagesFolder; // 保存目录
            string originalFilePath = _originalBitmapSource is BitmapImage bmpImg && bmpImg.UriSource != null ? bmpImg.UriSource.LocalPath : null;
            string fileName = $"{DateTimeOffset.Now.ToUnixTimeMilliseconds():x}.png"; // 默认保存文件名
            string filePath = System.IO.Path.Combine(dir, fileName); // 默认保存路径
            string savedPath;
            if (UseOriginalImageCheckBox.IsChecked == true) // 保存原图
            {
                savedPath = SaveOriginalImageWithFallback(bitmapSource, dir, filePath, originalFilePath);
            }
            else
            {
                if (!TryGetCropRect(bitmapSource, out var cropRect, out var scaleInfo)) return; // 计算裁剪区域失败
                savedPath = SaveCroppedImageWithFallback(bitmapSource, cropRect, dir, filePath, originalFilePath);
            }
            if (string.IsNullOrEmpty(savedPath)) return; // 保存失败
            CroppedImagePath = savedPath; // 实际保存路径
            OnCropCompleted(CroppedImagePath); // 通知完成
        }

        /// <summary>
        /// 优先保存为PNG，失败则用原格式保存原图，返回实际保存路径，失败返回null
        /// </summary>
        /// <param name="bitmapSource"> 图片源 </param>
        /// <param name="dir"> 保存目录 </param>
        /// <param name="filePath"> 保存路径 </param>
        /// <param name="originalFilePath"> 原图路径 </param>
        /// <returns>实际保存路径，失败返回 null </returns>
        private string SaveOriginalImageWithFallback(BitmapSource bitmapSource, string dir, string filePath, string originalFilePath)
        {
            if (bitmapSource == null)
            {
                ShowToast("图片加载失败！", ToastType.Error);
                return null;
            }
            if (!Directory.Exists(dir)) // 创建保存目录
                Directory.CreateDirectory(dir);
            try // 优先尝试用PNG编码器保存图片
            {
                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    PngBitmapEncoder encoder = new(); // 编码器
                    encoder.Frames.Add(BitmapFrame.Create(bitmapSource)); // 添加图片
                    encoder.Save(fileStream); // 保存图片
                }
                return filePath; // 保存成功，返回PNG路径
            }
            catch // PNG保存失败，尝试用原格式保存
            {
                try // 检查原图路径有效且文件存在
                {
                    if (!string.IsNullOrEmpty(originalFilePath) && File.Exists(originalFilePath)) // 原图路径有效且文件存在
                    {
                        string ext = Path.GetExtension(originalFilePath).ToLower(); // 获取原图扩展名
                        string fallbackPath = Path.ChangeExtension(filePath, ext); // 生成原格式的目标路径
                        File.Copy(originalFilePath, fallbackPath, true); // 直接复制原图文件
                        ShowToast("PNG保存失败，已按原格式保存。", ToastType.Error);
                        return fallbackPath; // 返回原格式路径
                    }
                }
                catch { /* 原格式保存也失败，进入下方错误提示 */ }
                ShowToast("图片保存失败！", ToastType.Error); // 所有方式都失败
                return null;
            }
        }

        /// <summary>
        /// 优先保存为PNG，失败则用原格式保存裁剪图，返回实际保存路径，失败返回null
        /// </summary>
        /// <param name="bitmapSource"> 图片源 </param>
        /// <param name="cropRect"> 裁剪区域 </param>
        /// <param name="dir"> 保存目录 </param>
        /// <param name="filePath"> 保存路径 </param>
        /// <param name="originalFilePath"> 原图路径 </param>
        /// <returns>实际保存路径，失败返回 null </returns>
        private string SaveCroppedImageWithFallback(BitmapSource bitmapSource, Int32Rect cropRect, string dir, string filePath, string originalFilePath)
        {
            if (TrySaveGifAnimation(originalFilePath, cropRect, dir, filePath, out string gifPath)) return gifPath; // 尝试GIF动图裁剪
            if (TrySaveAsPng(bitmapSource, cropRect, dir, filePath)) return filePath; // 尝试PNG保存
            return TrySaveAsOriginalFormat(bitmapSource, cropRect, dir, filePath, originalFilePath); // PNG保存失败，尝试用原格式保存
        }

        /// <summary>
        /// 尝试保存GIF动图裁剪
        /// </summary>
        /// <param name="originalFilePath">原始文件路径</param>
        /// <param name="cropRect">裁剪区域</param>
        /// <param name="dir">保存目录</param>
        /// <param name="filePath">目标文件路径</param>
        /// <param name="gifPath">输出：GIF文件路径</param>
        /// <returns>成功返回true，否则false</returns>
        private bool TrySaveGifAnimation(string originalFilePath, Int32Rect cropRect, string dir, string filePath, out string gifPath)
        {
            gifPath = null;
            if (!IsGifFile(originalFilePath)) return false; // 检查是否为GIF动图

            // 尝试GIF动图裁剪
            string gifTargetPath = Path.ChangeExtension(filePath, ".gif");
            if (SaveGifAnimation(originalFilePath, cropRect, dir, gifTargetPath))
            {
                gifPath = gifTargetPath;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 检查是否为GIF文件
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>是否为GIF文件</returns>
        private bool IsGifFile(string filePath)
        {
            return !string.IsNullOrEmpty(filePath) && 
                   Path.GetExtension(filePath).ToLower() == ".gif" &&
                   _originalBitmapSource is BitmapImage bmpImg && 
                   bmpImg.UriSource != null;
        }

        /// <summary>
        /// 尝试保存为PNG格式
        /// </summary>
        /// <param name="bitmapSource">图片源</param>
        /// <param name="cropRect">裁剪区域</param>
        /// <param name="dir">保存目录</param>
        /// <param name="filePath">文件路径</param>
        /// <returns>成功返回true，否则false</returns>
        private bool TrySaveAsPng(BitmapSource bitmapSource, Int32Rect cropRect, string dir, string filePath)
        {
            try
            {
                EnsureDirectoryExists(dir); // 创建保存目录
                var cropped = new CroppedBitmap(bitmapSource, cropRect); // 裁剪图片
                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    PngBitmapEncoder encoder = new(); // 编码器
                    encoder.Frames.Add(BitmapFrame.Create(cropped)); // 添加图片
                    encoder.Save(fileStream); // 保存图片
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 尝试用原格式保存
        /// </summary>
        /// <param name="bitmapSource">图片源</param>
        /// <param name="cropRect">裁剪区域</param>
        /// <param name="dir">保存目录</param>
        /// <param name="filePath">文件路径</param>
        /// <param name="originalFilePath">原始文件路径</param>
        /// <returns>保存成功返回文件路径，失败返回null</returns>
        private string TrySaveAsOriginalFormat(BitmapSource bitmapSource, Int32Rect cropRect, string dir, string filePath, string originalFilePath)
        {
            try
            {
                if (!IsValidOriginalFile(originalFilePath))
                    return null;

                string ext = Path.GetExtension(originalFilePath).ToLower();
                string fallbackPath = Path.ChangeExtension(filePath, ext);
                if (SaveCroppedImageAsFormat(bitmapSource, cropRect, fallbackPath, ext))
                {
                    ShowToast($"PNG保存失败，已按原格式{ext}保存。", ToastType.Error);
                    return fallbackPath;
                }
            }
            catch { }

            ShowToast("图片保存失败，已跳过。", ToastType.Error);
            return null;
        }

        /// <summary>
        /// 检查原始文件是否有效
        /// </summary>
        /// <param name="originalFilePath">原始文件路径</param>
        /// <returns>文件是否有效</returns>
        private bool IsValidOriginalFile(string originalFilePath)
        {
            return !string.IsNullOrEmpty(originalFilePath) && File.Exists(originalFilePath);
        }

        /// <summary>
        /// 确保目录存在
        /// </summary>
        /// <param name="dir">目录路径</param>
        private void EnsureDirectoryExists(string dir)
        {
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }

        /// <summary>
        /// 将裁剪后的图片保存为指定格式
        /// </summary>
        /// <param name="bitmapSource">图片源</param>
        /// <param name="cropRect">裁剪区域</param>
        /// <param name="outputPath">输出路径</param>
        /// <param name="format">目标格式</param>
        /// <returns>成功返回true，否则false</returns>
        private bool SaveCroppedImageAsFormat(BitmapSource bitmapSource, Int32Rect cropRect, string outputPath, string format)
        {
            try
            {
                var cropped = new CroppedBitmap(bitmapSource, cropRect);
                using (var ms = new MemoryStream())
                {
                    // 先用WPF保存为BMP到内存流
                    BmpBitmapEncoder bmpEncoder = new();
                    bmpEncoder.Frames.Add(BitmapFrame.Create(cropped));
                    bmpEncoder.Save(ms);
                    ms.Seek(0, SeekOrigin.Begin);

                    // 用ImageSharp加载BMP流并保存为指定格式
                    using (var image = ImageSharpImage.Load(ms))
                    {
                        SaveImageWithEncoder(image, outputPath, format);
                    }
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 使用指定的编码器保存图片
        /// </summary>
        /// <param name="image">图片对象</param>
        /// <param name="outputPath">输出路径</param>
        /// <param name="format">格式</param>
        private void SaveImageWithEncoder(ImageSharpImage image, string outputPath, string format)
        {
            switch (format)
            {
                case ".jpg":
                case ".jpeg":
                    image.Save(outputPath, new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder());
                    break;
                case ".bmp":
                    image.Save(outputPath, new SixLabors.ImageSharp.Formats.Bmp.BmpEncoder());
                    break;
                case ".gif":
                    image.Save(outputPath, new GifEncoder());
                    break;
                case ".webp":
                    image.Save(outputPath, new SixLabors.ImageSharp.Formats.Webp.WebpEncoder());
                    break;
                case ".tiff":
                    image.Save(outputPath, new SixLabors.ImageSharp.Formats.Tiff.TiffEncoder());
                    break;
                default:
                    image.Save(outputPath, new SixLabors.ImageSharp.Formats.Png.PngEncoder()); // 兜底
                    break;
            }
        }

        /// <summary>
        /// 保存GIF动图裁剪，保留所有帧的动画效果
        /// </summary>
        /// <param name="originalFilePath">原始GIF文件路径</param>
        /// <param name="cropRect">裁剪区域</param>
        /// <param name="dir">保存目录</param>
        /// <param name="filePath">保存路径</param>
        /// <returns>保存成功返回 true，否则 false</returns>
        private bool SaveGifAnimation(string originalFilePath, Int32Rect cropRect, string dir, string filePath)
        {
            try
            {
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                using (var image = ImageSharpImage.Load(originalFilePath))
                {
                    // 直接对整个图像进行裁剪，ImageSharp会自动处理所有帧
                    image.Mutate(ctx => ctx.Crop(new SixLabors.ImageSharp.Rectangle(cropRect.X, cropRect.Y, cropRect.Width, cropRect.Height)));
                    image.Save(filePath, new GifEncoder()); // 保存为GIF，保留所有元数据
                }
                return true;
            }
            catch (Exception ex)
            {
                ShowToast($"GIF动图裁剪失败：{ex.Message}", ToastType.Error);
                return false;
            }
        }

        /// <summary>
        /// 获取裁剪区域在原图上的像素矩形
        /// </summary>
        /// <param name="bitmapSource">原始图片源</param>
        /// <param name="cropRect">输出：裁剪区域</param>
        /// <param name="scaleInfo">输出：缩放信息</param>
        /// <returns>成功返回 true，否则 false</returns>
        private bool TryGetCropRect(BitmapSource bitmapSource, out Int32Rect cropRect, out (double scale, double offsetX, double offsetY) scaleInfo)
        {
            cropRect = new Int32Rect(); // 裁剪区域
            scaleInfo = (0, 0, 0); // 缩放信息
            if (bitmapSource == null)
            {
                ShowToast("图片加载失败！", ToastType.Error); // 图片加载失败
                return false;
            }

            // 获取Grid和Border的相对位置
            double gridLeft = Canvas.GetLeft(ImageGrid);
            double gridTop = Canvas.GetTop(ImageGrid);
            double borderLeft = Canvas.GetLeft(CropBorder);
            double borderTop = Canvas.GetTop(CropBorder);

            // Border在Grid内的相对位置
            double cropX = borderLeft - gridLeft;
            double cropY = borderTop - gridTop;
            double cropWidth = CropBorder.ActualWidth;
            double cropHeight = CropBorder.ActualHeight;

            // 计算缩放关系
            double imgControlWidth = ImageGrid.ActualWidth;
            double imgControlHeight = ImageGrid.ActualHeight;
            double bmpWidth = bitmapSource.PixelWidth;
            double bmpHeight = bitmapSource.PixelHeight;

            double scale = Math.Min(imgControlWidth / bmpWidth, imgControlHeight / bmpHeight);

            // 图片实际显示的大小
            double showWidth = bmpWidth * scale;
            double showHeight = bmpHeight * scale;

            // 图片在控件中的偏移（居中）
            double offsetX = (imgControlWidth - showWidth) / 2;
            double offsetY = (imgControlHeight - showHeight) / 2;

            // Border在原图上的像素坐标
            double x = (cropX - offsetX) / scale;
            double y = (cropY - offsetY) / scale;
            double w = cropWidth / scale;
            double h = cropHeight / scale;

            // 边界修正
            x = Math.Max(0, x);
            y = Math.Max(0, y);
            w = Math.Min(bmpWidth - x, w);
            h = Math.Min(bmpHeight - y, h);

            if (w <= 0 || h <= 0)
            {
                ShowToast("裁剪区域无效！", ToastType.Error);
                return false;
            }

            cropRect = new Int32Rect((int)x, (int)y, (int)w, (int)h);
            scaleInfo = (scale, offsetX, offsetY);
            return true;
        }

        /// <summary>
        /// 显示消息提示
        /// </summary>
        /// <param name="message">消息内容</param>
        /// <param name="type">消息类型</param>
        private void ShowToast(string message, ToastType type)
        {
            using var toast = new ToastManager();
            toast.Show(message, type);
        }

        // 点击按钮关闭窗口
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close(); // 关闭窗口
        }

        // 点击按钮弹出颜色选择器
        private void ColorButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn) // 防止空引用
            {
                _currentColorButton = btn; // 保存当前选中的按钮
                var brush = btn.Tag as SolidColorBrush ?? new SolidColorBrush(Colors.Black); // 取出按钮的颜色
                PopupColorPicker.SelectedColor = brush.Color; // 弹出颜色选择器
                ColorPickerPopup.PlacementTarget = btn; // 设置弹出位置
                ColorPickerPopup.IsOpen = true; // 打开颜色选择器
            }
        }

        // 颜色选择器选择颜色后，应用到按钮上
        private void PopupColorPicker_SelectedColorChanged(object sender, ColorChangedEventArgs e)
        {
            if (_currentColorButton == null) return; // 防止空引用
            var newBrush = new SolidColorBrush(e.NewColor); // 取出选择的颜色
            _currentColorButton.Tag = newBrush; // 保存到按钮的Tag属性

            if (_currentColorButton == BackgroundColorButton)
                ImageGrid.Background = newBrush;
            else if (_currentColorButton == BorderColorButton)
                CropBorder.BorderBrush = newBrush;
            else if (_currentColorButton == HandleColorButton)
            {
                // 直接更新所有锚点的Fill属性，因为Tag不是依赖属性，绑定不会自动更新
                UpdateAllResizeHandles(newBrush);
            }
        }

        // 关闭窗口释放资源
        private void ImageCropWindow_Closed(object sender, EventArgs e)
        {
            if (PopupColorPicker != null)
                PopupColorPicker.SelectedColorChanged -= PopupColorPicker_SelectedColorChanged;

            // 释放图片资源
            if (CropImage.Source is BitmapImage bitmapImage)
            {
                CropImage.Source = null;
                bitmapImage.StreamSource?.Dispose();
            }
            _originalBitmapSource = null;
            _currentColorButton = null;
            CropCompleted = null;

            // 强制垃圾回收
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        /// <summary>
        /// 点击"更改图片"按钮后弹出文件选择对话框，支持选择普通图片和SVG图片，并加载到裁剪窗口。
        /// </summary>
        private void ChangeImageButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "图片文件|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.svg;*.ico",
                Title = "选择图片"
            }; // 创建文件选择对话框，过滤支持的图片格式，包括SVG
            if (dialog.ShowDialog() == true) // 如果用户选择了文件并点击"打开"
            {
                try // 获取文件扩展名，判断是否为SVG
                {
                    string ext = Path.GetExtension(dialog.FileName).ToLower();
                    var iconManager = new IconManager();
                    var result = iconManager.ProcessIcon(dialog.FileName);
                    if (result is BitmapImage bitmapImage)
                    {
                        ImageBehavior.SetAnimatedSource(CropImage, bitmapImage);
                        _originalBitmapSource = bitmapImage;
                    }
                    else if (result is BitmapFrame[] frames && frames.Length > 1)
                    {
                        var gifImage = new BitmapImage(new Uri(dialog.FileName, UriKind.Absolute));
                        ImageBehavior.SetAnimatedSource(CropImage, gifImage);
                        _originalBitmapSource = gifImage;
                    }
                }
                catch
                {
                    ShowToast("图片加载失败！", ToastType.Error);
                }
            }
        }
    }
}