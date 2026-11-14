namespace Quicker.Converters
{
    /// <summary>
    /// 毛玻璃效果转换器：根据模糊下拉框选项返回BlurEffect或null
    /// </summary>
    public class BlurEffectConverter : System.Windows.Data.IMultiValueConverter
    {
        /// <summary>
        /// 将模糊下拉框选项转换为BlurEffect对象
        /// </summary>
        public object Convert(object[] values, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            int selectedIndex = (int)values[0];
            if (selectedIndex == 1) // 毛玻璃
            {
                return new System.Windows.Media.Effects.BlurEffect { Radius = 15 };
            }
            return null;
        }
        /// <summary>
        /// 不支持反向转换
        /// </summary>
        public object[] ConvertBack(object value, System.Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture) => throw new System.NotImplementedException();
    }
}