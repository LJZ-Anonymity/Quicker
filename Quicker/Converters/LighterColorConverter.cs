using System.Globalization;
using System.Windows.Media;
using System.Windows.Data;

namespace Quicker.Converters
{
    /// <summary>
    /// 颜色变浅转换器。将颜色字符串转换为变浅后的 SolidColorBrush。
    /// 用于将颜色变亮，factor 越大越浅。
    /// </summary>
    public class LighterColorConverter : IValueConverter
    {
        /// <summary>
        /// 变浅系数，factor > 1，越大越浅。默认 1.2。
        /// </summary>
        public double Factor { get; set; } = 1.2;

        /// <summary>
        /// 将颜色字符串转换为变浅后的 SolidColorBrush。
        /// </summary>
        /// <param name="value">颜色字符串（如 #FFAABBCC）</param>
        /// <param name="targetType">目标类型</param>
        /// <param name="parameter">转换参数</param>
        /// <param name="culture">区域性</param>
        /// <returns>变浅后的 SolidColorBrush</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string colorStr)
            {
                try
                {
                    var color = (Color)ColorConverter.ConvertFromString(colorStr);
                    byte r = (byte)Math.Min(color.R * Factor, 255);
                    byte g = (byte)Math.Min(color.G * Factor, 255);
                    byte b = (byte)Math.Min(color.B * Factor, 255);
                    return new SolidColorBrush(Color.FromArgb(color.A, r, g, b)); // 返回变浅后的颜色
                }
                catch
                {
                    return new SolidColorBrush(Colors.Transparent); // 解析失败返回透明色
                }
            }
            return new SolidColorBrush(Colors.Transparent);
        }

        /// <summary>
        /// 不支持反向转换。
        /// </summary>
        /// <param name="value">颜色</param>
        /// <param name="targetType">目标类型</param>
        /// <param name="parameter">转换参数</param>
        /// <param name="culture">区域性</param>
        /// <returns>抛出 NotImplementedException</returns>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}