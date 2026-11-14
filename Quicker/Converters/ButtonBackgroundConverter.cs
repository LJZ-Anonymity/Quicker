using System.Globalization;
using System.Windows.Media;
using System.Windows.Data;

namespace Quicker.Converters
{
    /// <summary>
    /// 按钮背景色转换器，根据按钮的 Tag、鼠标悬停状态和不同颜色参数，动态返回对应的 SolidColorBrush。
    /// </summary>
    public class ButtonBackgroundConverter : IMultiValueConverter
    {
        /// <summary>
        /// 将多个绑定值转换为按钮的背景画刷。
        /// </summary>
        /// <param name="values">依次为：Tag, IsMouseOver, ActionButtonColor, ActionButtonMouseOverColor, BlankButtonColor, BlankButtonMouseOverColor</param>
        /// <param name="targetType">目标类型（通常为 Brush）</param>
        /// <param name="parameter">可选参数</param>
        /// <param name="culture">区域信息</param>
        /// <returns>返回对应的 SolidColorBrush</returns>
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
           
            var tag = values[0]; // 第一个参数为 Tag，用于区分是否为“动作”按钮
            var isMouseOver = (bool)values[1]; // 第二个参数为按钮是否鼠标悬停
            var actionColor = values[2]?.ToString(); // 第三个参数为“动作按钮”常规颜色
            var actionHoverColor = values[3]?.ToString(); // 第四个参数为“动作按钮”悬停颜色
            var blankColor = values[4]?.ToString(); // 第五个参数为“空白按钮”常规颜色
            var blankHoverColor = values[5]?.ToString(); // 第六个参数为“空白按钮”悬停颜色

            bool isAction = tag != null; // 判断当前按钮是否为“动作”按钮（Tag 不为 null）
            string colorStr = isAction
                ? (isMouseOver ? actionHoverColor : actionColor)
                : (isMouseOver ? blankHoverColor : blankColor); // 根据按钮类型和鼠标状态选择颜色字符串

            // 防御性处理：如果颜色字符串为空，使用默认色
            if (string.IsNullOrWhiteSpace(colorStr))
                colorStr = "#FFF3F3F3"; // 默认色

            try
            {
                return (SolidColorBrush)new BrushConverter().ConvertFromString(colorStr); // 尝试将颜色字符串转换为 SolidColorBrush
            }
            catch
            {
                return Brushes.Transparent; // 转换失败时返回透明画刷
            }
        }

        /// <summary>
        /// 不支持的反向转换，直接抛出异常。
        /// </summary>
        /// <param name="value">绑定值</param>
        /// <param name="targetTypes">目标类型数组</param>
        /// <param name="parameter">可选参数</param>
        /// <param name="culture">区域信息</param>
        /// <returns>抛出异常</returns>
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}