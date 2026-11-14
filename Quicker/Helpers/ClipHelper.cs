using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Quicker.Helpers
{
    /// <summary>
    /// 附加属性帮助类，用于为按钮动态设置自定义裁剪（圆角）
    /// </summary>
    public static class ClipHelper
    {
        // 定义附加属性 EnableCustomClip，控制是否启用自定义裁剪
        public static readonly DependencyProperty EnableCustomClipProperty =
            DependencyProperty.RegisterAttached("EnableCustomClip", typeof(bool), typeof(ClipHelper), new PropertyMetadata(false, OnEnableCustomClipChanged));

        // 定义附加属性 ClipToBounds，控制是否裁剪超出边界的子元素
        public static readonly DependencyProperty ClipToBoundsProperty =
            DependencyProperty.RegisterAttached("ClipToBounds", typeof(bool), typeof(ClipHelper), new PropertyMetadata(false, OnClipToBoundsChanged));

        // 设置附加属性方法
        public static void SetEnableCustomClip(UIElement element, bool value) => element.SetValue(EnableCustomClipProperty, value);

        // 获取附加属性方法
        public static bool GetEnableCustomClip(UIElement element) => (bool)element.GetValue(EnableCustomClipProperty);

        // 设置裁剪边界属性
        public static void SetClipToBounds(UIElement element, bool value) => element.SetValue(ClipToBoundsProperty, value);

        // 获取裁剪边界属性
        public static bool GetClipToBounds(UIElement element) => (bool)element.GetValue(ClipToBoundsProperty);

        // 附加属性值变化时的回调
        private static void OnEnableCustomClipChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Button btn)
            {
                if ((bool)e.NewValue)
                {
                    UpdateButtonClip(btn);
                }
                else
                {
                    btn.Clip = null;
                }
            }
        }

        // 裁剪边界属性变化时的回调
        private static void OnClipToBoundsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Border border)
            {
                if ((bool)e.NewValue)
                {
                    border.SizeChanged += Border_SizeChanged; // 启用时，注册 SizeChanged 事件
                    UpdateBorderClip(border);
                }
                else
                {
                    border.SizeChanged -= Border_SizeChanged; // 关闭时，移除事件并清除裁剪
                    border.Clip = null;
                }
            }
        }

        // Border 尺寸变化时，更新裁剪路径
        private static void Border_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (sender is Border border)
            {
                UpdateBorderClip(border); // 更新裁剪路径
            }
        }

        /// <summary>
        /// 为 Border 设置裁剪路径，使其子元素超出边界时被裁剪
        /// </summary>
        /// <param name="border">需要裁剪的 Border</param>
        public static void UpdateBorderClip(Border border)
        {
            double width = border.ActualWidth; // 边框实际宽度
            double height = border.ActualHeight; // 边框实际高度
            double cornerRadius = border.CornerRadius.TopLeft; // 直接用Border的CornerRadius
            if (cornerRadius > 0)
            {
                // 创建圆角矩形裁剪路径
                var geometry = new RectangleGeometry(new Rect(0, 0, width, height), cornerRadius, cornerRadius);
                border.Clip = geometry;
            }
            else
            {
                // 创建矩形裁剪路径
                var geometry = new RectangleGeometry(new Rect(0, 0, width, height));
                border.Clip = geometry;
            }
        }

        /// <summary>
        /// 根据按钮名称，动态设置左下角或右下角圆角裁剪，圆角半径固定为5
        /// </summary>
        /// <param name="btn">需要裁剪的按钮</param>
        private static void UpdateButtonClip(Button btn)
        {
            double height = btn.ActualHeight; // 按钮实际高度
            double width = btn.ActualWidth;   // 按钮实际宽度
            double radius = 5;                // 固定圆角半径

            // 判断是左下角还是右下角圆角（通过按钮 Name 区分）
            if (btn.Name.Contains("LeftBottom")) // 左下角圆角裁剪
            {
                var geometry = new PathGeometry();
                var figure = new PathFigure { StartPoint = new Point(0, 0), IsClosed = true };
                figure.Segments.Add(new LineSegment(new Point(width, 0), true));                // 上边
                figure.Segments.Add(new LineSegment(new Point(width, height), true));           // 右边
                figure.Segments.Add(new LineSegment(new Point(radius, height), true));          // 下边（右下到左下圆角起点）
                figure.Segments.Add(new ArcSegment(new Point(0, height - radius), new Size(radius, radius), 0, false, SweepDirection.Clockwise, true)); // 左下角圆弧
                figure.Segments.Add(new LineSegment(new Point(0, 0), true));                    // 左边
                geometry.Figures.Add(figure);
                btn.Clip = geometry;
            }
            else if (btn.Name.Contains("RightBottom")) // 右下角圆角裁剪
            {
                var geometry = new PathGeometry();
                var figure = new PathFigure { StartPoint = new Point(0, 0), IsClosed = true };
                figure.Segments.Add(new LineSegment(new Point(width, 0), true));                // 上边
                figure.Segments.Add(new LineSegment(new Point(width, height - radius), true));  // 右边（右上到右下圆角起点）
                figure.Segments.Add(new ArcSegment(new Point(width - radius, height), new Size(radius, radius), 0, false, SweepDirection.Clockwise, true)); // 右下角圆弧
                figure.Segments.Add(new LineSegment(new Point(0, height), true));               // 下边
                figure.Segments.Add(new LineSegment(new Point(0, 0), true));                    // 左边
                geometry.Figures.Add(figure);
                btn.Clip = geometry;
            }
        }
    }
}