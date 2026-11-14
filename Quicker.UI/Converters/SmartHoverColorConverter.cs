using System.Globalization;
using System.Windows.Media;
using System.Windows.Data;

namespace Quicker.UI.Converters
{
    /// <summary>
    /// 智能变色转换器：深色变浅，浅色变深。支持SolidColorBrush和颜色字符串。
    /// </summary>
    public class SmartHoverColorConverter : IValueConverter
    {
        public double BrightnessThreshold { get; set; } = 100; // 推荐用186
        public double LighterFactor { get; set; } = 1.2; // 变浅系数
        public double DarkerFactor { get; set; } = 0.8; // 变深系数

        /// <summary>
        /// 转换
        /// </summary>
        /// <param name="value"> 颜色值 </param>
        /// <param name="targetType"> 目标类型 </param>
        /// <param name="parameter"> 参数 </param>
        /// <param name="culture"> 区域性 </param>
        /// <returns> 转换后的颜色 </returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            Color color;
            if (value is SolidColorBrush brush) // 颜色画刷
            {
                color = brush.Color;
            }
            else if (value is string colorStr) // 颜色字符串
            {
                try { color = (Color)ColorConverter.ConvertFromString(colorStr); }
                catch { return new SolidColorBrush(Colors.Transparent); }
            }
            else
            {
                return new SolidColorBrush(Colors.Transparent);
            }

            // HSL明度判断
            double luminance = GetLuminance(color);
            if (luminance < 0.5) // 深色，变浅
            {
                byte r = (byte)Math.Min(color.R * LighterFactor, 255);
                byte g = (byte)Math.Min(color.G * LighterFactor, 255);
                byte b = (byte)Math.Min(color.B * LighterFactor, 255);
                return new SolidColorBrush(Color.FromArgb(color.A, r, g, b));
            }
            else // 浅色，变深
            {
                byte r = (byte)(color.R * DarkerFactor);
                byte g = (byte)(color.G * DarkerFactor);
                byte b = (byte)(color.B * DarkerFactor);
                return new SolidColorBrush(Color.FromArgb(color.A, r, g, b));
            }
        }

        /// <summary>
        /// 转换回原值
        /// </summary>
        /// <param name="value"> 转换后的颜色 </param>
        /// <param name="targetType"> 目标类型 </param>
        /// <param name="parameter"> 参数 </param>
        /// <param name="culture"> 区域性 </param>
        /// <returns> 转换回原值 </returns>
        /// <exception cref="NotImplementedException"></exception>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();

        /// <summary>
        /// 获取颜色的亮度值
        /// </summary>
        /// <param name="color"> 颜色 </param>
        /// <returns> 亮度值 </returns>
        private static double GetLuminance(Color color)
        {
            double r = color.R / 255.0;
            double g = color.G / 255.0;
            double b = color.B / 255.0;
            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            return (max + min) / 2.0;
        }
    }
}