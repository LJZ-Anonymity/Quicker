using System.Text.RegularExpressions;
using Quicker.Windows.ToolWindows;
using System.Windows.Threading;
using System.Windows.Controls;
using System.Windows.Shapes;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows;

namespace Quicker.UserControls.SettingWindow
{
    public partial class ColorPicker : UserControl
    {
        private ColorPickerMagnifier _magnifierWindow; // 放大镜窗口
        private Color _currentColor = Colors.White; // 当前颜色
        private DispatcherTimer _magnifierTimer; // 放大镜更新定时器
        private bool _updatingControls = false; //  确保颜色变化事件只被触发一次
        private bool _isColorPicking = false; // 是否正在取色
        private double _saturation = 1;  // 饱和度
        private Rectangle _colorRect; // 用于存储颜色画布中的矩形，方便直接访问
        private double _value = 1;  // 亮度
        private double _hue = 0; // 色相

        public event EventHandler<ColorChangedEventArgs> SelectedColorChanged;  // 颜色变化事件
        public static readonly DependencyProperty SelectedColorProperty =
            DependencyProperty.Register("SelectedColor", typeof(Color), typeof(ColorPicker),
                new FrameworkPropertyMetadata(Colors.White, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedColorChanged)); // 颜色变化事件

        public Color SelectedColor
        {
            get { return (Color)GetValue(SelectedColorProperty); } // 触发颜色变化事件
            set { SetValue(SelectedColorProperty, value); } // 触发颜色变化事件
        }

        // 颜色变化事件处理方法
        private static void OnSelectedColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var picker = (ColorPicker)d; // 转换为ColorPicker类型
            picker._currentColor = (Color)e.NewValue; // 获取新颜色
            picker.UpdateControlsFromColor(); // 更新控件状态
            picker.RaiseSelectedColorChanged((Color)e.OldValue, (Color)e.NewValue); // 触发颜色变化事件
        }

        /// <summary>
        /// 触发颜色变化事件
        /// </summary>
        /// <param name="oldColor"> 旧颜色 </param>
        /// <param name="newColor"> 新颜色 </param>
        private void RaiseSelectedColorChanged(Color oldColor, Color newColor)
        {
            SelectedColorChanged?.Invoke(this, new ColorChangedEventArgs(oldColor, newColor)); // 触发颜色变化事件
        }

        public ColorPicker()
        {
            InitializeComponent(); // 加载xaml文件
            Loaded += ColorPicker_Loaded; // 加载完成事件
            
            // 设置默认值，防止空引用异常
            if (HueSlider != null)
                HueSlider.Value = 0; // 色相
                
            if (RedSlider != null)
                RedSlider.Value = 255; // 红色
                
            if (GreenSlider != null)
                GreenSlider.Value = 255; // 绿色
                
            if (BlueSlider != null)
                BlueSlider.Value = 255; // 蓝色
                
            if (AlphaSlider != null)
                AlphaSlider.Value = 255; // 透明度
        }

