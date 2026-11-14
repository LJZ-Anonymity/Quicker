using System.Windows.Documents;
using System.Windows.Media;
using System.Globalization;
using System.Windows.Data;
using System.Windows;

namespace Quicker.UI.Converters
{
    /// <summary>
    /// 用于将文本和关键字转换为高亮显示（加粗匹配关键字）的 Inline 集合的多值转换器。
    /// 适用于WPF绑定，便于在TextBlock中动态高亮显示搜索结果。
    /// </summary>
    public class HighlightTextConverter : IMultiValueConverter
    {
        /// <summary>
        /// 将原始文本和关键字转换为高亮显示的 Inline 集合。
        /// </summary>
        /// <param name="values">values[0]为原始文本，values[1]为关键字</param>
        /// <param name="targetType">目标类型</param>
        /// <param name="parameter">参数</param>
        /// <param name="culture">区域信息</param>
        /// <returns>包含高亮效果的 Inline 集合</returns>
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            string text = values[0] as string ?? "";
            string keyword = values[1] as string ?? "";
            var inlines = new List<Inline>();

            // 如果没有关键字，直接返回原文本
            if (string.IsNullOrEmpty(keyword))
            {
                inlines.Add(new Run(text));
                return inlines;
            }

            int index = 0;
            int kwLen = keyword.Length;
            string lowerText = text.ToLower();
            string lowerKeyword = keyword.ToLower();
            // 循环查找所有匹配关键字的位置，并加粗显示
            while (index < text.Length)
            {
                int found = lowerText.IndexOf(lowerKeyword, index, StringComparison.Ordinal);
                if (found < 0)
                {
                    inlines.Add(new Run(text.Substring(index)));
                    break;
                }
                if (found > index)
                    inlines.Add(new Run(text.Substring(index, found - index)));
                // 匹配部分加粗
                inlines.Add(new Run(text.Substring(found, kwLen)) { FontWeight = FontWeights.Bold, Foreground = Brushes.Black });
                index = found + kwLen;
            }
            return inlines;
        }

        /// <summary>
        /// 不支持反向转换。
        /// </summary>
        /// <param name="value">值</param>
        /// <param name="targetTypes">目标类型</param>
        /// <param name="parameter">参数</param>
        /// <param name="culture">区域信息</param>
        /// <returns>抛出异常</returns>
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}