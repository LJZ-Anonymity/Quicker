namespace Quicker.Converters
{
    /// <summary>
    /// 路径转图片源转换器：将路径转换为BitmapImage
    /// </summary>
    public class PathToImageSourceConverter : System.Windows.Data.IValueConverter
    {
        /// <summary>
        /// 将路径转换为BitmapImage
        /// </summary>
        public object Convert(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            string path = value as string; // 路径
            if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path)) // 路径不存在
                return null;
            try
            {
                return new System.Windows.Media.Imaging.BitmapImage(new System.Uri(path, System.UriKind.Absolute));
            }
            catch
            {
                return null;
            }
        }
        /// <summary>
        /// 不支持反向转换
        /// </summary>
        public object ConvertBack(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture) => throw new System.NotImplementedException();
    }
}