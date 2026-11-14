namespace Quicker.Converters
{
    /// <summary>
    /// ThicknessConverter：用于将double类型的值转换为Thickness类型，常用于XAML绑定时对Margin、Padding等Thickness类型值进行设置。
    /// </summary>
    public class ThicknessConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is double d)
            {
                return new System.Windows.Thickness(d);
            }
            return new System.Windows.Thickness(0);
        }

        public object ConvertBack(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is System.Windows.Thickness thickness)
            {
                return thickness.Left;
            }
            return 0.0;
        }
    }
}