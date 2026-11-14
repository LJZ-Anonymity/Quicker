namespace Quicker.Converters
{
    /// <summary>
    /// 字体粗细转换器：根据SelectedIndex返回对应FontWeight
    /// </summary>
    public class FontWeightConverter : System.Windows.Data.IValueConverter
    {
        // WPF标准16种字体粗细枚举，顺序需与ComboBox一致
        private static readonly System.Windows.FontWeight[] FontWeightList = new System.Windows.FontWeight[]
        {
            System.Windows.FontWeights.Thin,        // 0
            System.Windows.FontWeights.ExtraLight,  // 1
            System.Windows.FontWeights.UltraLight,  // 2
            System.Windows.FontWeights.Light,       // 3
            System.Windows.FontWeights.Normal,      // 4
            System.Windows.FontWeights.Regular,     // 5
            System.Windows.FontWeights.Medium,      // 6
            System.Windows.FontWeights.DemiBold,    // 7
            System.Windows.FontWeights.SemiBold,    // 8
            System.Windows.FontWeights.Bold,        // 9
            System.Windows.FontWeights.ExtraBold,   // 10
            System.Windows.FontWeights.UltraBold,   // 11
            System.Windows.FontWeights.Black,       // 12
            System.Windows.FontWeights.Heavy,       // 13
            System.Windows.FontWeights.ExtraBlack,  // 14
            System.Windows.FontWeights.UltraBlack   // 15
        };

        /// <summary>
        /// 将SelectedIndex或字符串转换为FontWeight
        /// </summary>
        public object Convert(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            // value 可能是 ComboBoxItem、string、int
            int index = -1;
            if (value is int i)
                index = i;
            else if (value is System.Windows.Controls.ComboBoxItem item && item.Parent is System.Windows.Controls.ComboBox combo)
                index = combo.Items.IndexOf(item);
            else if (value is string s)
            {
                // 尝试用字符串查找索引
                for (int j = 0; j < FontWeightList.Length; j++)
                {
                    if (FontWeightList[j].ToString().Equals(s, System.StringComparison.OrdinalIgnoreCase))
                    {
                        index = j;
                        break;
                    }
                }
            }
            if (index >= 0 && index < FontWeightList.Length)
                return FontWeightList[index];
            return System.Windows.FontWeights.Normal;
        }

        /// <summary>
        /// 将FontWeight反向转换为索引
        /// </summary>
        public object ConvertBack(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            // 反向转换为索引
            if (value is System.Windows.FontWeight fw)
            {
                for (int i = 0; i < FontWeightList.Length; i++)
                {
                    if (FontWeightList[i] == fw)
                        return i;
                }
            }
            return 4; // Normal
        }

        /// <summary>
        /// 静态方法：根据索引获取FontWeight
        /// </summary>
        public static System.Windows.FontWeight IndexToFontWeight(int selectedIndex)
        {
            if (selectedIndex >= 0 && selectedIndex < FontWeightList.Length)
                return FontWeightList[selectedIndex];
            return System.Windows.FontWeights.Normal;
        }

        /// <summary>
        /// 静态方法：根据FontWeight获取索引
        /// </summary>
        public static int FontWeightToIndex(System.Windows.FontWeight fontWeight)
        {
            for (int i = 0; i < FontWeightList.Length; i++)
            {
                if (FontWeightList[i] == fontWeight)
                    return i;
            }
            return 4; // Normal
        }
    }
}