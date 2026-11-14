using System.Globalization;
using System.Windows.Data;

namespace Quicker.Converters
{
    /// <summary>
    /// GridHeightConverter：用于计算Grid的宽度，根据按钮大小、间隙、列数计算。
    /// </summary>
    public class GridHeightConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            // values[0]: 按钮大小, values[1]: 按钮间隙, parameter: 行数
            if (values.Length >= 2 && double.TryParse(values[0]?.ToString(), out double btnSize) && double.TryParse(values[1]?.ToString(), out double gap))
            {
                int rows = 3; // 默认3行
                if (parameter != null && int.TryParse(parameter.ToString(), out int pRows))
                    rows = pRows;
                return btnSize * rows + gap * (rows - 1);
            }
            return 0.0;
        }
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new System.NotImplementedException();
    }
}