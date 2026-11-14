namespace Quicker.Converters
{
    /// <summary>
    /// GridWidthConverter：用于计算Grid的宽度，根据按钮大小、间隙、列数计算。
    /// </summary>
    public class GridWidthConverter : System.Windows.Data.IMultiValueConverter
    {
        public object Convert(object[] values, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            // values[0]: 按钮大小, values[1]: 按钮间隙, parameter: 列数
            if (values.Length >= 2 && double.TryParse(values[0]?.ToString(), out double btnSize) && double.TryParse(values[1]?.ToString(), out double gap))
            {
                int cols = 4; // 默认4列
                if (parameter != null && int.TryParse(parameter.ToString(), out int pCols))
                    cols = pCols;
                return btnSize * cols + gap * (cols - 1);
            }
            return 0.0;
        }
        public object[] ConvertBack(object value, System.Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture) => throw new System.NotImplementedException();
    }
}