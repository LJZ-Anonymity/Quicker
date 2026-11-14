using System.Reflection;
using System.IO;

namespace Quicker.Helpers
{
    /// <summary>
    /// 应用程序路径管理助手类
    /// 统一管理Quicker应用程序的所有路径
    /// </summary>
    public static class AppPathHelper
    {
        // 程序根目录与配置文件名
        private static readonly string ProgramRoot = AppDomain.CurrentDomain.BaseDirectory;
        private const string CustomPathConfigFileName = "settings.ini"; // 放在程序同目录

        /// <summary>
        /// 应用程序数据根目录
        /// </summary>
        public static string AppDataRoot
        {
            get
            {
                var custom = GetCustomAppDataRootIfConfigured(); // 获取自定义的应用数据根目录
                if (!string.IsNullOrWhiteSpace(custom))
                {
                    return custom; // 使用自定义的应用数据根目录
                }

                return DefaultAppDataRoot; // 默认的应用数据根目录
            }
        }

        /// <summary>
        /// 默认的应用程序数据根目录
        /// </summary>
        private static string DefaultAppDataRoot => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Anonymity", "Quicker");

        /// <summary>
        /// 数据库文件夹路径
        /// </summary>
        public static string DatabaseFolder => Path.Combine(AppDataRoot, "Database");

        /// <summary>
        /// 图片文件夹根路径
        /// </summary>
        public static string ImagesFolder => Path.Combine(AppDataRoot, "Images");

        /// <summary>
        /// 本地图标文件夹路径
        /// </summary>
        public static string LocalIconsFolder => Path.Combine(ImagesFolder, "LocalIcons");

        /// <summary>
        /// 背景图片文件夹路径
        /// </summary>
        public static string BackgroundImagesFolder => Path.Combine(ImagesFolder, "BackgroundImages");

        /// <summary>
        /// 外观分享文件夹路径
        /// </summary>
        public static string SharedAppearanceFolder => Path.Combine(ImagesFolder, "SharedAppearance");

        /// <summary>
        /// 临时数据文件夹路径
        /// </summary>
        public static string TempDataFolder => Path.Combine(AppDataRoot, "TempData");

        /// <summary>
        /// 扩展文件夹路径
        /// </summary>
        public static string ExtensionsFolder => Path.Combine(AppDataRoot, "Extensions");

        /// <summary>
        /// 确保目录存在，如果不存在则创建
        /// </summary>
        /// <param name="path">目录路径</param>
        public static void EnsureDirectoryExists(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }

        /// <summary>
        /// 确保所有应用程序目录都存在
        /// </summary>
        public static void EnsureAllDirectoriesExist()
        {
            var directories = new[]
            {
                AppDataRoot,
                DatabaseFolder,
                ImagesFolder,
                LocalIconsFolder,
                BackgroundImagesFolder,
                SharedAppearanceFolder,
                TempDataFolder,
                ExtensionsFolder
            }; // 所有目录

            foreach (var directory in directories) // 遍历并确保所有目录存在
            {
                EnsureDirectoryExists(directory);
            }

            //LogDirectorie(); // 记录路径
        }

        // 日志记录（修改路径部分）
        private static void LogDirectorie()
        {
            var programRoot = AppDomain.CurrentDomain.BaseDirectory; // 运行时路径
            var logPath = Path.Combine(programRoot, "Directorie.txt"); // 日志文件路径
            File.WriteAllLines(logPath, new[]
            {
                $"生成时间：{DateTime.Now}",
                $"运行时路径 - 程序：{Assembly.GetEntryAssembly()?.GetName().Name}",
                "",
                "[应用路径]",
                AppDataRoot,
                "",
                $"[运行时信息] 程序版本：{Assembly.GetEntryAssembly()?.GetName().Version}"
            });
        }

        /// <summary>
        /// 如果存在配置文件，则读取自定义的应用数据根目录。
        /// 支持两种格式：
        /// 1) 纯路径（第一行非空即为路径）
        /// 2) key=value 格式，例如：path=D:\\QuickerData
        /// </summary>
        /// <returns>自定义的应用数据根目录</returns>
        private static string? GetCustomAppDataRootIfConfigured()
        {
            try
            {
                var configPath = Path.Combine(ProgramRoot, CustomPathConfigFileName);
                if (!File.Exists(configPath)) // 配置文件不存在则创建，并写入默认路径（便于用户后续修改）
                {
                    WriteDefaultConfig(configPath, DefaultAppDataRoot);
                    return null; // 本次仍回退默认
                }

                var allLines = File.ReadAllLines(configPath)
                    .Select(l => l?.Trim())
                    .Where(l => !string.IsNullOrWhiteSpace(l))
                    .Where(l => !(l!.StartsWith("#") || l.StartsWith("//")))
                    .ToList(); // 读取配置文件内容

                if (allLines.Count == 0)
                {
                    // 空文件则写入默认并回退
                    WriteDefaultConfig(configPath, DefaultAppDataRoot);
                    return null;
                }

                string candidate = allLines[0]!;
                var eqIndex = candidate.IndexOf('=');
                if (eqIndex > 0)
                {
                    var key = candidate.Substring(0, eqIndex).Trim();
                    var value = candidate.Substring(eqIndex + 1).Trim();
                    if (key.Equals("path", StringComparison.OrdinalIgnoreCase))
                    {
                        candidate = value;
                    }
                }

                if (string.IsNullOrWhiteSpace(candidate))
                {
                    WriteDefaultConfig(configPath, DefaultAppDataRoot);
                    return null;
                }

                // 允许相对路径：相对于程序目录
                string resolved = Path.IsPathRooted(candidate)
                    ? candidate
                    : Path.GetFullPath(Path.Combine(ProgramRoot, candidate));

                // 规范化并尝试创建
                resolved = Path.GetFullPath(resolved);
                EnsureDirectoryExists(resolved);
                return resolved;
            }
            catch
            {
                return null; // 配置解析失败则回退默认
            }
        }

        /// <summary>
        /// 将默认路径写入配置文件（带简要注释）
        /// </summary>
        /// <param name="configPath">配置文件路径</param>
        /// <param name="defaultRoot">默认根目录</param>
        private static void WriteDefaultConfig(string configPath, string defaultRoot)
        {
            try
            {
                var content = new[]
                {
                    "# Quicker 应用数据根目录配置",
                    string.Concat("path=", defaultRoot)
                };
                File.WriteAllLines(configPath, content);
            }
            catch
            {
                // 忽略写入失败，保持回退逻辑
            }
        }

        /// <summary>
        /// 尝试设置自定义的应用数据根目录，并写入配置文件。
        /// 允许相对路径（相对于程序目录）。
        /// </summary>
        /// <param name="newRoot">新的根目录路径（可为绝对或相对路径）</param>
        /// <param name="resolvedPath">解析后的绝对路径</param>
        /// <returns>是否设置成功</returns>
        public static bool TrySetAppDataRoot(string newRoot, out string? resolvedPath)
        {
            resolvedPath = null;
            try
            {
                if (string.IsNullOrWhiteSpace(newRoot))
                {
                    return false;
                }

                string candidate = newRoot.Trim();
                string resolved = Path.IsPathRooted(candidate)
                    ? candidate
                    : Path.GetFullPath(Path.Combine(ProgramRoot, candidate));

                resolved = Path.GetFullPath(resolved);
                EnsureDirectoryExists(resolved);

                var configPath = Path.Combine(ProgramRoot, CustomPathConfigFileName);
                WriteDefaultConfig(configPath, resolved);

                resolvedPath = resolved;
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}