        //  加载完成事件处理方法
        private void ColorPicker_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // 确保所有控件都已初始化
                if (ColorCanvas != null && HueSlider != null && 
                    RedSlider != null && GreenSlider != null && 
                    BlueSlider != null && AlphaSlider != null)
                {
                    // 存储第一个矩形引用以便直接访问
                    if (ColorCanvas.Children.Count > 0 && ColorCanvas.Children[0] is Rectangle rect)
                    {
                        _colorRect = rect;
                    }
                    
                    HueSlider.Value = _hue; // 色相
                    
                    // 确保设置初始颜色
                    UpdateHueColorRect(_hue);
                    
                    UpdateControlsFromColor(); // 更新控件状态
                    UpdateColorThumbPosition(); // 更新色彩画布
                }
            }
            catch
            {
                // 处理异常，防止崩溃
            }
        }
        
        /// <summary>
        /// 更新色相矩形的颜色
        /// </summary>
        /// <param name="hue">色相值</param>
        private void UpdateHueColorRect(double hue)
        {
            if (_colorRect != null)
            {
                Color hueColor = GetColorFromHue(hue);
                
                // 创建新的渐变画刷
                LinearGradientBrush gradientBrush = new LinearGradientBrush();
                gradientBrush.StartPoint = new Point(0, 0);
                gradientBrush.EndPoint = new Point(1, 0);
                gradientBrush.GradientStops.Add(new GradientStop(Colors.White, 0));
                gradientBrush.GradientStops.Add(new GradientStop(hueColor, 1));
                
                // 应用到矩形
                _colorRect.Fill = gradientBrush;
            }
        }
        
        /// <summary>
        /// 强制刷新颜色选择器的所有控件状态
        /// </summary>
        /// <param name="color">要设置的颜色</param>
        public void ResetColorControls(Color color)
        {
            _currentColor = color;
            
            // 先将控件更新标志设为false，确保后续更新能正常进行
            _updatingControls = false;
            
            // 更新RGB和HSV值
            RgbToHsv(_currentColor.R, _currentColor.G, _currentColor.B, out _hue, out _saturation, out _value);
            
            if (HueSlider != null)
            {
                // 开始更新控件，避免循环触发事件
                _updatingControls = true;
                
                try
                {
                    // 1. 先更新色相滑块的值
                    HueSlider.Value = _hue;
                    
                    // 2. 直接更新颜色选择区域的背景色
                    UpdateHueColorRect(_hue);
                    
                    // 3. 更新RGB滑块
                    if (RedSlider != null) RedSlider.Value = _currentColor.R;
                    if (GreenSlider != null) GreenSlider.Value = _currentColor.G;
                    if (BlueSlider != null) BlueSlider.Value = _currentColor.B;
                    if (AlphaSlider != null) AlphaSlider.Value = _currentColor.A;
                    
                    // 4. 更新色彩选择点的位置
                    UpdateColorThumbPosition();
                    
                    // 5. 更新十六进制值
                    UpdateHexValue();
                }
                finally
                {
                    // 恢复控件更新标志
                    _updatingControls = false;
                }
            }
        }

        // 更新控件状态
        private void UpdateControlsFromColor()
        {
            if (_updatingControls || RedSlider == null || GreenSlider == null || BlueSlider == null ||
                AlphaSlider == null || HueSlider == null || ColorThumb == null || HexValue == null) 
            {
                return; // 如果控件尚未初始化或者正在更新，则返回
            }
            _updatingControls = true; // 设置标志，表示正在更新控件状态

            try
            {
                // 更新 RGB sliders
                RedSlider.Value = _currentColor.R; // 红色
                GreenSlider.Value = _currentColor.G; // 绿色
                BlueSlider.Value = _currentColor.B; // 蓝色
                AlphaSlider.Value = _currentColor.A; // 透明度

                RgbToHsv(_currentColor.R, _currentColor.G, _currentColor.B, out _hue, out _saturation, out _value); // 将RGB转换为HSV
                HueSlider.Value = _hue; // 更新色相
                
                // 更新色相对应的颜色选择区域背景色
                UpdateHueColorRect(_hue);
                
                if (ColorCanvas != null && ColorCanvas.ActualWidth > 0 && ColorCanvas.ActualHeight > 0)
                {
                    UpdateColorThumbPosition(); // 如果颜色画布已正确初始化，则更新颜色块位置
                }
                UpdateHexValue(); // 更新十六进制值
            }
            catch
            {

            }
            finally
            {
                _updatingControls = false;  // 重置标志
            }
        }

        // 更新颜色块位置
        private void UpdateColorThumbPosition()
        {
            if (ColorCanvas == null || ColorThumb == null || ColorCanvas.ActualWidth <= 0 || ColorCanvas.ActualHeight <= 0)
            {
                return; // 确保ColorCanvas和ColorThumb不为null且已正确初始化
            }

            try
            {
                double x = _saturation * ColorCanvas.ActualWidth; // 计算颜色块在色彩画布中的位置
                double y = (1 - _value) * ColorCanvas.ActualHeight; // 计算颜色块在色彩画布中的位置
                
                ColorThumb.SetValue(Canvas.LeftProperty, Math.Max(0, Math.Min(ColorCanvas.ActualWidth - ColorThumb.Width / 2, x - ColorThumb.Width / 2))); // 确保颜色块在画布内
                ColorThumb.SetValue(Canvas.TopProperty, Math.Max(0, Math.Min(ColorCanvas.ActualHeight - ColorThumb.Height / 2, y - ColorThumb.Height / 2)));  // 确保颜色块在画布内
            }
            catch
            {

            }
        }

        // 更新颜色块位置
        private void UpdateColorFromHsv()
        {
            if (_updatingControls || RedSlider == null || GreenSlider == null || BlueSlider == null) return; // 确保控件已初始化
            _updatingControls = true; // 设置标志，表示正在更新控件状态
            try
            {
                HsvToRgb(_hue, _saturation, _value, out byte r, out byte g, out byte b); // 将HSV转换为RGB
                _currentColor = Color.FromArgb(_currentColor.A, r, g, b); // 更新颜色
                SelectedColor = _currentColor; // 触发SelectedColorChanged事件

                // 更新 RGB sliders
                RedSlider.Value = r;  // 红色
                GreenSlider.Value = g; // 绿色
                BlueSlider.Value = b; // 蓝色

                UpdateHexValue(); // 更新十六进制值
            }
            catch
            {

            }
            finally
            {
                _updatingControls = false;  // 重置标志
            }
        }

        // 更新颜色块位置
        private void UpdateHexValue()
        {
            if (HexValue == null) return; // 确保控件已初始化
            try
            {
                HexValue.Text = $"#{_currentColor.A:X2}{_currentColor.R:X2}{_currentColor.G:X2}{_currentColor.B:X2}"; // 更新十六进制值
            }
            catch
            {

            }
        }

        // HSV颜色转换
        private void Slider_ValueChanged(object sender, System.Windows.RoutedPropertyChangedEventArgs<double> e)
        {
            if (_updatingControls) return; // 确保控件已初始化
            
            var slider = sender as Slider; // 获取滑块
            if (slider == null) return; // 确保滑块不为null

            int value = (int)slider.Value;  // 获取滑块值
            string sliderName = slider.Name; // 获取滑块名称
            switch (sliderName)
            {
                case "RedSlider":
                    RedValue.Text = value.ToString(); // 红色
                    _currentColor = Color.FromArgb(_currentColor.A, (byte)value, _currentColor.G, _currentColor.B); // 更新颜色
                    break;
                case "GreenSlider":
                    GreenValue.Text = value.ToString(); // 绿色
                    _currentColor = Color.FromArgb(_currentColor.A, _currentColor.R, (byte)value, _currentColor.B); // 更新颜色
                    break;
                case "BlueSlider":
                    BlueValue.Text = value.ToString(); // 蓝色
                    _currentColor = Color.FromArgb(_currentColor.A, _currentColor.R, _currentColor.G, (byte)value); // 更新颜色
                    break;
                case "AlphaSlider":
                    AlphaValue.Text = value.ToString(); // 透明度
                    _currentColor = Color.FromArgb((byte)value, _currentColor.R, _currentColor.G, _currentColor.B); // 更新颜色
                    break;
            }
            SelectedColor = _currentColor; // 更新SelectedColor
            RgbToHsv(_currentColor.R, _currentColor.G, _currentColor.B, out _hue, out _saturation, out _value); // 更新HSV值
            _updatingControls = true; // 设置标志，表示正在更新控件状态
            UpdateColorThumbPosition(); // 更新颜色块位置
            _updatingControls = false; // 重置标志
            UpdateHexValue(); // 更新十六进制值
        }

        // 更新十六进制值
        private void Value_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_updatingControls) return; // 确保控件已初始化
            
            TextBox textBox = (TextBox)sender; // 获取文本框
            if (string.IsNullOrEmpty(textBox.Text)) return; // 确保文本框不为空
            
            int value = 0;  // 获取文本框值
            if (!int.TryParse(textBox.Text, out value))
            {
                value = 0; // 如果文本框值不是数字，则设置为0
            }

            value = Math.Max(0, Math.Min(255, value)); // 限制值在0到255之间
            _updatingControls = true;  // 设置标志，表示正在更新控件状态
            switch (textBox.Name)
            {
                case "RedValue":
                    RedSlider.Value = value; // 更新滑块值
                    _currentColor = Color.FromArgb(_currentColor.A, (byte)value, _currentColor.G, _currentColor.B); // 更新颜色
                    break;
                case "GreenValue":
                    GreenSlider.Value = value; // 更新滑块值
                    _currentColor = Color.FromArgb(_currentColor.A, _currentColor.R, (byte)value, _currentColor.B); // 更新颜色
                    break;
                case "BlueValue":
                    BlueSlider.Value = value; // 更新滑块值
                    _currentColor = Color.FromArgb(_currentColor.A, _currentColor.R, _currentColor.G, (byte)value); // 更新颜色
                    break;
                case "AlphaValue":
                    AlphaSlider.Value = value; // 更新滑块值
                    _currentColor = Color.FromArgb((byte)value, _currentColor.R, _currentColor.G, _currentColor.B); // 更新颜色
                    break;
            }
            _updatingControls = false; // 重置标志
            SelectedColor = _currentColor;  // 触发SelectedColorChanged事件
            RgbToHsv(_currentColor.R, _currentColor.G, _currentColor.B, out _hue, out _saturation, out _value); // 更新HSV值
            UpdateColorThumbPosition(); // 更新颜色块位置
            UpdateHexValue(); // 更新十六进制值
        }

        // 更新色彩画布
        private void HueSlider_ValueChanged(object sender, System.Windows.RoutedPropertyChangedEventArgs<double> e)
        {
            if (_updatingControls) return; // 确保控件已初始化
            _hue = ((Slider)sender).Value; // 获取色相值

            // 更新颜色选择区域背景色
            UpdateHueColorRect(_hue);
            
            // 更新颜色
            UpdateColorFromHsv(); 
        }

        /// <summary>
        /// 从色相获取颜色
        /// </summary>
        /// <param name="hue"> 色相值 </param>
        /// <returns> 颜色 </returns>
        private Color GetColorFromHue(double hue)
        {
            HsvToRgb(hue, 1, 1, out byte r, out byte g, out byte b); // 将HSV转换为RGB
            return Color.FromRgb(r, g, b); // 返回颜色
        }

        //  鼠标按下
        private void ColorCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.RightButton == MouseButtonState.Pressed) // 右键按下，启动屏幕取色
            {
                StartScreenColorPicking();
            }
            else if (e.LeftButton == MouseButtonState.Pressed) // 左键按下
            {
                if (_isColorPicking) // 如果正在取色，则确认选择
                {
                    ConfirmColorPick();
                }
                else
                {
                    // 捕获鼠标以获得更流畅的拖动体验
                    ColorCanvas.CaptureMouse();
                    var position = e.GetPosition(ColorCanvas); // 获取鼠标位置
                    UpdateColorFromCanvasPosition(position); // 更新颜色
                }
            }
        }

        //  鼠标移动
        private void ColorCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && ColorCanvas.IsMouseCaptured) // 左键按下且已捕获鼠标
            {
                var position = e.GetPosition(ColorCanvas); // 获取鼠标位置
                UpdateColorFromCanvasPosition(position); // 更新颜色
            }
        }

        //  鼠标抬起
        private void ColorCanvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (ColorCanvas.IsMouseCaptured)
            {
                // 释放鼠标捕获
                ColorCanvas.ReleaseMouseCapture();
                var position = e.GetPosition(ColorCanvas); // 获取鼠标位置
                UpdateColorFromCanvasPosition(position);  // 更新颜色
            }
        }

        /// <summary>
        ///  更新颜色
        /// </summary>
        /// <param name="position"> 鼠标位置 </param>
        private void UpdateColorFromCanvasPosition(Point position)
        {
            double canvasWidth = ColorCanvas.ActualWidth; // 画布宽度
            double canvasHeight = ColorCanvas.ActualHeight; // 画布高度

            // 限制鼠标位置
            double x = Math.Max(0, Math.Min(canvasWidth, position.X)); // 限制x在0到画布宽度之间
            double y = Math.Max(0, Math.Min(canvasHeight, position.Y)); // 限制y在0到画布高度之间
            
            // 更新颜色块位置
            ColorThumb.SetValue(Canvas.LeftProperty, x - ColorThumb.Width / 2); // 确保颜色块在画布内
            ColorThumb.SetValue(Canvas.TopProperty, y - ColorThumb.Height / 2); // 确保颜色块在画布内
            
            // 更新HSV值
            _saturation = x / canvasWidth; // 饱和度
            _value = 1 - (y / canvasHeight); // 明度
            
            UpdateColorFromHsv(); // 更新颜色
        }

        /// <summary>
        /// RGB转HSV
        /// </summary>
        /// <param name="r"> 红色 </param>
        /// <param name="g"> 绿色 </param>
        /// <param name="b"> 蓝色 </param>
        /// <param name="h"> 色相 </param>
        /// <param name="s"> 饱和度 </param>
        /// <param name="v"> 亮度 </param>
        private void RgbToHsv(byte r, byte g, byte b, out double h, out double s, out double v)
        {
            double red = r / 255.0; // 转换为0-1范围
            double green = g / 255.0; // 转换为0-1范围
            double blue = b / 255.0;  // 转换为0-1范围
            
            double max = Math.Max(red, Math.Max(green, blue)); // 获取最大值
            double min = Math.Min(red, Math.Min(green, blue)); // 获取最小值
            double delta = max - min; // 最大值与最小值差

            v = max; // 亮度
            s = max == 0 ? 0 : delta / max; // 饱和度
            h = 0;  // 色调
            if (delta == 0)
            {
                h = 0; // 如果差为0，则无色
            }
            else
            {
                if (max == red)  // 如果最大值为红色
                {
                    h = ((green - blue) / delta) % 6; // 红色最大
                }
                else if (max == green) // 如果最大值为绿色
                {
                    h = (blue - red) / delta + 2; // 绿色最大
                }
                else
                {
                    h = (red - green) / delta + 4; // 红色最大
                }
                
                h *= 60; // 转换为度
                if (h < 0) h += 360; // 限制在0到360之间
            }
        }
        
        /// <summary>
        ///  获取RGB颜色
        /// </summary>
        /// <param name="h"> 色相 </param>
        /// <param name="s"> 饱和度 </param>
        /// <param name="v"> 亮度 </param>
        /// <param name="r"> 红色 </param>
        /// <param name="g"> 绿色 </param>
        /// <param name="b"> 蓝色 </param>
        private void HsvToRgb(double h, double s, double v, out byte r, out byte g, out byte b)
        {
            double c = v * s; // 计算临界值
            double x = c * (1 - Math.Abs((h / 60) % 2 - 1)); // 计算 x 值
            double m = v - c; // 颜色值
            double red = 0, green = 0, blue = 0;  // 颜色值
            
            if (h >= 0 && h < 60) // 如果 色相在 0 到 60 之间
            {
                red = c; green = x; blue = 0; // 红色最大
            }
            else if (h >= 60 && h < 120)  // 如果 色相在 60 到 120 之间
            {
                red = x; green = c; blue = 0;  // 绿色最大
            }
            else if (h >= 120 && h < 180)  // 如果 色相在 120 到 180 之间
            {
                red = 0; green = c; blue = x;  // 蓝色最大
            }
            else if (h >= 180 && h < 240) // 如果 色相在 180 到 240 之间
            {
                red = 0; green = x; blue = c; // 红色最大
            }
            else if (h >= 240 && h < 300) // 如果 色相在 240 到 300 之间
            {
                red = x; green = 0; blue = c;  // 黄色最大
            }
            else
            {
                red = c; green = 0; blue = x; // 青色最大
            }
            
            r = (byte)Math.Round((red + m) * 255); // 计算红色通道的值
            g = (byte)Math.Round((green + m) * 255); // 计算绿色通道的值
            b = (byte)Math.Round((blue + m) * 255); // 计算蓝色通道的值
        }

        // 十六进制颜色输入框失去焦点事件处理方法
        private void HexValue_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_updatingControls) return; // 防止重复触发
            string hexText = HexValue.Text.Trim(); // 获取并去除两端空格

            // 自动补#，如果没有
            if (!hexText.StartsWith("#"))
            {
                hexText = "#" + hexText; // 如果没有#，则自动补全
            }

            if (IsValidHexColor(hexText)) // 如果是有效的十六进制颜色格式
            {
                try
                {
                    Color newColor; // 新颜色
                    if (hexText.Length == 9) // 如果是带透明度的十六进制颜色
                    {
                        newColor = (Color)ColorConverter.ConvertFromString(hexText); // 转换为颜色
                    }
                    else if (hexText.Length == 7) // 如果是普通十六进制颜色
                    {
                        var baseColor = (Color)ColorConverter.ConvertFromString(hexText); // 获取基础颜色
                        newColor = Color.FromArgb(_currentColor.A, baseColor.R, baseColor.G, baseColor.B); // 设置透明度
                    }
                    else if (hexText.Length == 4) // 如果是简写十六进制颜色
                    {
                        string fullHex = "#" + hexText[1] + hexText[1] + hexText[2] + hexText[2] + hexText[3] + hexText[3]; // 获取完整十六进制颜色
                        var baseColor = (Color)ColorConverter.ConvertFromString(fullHex); // 转换为Color对象
                        newColor = Color.FromArgb(_currentColor.A, baseColor.R, baseColor.G, baseColor.B); // 设置透明度
                    }
                    else
                    {
                        return; // 无效的颜色格式
                    }

                    _currentColor = newColor; // 新颜色
                    SelectedColor = _currentColor; // 新颜色

                    _updatingControls = true; // 设置标志，表示正在更新控件状态
                    UpdateControlsFromColor(); // 更新控件状态
                    _updatingControls = false; // 重置标志
                }
                catch
                {
                    // 处理异常，防止崩溃
                }
            }
            // 自动补全#后，回写到输入框
            _updatingControls = true; // 设置标志，表示正在更新控件状态
            HexValue.Text = hexText; // 更新文本框
            _updatingControls = false; // 重置标志
        }
        
        /// <summary>
        ///  检查给定的字符串是否为有效的十六进制颜色格式
        /// </summary>
        /// <param name="hexColor"> 十六进制颜色字符串 </param>
        /// <returns> 是否为有效的十六进制颜色格式 </returns>
        private bool IsValidHexColor(string hexColor)
        {
            return Regex.IsMatch(hexColor, @"^#([0-9A-Fa-f]{3}|[0-9A-Fa-f]{6}|[0-9A-Fa-f]{8})$"); // 匹配 #RGB、#RRGGBB和#AARRGGBB格式
        }

        /// <summary>
        /// 启动屏幕取色模式
        /// </summary>
        private void StartScreenColorPicking()
        {
            if (_isColorPicking) return;

            _isColorPicking = true;

            // 创建放大镜窗口
            _magnifierWindow = new ColorPickerMagnifier();
            _magnifierWindow.Show();

            // 创建定时器，定期更新放大镜和检查鼠标点击
            _magnifierTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16) // 约60fps
            };
            _magnifierTimer.Tick += MagnifierTimer_Tick;
            _magnifierTimer.Start();

            // 捕获鼠标以接收全局事件
            Mouse.Capture(this, CaptureMode.SubTree);
            PreviewMouseLeftButtonDown += ColorPicker_PreviewMouseLeftButtonDown;
            PreviewMouseRightButtonDown += ColorPicker_PreviewMouseRightButtonDown;
        }

        /// <summary>
        /// 放大镜定时器事件处理
        /// </summary>
        private void MagnifierTimer_Tick(object sender, EventArgs e)
        {
            if (_magnifierWindow != null && _isColorPicking)
            {
                _magnifierWindow.UpdateColor();

                // 检查鼠标左键是否按下（全局检查）
                if (System.Windows.Forms.Control.MouseButtons == System.Windows.Forms.MouseButtons.Left)
                {
                    ConfirmColorPick();
                }
                // 检查鼠标右键是否按下（取消取色）
                else if (System.Windows.Forms.Control.MouseButtons == System.Windows.Forms.MouseButtons.Right)
                {
                    StopScreenColorPicking();
                }
            }
        }

        /// <summary>
        /// 确认颜色选择
        /// </summary>
        private void ConfirmColorPick()
        {
            if (!_isColorPicking || _magnifierWindow == null) return;

            // 获取选中的颜色
            var pickedColor = _magnifierWindow.SelectedColor;
            
            // 设置颜色
            _currentColor = pickedColor;
            SelectedColor = pickedColor;

            // 停止取色
            StopScreenColorPicking();
        }

        /// <summary>
        /// 停止屏幕取色模式
        /// </summary>
        private void StopScreenColorPicking()
        {
            if (!_isColorPicking) return;

            _isColorPicking = false;

            // 停止定时器
            _magnifierTimer?.Stop();
            _magnifierTimer = null;

            // 关闭放大镜窗口
            _magnifierWindow?.Close();
            _magnifierWindow = null;

            // 释放鼠标捕获
            Mouse.Capture(null);
            //PreviewMouseMove -= ColorPicker_PreviewMouseMove;
            PreviewMouseLeftButtonDown -= ColorPicker_PreviewMouseLeftButtonDown;
            PreviewMouseRightButtonDown -= ColorPicker_PreviewMouseRightButtonDown;
        }


        /// <summary>
        /// 预览鼠标左键按下事件（用于确认颜色选择）
        /// </summary>
        private void ColorPicker_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_isColorPicking)
            {
                ConfirmColorPick();
                e.Handled = true;
            }
        }

        /// <summary>
        /// 预览鼠标右键按下事件（用于取消取色）
        /// </summary>
        private void ColorPicker_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_isColorPicking)
            {
                StopScreenColorPicking();
                e.Handled = true;
            }
        }

        /// <summary>
        /// 颜色预览区域右键点击事件
        /// </summary>
        private void ColorPreview_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            StartScreenColorPicking();
            e.Handled = true;
        }
    }

    // 颜色变化事件参数类
    public class ColorChangedEventArgs : EventArgs
    {
        public Color OldColor { get; } // 旧颜色
        public Color NewColor { get; } // 新颜色

        /// <summary>
        /// 创建颜色变化事件参数
        /// </summary>
        /// <param name="oldColor"> 旧颜色 </param>
        /// <param name="newColor"> 新颜色 </param>
        public ColorChangedEventArgs(Color oldColor, Color newColor)
        {
            OldColor = oldColor; // 旧颜色
            NewColor = newColor; // 新颜色
        }
    }
}