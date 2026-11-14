using System.Globalization;
using System.Windows.Data;
using System.Windows;

namespace Quicker.UI.Converters
{
    public class SearchHintVisibilityConverter : IValueConverter
    {
        /// <summary>
        /// 将搜索提示的可见性转换为bool值
        /// </summary>
        /// <param name="value"> 搜索提示的可见性 </param>
        /// <param name="targetType"> 目标类型 </param>
        /// <param name="parameter"> 转换参数 </param>
        /// <param name="culture"> 区域性 </param>
        /// <returns> bool值 </returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string? text = value as string; // 转换前的类型为string
            return string.IsNullOrWhiteSpace(text) ? Visibility.Visible : Visibility.Hidden; // 若搜索提示为空白，则可见，否则不可见
        }

        /// <summary>
        /// 未实现
        /// </summary>
        /// <param name="value"> 搜索提示的可见性 </param>
        /// <param name="targetType"> 目标类型 </param>
        /// <param name="parameter"> 转换参数 </param>
        /// <param name="culture"> 区域性 </param>
        /// <returns> 未实现 </returns>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}