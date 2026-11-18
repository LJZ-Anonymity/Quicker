using Quicker.Models.Settings;
using System.IO;

namespace Quicker.Helpers
{
    /// <summary>
    /// 黑名单规则的规范化：所有写入入口都必须经过这里，保证库里同一个目标只有一种写法
    /// </summary>
    public static class BlacklistRuleNormalizer
    {
        /// <summary>
        /// 按匹配方式规范化目标
        /// </summary>
        public static string Normalize(BlacklistMatchType matchType, string input)
        {
            return matchType switch
            {
                BlacklistMatchType.ProcessName => NormalizeProcessName(input),
                BlacklistMatchType.Folder => NormalizeFolderPath(input),
                _ => string.Empty
            };
        }

        /// <summary>
        /// 规范化进程名：支持传入完整路径、带 .exe 的文件名或纯进程名，统一成"小写、无扩展名"
        /// </summary>
        public static string NormalizeProcessName(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;

            string name = input.Trim().Trim('"');
            name = Path.GetFileName(name); // 完整路径取文件名；纯进程名保持不变

            if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                name = name.Substring(0, name.Length - 4);

            return name.Trim().ToLowerInvariant();
        }

        /// <summary>
        /// 规范化文件夹路径：转成完整路径并去掉末尾分隔符（保留原有大小写，比较时一律忽略大小写）
        /// </summary>
        public static string NormalizeFolderPath(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;

            try
            {
                string path = Path.GetFullPath(input.Trim().Trim('"'));
                return path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            catch
            {
                return string.Empty; // 路径格式非法
            }
        }
    }
}