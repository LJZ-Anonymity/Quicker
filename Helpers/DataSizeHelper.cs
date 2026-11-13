namespace Quicker.Helpers
{
    public static class DataSizeHelper
    {
        /// <summary>
        /// 转换数据
        /// </summary>
        /// <param name="data"> 数据大小 </param>
        /// <returns> 转换后的数据 </returns>
        public static int ConversionData(int data)
        {
            if (data < 1024)
                return data; // 字节
            else if (data < 1024 * 1024)
                return data / 1024; // 千字节
            else if (data < 1024 * 1024 * 1024)
                return data / (1024 * 1024); // 兆字节
            return data / (1024 * 1024 * 1024); // 吉字节
        }

        /// <summary>
        /// 转换单位
        /// </summary>
        /// <param name="unit"> 数据大小 </param>
        /// <returns> 大小单位 </returns>
        public static string ConversionUnits(int unit)
        {
            if (unit < 1024)
                return "B"; // 字节
            else if (unit < 1024 * 1024)
                return "KB"; // 千字节
            else if (unit < 1024 * 1024 * 1024)
                return "MB"; // 兆字节
            return "GB"; // 吉字节
        }
    }
}