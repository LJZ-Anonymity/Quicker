using System.Globalization;
using System.Windows.Data;

namespace Quicker.Converters
{
    /// <summary>
    /// BorderHeightConverter：用于计算预览区高度，根据按钮高度、间隙、行数计算。
    /// </summary>
    public class BorderHeightConverter : IMultiValueConverter
    {
        /// <summary>
        /// 预览区高度转换器：根据按钮高度、间隙、行数计算预览区高度
        /// </summary>
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            // values[0]: 按钮高度, values[1]: 按钮间隙
            if (values.Length >= 2 && double.TryParse(values[0]?.ToString(), out double btnHeight) && double.TryParse(values[1]?.ToString(), out double gap))
            {
                return 31 + 25.5 + btnHeight * 7 + gap * 5; // 预览区高度
            }
            return 0.0; // 错误值，返回 double 类型
        }
        /// <summary>
        /// 不支持反向转换
        /// </summary>
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new System.NotImplementedException();
    }
}