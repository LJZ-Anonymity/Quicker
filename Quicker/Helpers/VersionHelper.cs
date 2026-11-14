using System.Reflection;

namespace Quicker.Helpers
{
    /// <summary>
    /// 版本号比较工具类
    /// </summary>
    public static class VersionHelper
    {
        public static string CurrentVersion
        {
            get
            {
                try
                {
                    // 尝试获取入口程序集（WPF 程序的 exe）
                    var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
                    var version = assembly?.GetName().Version;
                    if (version == null)
                    {
                        return "0.0.0";
                    }

                    // 统一返回三段版本：主.次.补丁
                    int major = version.Major;
                    int minor = version.Minor;
                    int build = version.Build >= 0 ? version.Build : 0;

                    return $"{major}.{minor}.{build}";
                }
                catch // 兜底，避免因为反射异常导致程序崩溃
                {
                    return "0.0.0";
                }
            }
        }

        /// <summary>
        /// 比较两个版本号
        /// </summary>
        /// <param name="version1">版本号1</param>
        /// <param name="version2">版本号2</param>
        /// <returns>
        /// -1: version1 < version2
        ///  0: version1 == version2
        ///  1: version1 > version2
        /// </returns>
        public static int CompareVersions(string version1, string version2)
        {
            try
            {
                string cleanVersion1 = NormalizeVersion(version1);
                string cleanVersion2 = NormalizeVersion(version2);
                if (Version.TryParse(cleanVersion1, out Version v1) && 
                    Version.TryParse(cleanVersion2, out Version v2))
                {
                    return v1.CompareTo(v2);
                }

                // 如果版本号解析失败，回退到字符串比较
                return string.Compare(version1, version2, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                // 异常情况下回退到字符串比较
                return string.Compare(version1, version2, StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>
        /// 检查是否有新版本可用
        /// </summary>
        public static bool IsNewVersionAvailable(string newVersion)
        {
            return CompareVersions(CurrentVersion, newVersion) < 0;
        }

        /// <summary>
        /// 标准化版本号格式，确保包含四个部分
        /// </summary>
        /// <param name="versionString">原始版本号字符串</param>
        /// <returns>标准化后的版本号字符串</returns>
        private static string NormalizeVersion(string versionString)
        {
            if (string.IsNullOrEmpty(versionString))
                return "0.0.0.0";

            // 确保版本号格式正确（补全缺失的部分）
            var parts = versionString.Split('.');
            if (parts.Length < 2)
            {
                versionString += ".0";
            }
            if (parts.Length < 3)
            {
                versionString += ".0";
            }
            if (parts.Length < 4)
            {
                versionString += ".0";
            }

            return versionString;
        }
    }
} 