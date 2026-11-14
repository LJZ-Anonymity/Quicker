using System.Globalization;
using System.Windows.Data;
using System.Windows;

namespace Quicker.Converters
{
    /// <summary>
    /// 将ComboBox的SelectedIndex（int）转换为CornerRadius的转换器。
    /// 0: 默认（5px），1: 无（0px），2: 圆角（5px），3: 小圆角（3px）。
    /// </summary>
    public class IntToCornerRadiusConverter : IValueConverter
    {
        /// <summary>
        /// 将SelectedIndex（int）转换为CornerRadius。
        /// </summary>
        /// <param name="value">ComboBox的SelectedIndex，类型为int。</param>
        /// <param name="targetType">目标类型。</param>
        /// <param name="parameter">可选参数。</param>
        /// <param name="culture">区域信息。</param>
        /// <returns>对应的CornerRadius对象。</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int radius = 5; // 默认圆角
            if (value is int index)
            {
                switch (index)
                {
                    case 0: // 默认
                    case 2: // 圆角
                        radius = 5;
                        break;
                    case 1: // 无
                        radius = 0;
                        break;
                    case 3: // 小圆角
                        radius = 3;
                        break;
                    default:
                        radius = 5;
                        break;
                }
            }
            return new CornerRadius(radius);
        }

        /// <summary>
        /// 将CornerRadius反向转换为ComboBox的SelectedIndex。
        /// </summary>
        /// <param name="value">CornerRadius对象。</param>
        /// <param name="targetType">目标类型。</param>
        /// <param name="parameter">可选参数。</param>
        /// <param name="culture">区域信息。</param>
        /// <returns>对应的SelectedIndex（int）。</returns>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is CornerRadius cr)
            {
                // 反向映射：只做简单处理
                if (cr.TopLeft == 0) return 1;
                if (cr.TopLeft == 3) return 3;
                return 0; // 其它都映射为默认
            }
            return 0;
        }
    }
}