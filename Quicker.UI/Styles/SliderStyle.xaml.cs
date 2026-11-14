using System.Globalization;
using System.Windows.Data;

namespace Quicker.Resources.Styles
{
    /// <summary>
    /// 用于将Slider的Value、Minimum、Maximum和轨道宽度（ActualWidth）转换为进度条宽度的转换器。
    /// 主要用于横向Slider的进度条宽度动态绑定。
    /// </summary>
    public class SliderTrackWidthConverter : IMultiValueConverter
    {
        /// <summary>
        /// 将Slider的Value、Minimum、Maximum和轨道宽度转换为进度条宽度。
        /// </summary>
        /// <param name="values">依次为Value、Minimum、Maximum、轨道宽度（ActualWidth）</param>
        /// <param name="targetType">目标类型</param>
        /// <param name="parameter">参数</param>
        /// <param name="culture">区域信息</param>
        /// <returns>进度条宽度（double）</returns>
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 4) // 至少需要4个参数
                return 0.0;

            for (int i = 0; i < 4; i++) // 检查每个值是否为有效数字，防止未初始化时抛出异常
            {
                if (values[i] == null || values[i] == System.Windows.DependencyProperty.UnsetValue)
                    return 0.0; // 未初始化时返回0.0
            }

            // 依次获取Value、Minimum、Maximum、轨道宽度
            double value = System.Convert.ToDouble(values[0]); // Value
            double min = System.Convert.ToDouble(values[1]); // Minimum
            double max = System.Convert.ToDouble(values[2]); // Maximum
            double totalWidth = System.Convert.ToDouble(values[3]); // 轨道宽度（ActualWidth）

            // 防止除数为0
            if (max <= min) return 0.0;
            // 计算进度条宽度
            return (value - min) / (max - min) * totalWidth;
        }

        /// <summary>
        /// 不支持反向转换。
        /// </summary>
        /// <param name="value">值</param>
        /// <param name="targetTypes">目标类型</param>
        /// <param name="parameter">参数</param>
        /// <param name="culture">区域信息</param>
        /// <returns>不支持反向转换</returns>
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
          => throw new NotImplementedException();
    }
}