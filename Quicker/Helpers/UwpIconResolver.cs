using Windows.ApplicationModel.Core;
using System.Collections.Concurrent;
using Windows.Management.Deployment;
using Windows.ApplicationModel;
using System.Xml.Linq;
using Windows.Storage;
using System.IO;

namespace Quicker.Helpers
{
    /// <summary>
    /// UWP 应用图标获取工具类（静态）。
    /// </summary>
    public static class UwpIconResolver
    {
        // ==================== 静态字段 ====================

        /// <summary>AppUserModelId → 图标文件绝对路径 的缓存字典。</summary>
        private static IDictionary<string, string> _iconPathCache;

        /// <summary>共享的 PackageManager（COM 创建昂贵，全局复用）。</summary>
        private static readonly PackageManager _packageManager = new();

        // ==================== 常量：图标选择优先级 ====================

        /// <summary>Logo 属性名优先级：44 → 71 → 150 → 通用 Logo。</summary>
        private static readonly string[] LogoAttributes =
        {
            "Square44x44Logo",
            "Square71x71Logo",
            "Square150x150Logo",
            "Logo"
        };

        /// <summary>
        /// 图标文件后缀优先级（从优到劣）。
        /// 优先 unplated（无底板，深色主题友好）与 targetsize 精准尺寸。
        /// </summary>
        private static readonly string[] PreferredIconSuffixes =
        {
            ".altform-unplated.png",
            ".targetsize-48.png",
            ".targetsize-48_altform-unplated.png",
            ".targetsize-64.png",
            ".targetsize-64_altform-unplated.png",
            ".png",
            ".scale-200.png",
            ".scale-150.png",
            ".scale-100.png",
            ".targetsize-256.png",
            ".targetsize-256_altform-unplated.png"
        };

        // ==================== 缓存构建 ====================

        /// <summary>
        /// 异步遍历当前用户所有已安装的 UWP 包，构建 AUMID → 图标路径 缓存。
        /// 建议在应用启动时调用一次即可。
        /// </summary>
        public static async Task BuildCacheAsync()
        {
            var cache = new ConcurrentDictionary<string, string>();
            var packages = _packageManager.FindPackagesForUser(string.Empty);

            foreach (Package package in packages)
            {
                await ProcessPackageAsync(package, cache);
            }

            _iconPathCache = cache;
        }

        private static async Task ProcessPackageAsync(
            Package package, ConcurrentDictionary<string, string> cache)
        {
            // 跳过无 UI 入口的包（运行时组件等）
            List<AppListEntry> entries;
            try
            {
                entries = (await package.GetAppListEntriesAsync()).ToList();
            }
            catch
            {
                return;
            }
            if (entries.Count == 0) return;

            // 读取 AppxManifest.xml
            StorageFolder installedLocation;
            StorageFile manifestFile;
            try
            {
                installedLocation = package.InstalledLocation;
                manifestFile = await installedLocation.GetFileAsync("AppxManifest.xml");
            }
            catch
            {
                return; // 无 manifest 或权限不足，跳过
            }

            XDocument manifest;
            try
            {
                manifest = XDocument.Parse(await FileIO.ReadTextAsync(manifestFile));
            }
            catch
            {
                return; // XML 损坏，跳过
            }

            XNamespace defaultNs = manifest.Root.Name.Namespace;
            XNamespace uapNs = manifest.Root.GetNamespaceOfPrefix("uap");

            var applicationElements = manifest
                .Descendants(defaultNs + "Application")
                .ToList();

            foreach (AppListEntry entry in entries)
            {
                TryResolveIconForEntry(
                    entry, installedLocation.Path, uapNs, applicationElements, cache);
            }
        }

        /// <summary>
        /// 解析单个 AppListEntry 的图标路径并写入缓存。
        /// </summary>
        private static void TryResolveIconForEntry(
            AppListEntry entry,
            string installPath,
            XNamespace uapNs,
            List<XElement> applicationElements,
            ConcurrentDictionary<string, string> cache)
        {
            string aumid = entry.AppUserModelId;

            // AUMID 形如 PackageFamilyName!AppId，取 ! 后的 AppId 匹配 XML
            string appId = aumid.Contains('!') ? aumid.Split('!').Last() : aumid;

            XElement application = applicationElements
                .FirstOrDefault(a => (string)a.Attribute("Id") == appId);
            if (application == null) return;

            XElement visualElements = application.Descendants()
                .FirstOrDefault(e => e.Name.LocalName == "VisualElements");
            if (visualElements == null) return;

            // 提取 Logo 属性值
            string logoValue = ExtractLogoValue(visualElements, uapNs);
            if (string.IsNullOrEmpty(logoValue)) return;

            // 在安装目录下按首选后缀匹配最佳图标文件
            string iconPath = PickBestIconFile(installPath, logoValue);
            if (iconPath != null)
            {
                cache[aumid] = iconPath;
            }
        }

        /// <summary>
        /// 按 LogoAttributes 优先级从 DefaultTile / VisualElements 提取 Logo 值。
        /// </summary>
        private static string ExtractLogoValue(XElement visualElements, XNamespace uapNs)
        {
            // DefaultTile 优先
            XElement defaultTile = visualElements.Element(uapNs + "DefaultTile")
                ?? visualElements.Descendants()
                    .FirstOrDefault(e => e.Name.LocalName == "DefaultTile");

            foreach (string attrName in LogoAttributes)
            {
                if (defaultTile != null)
                {
                    string v = (string)defaultTile.Attribute(attrName);
                    if (!string.IsNullOrEmpty(v)) return v;
                }

                string fromVisual = (string)visualElements.Attribute(attrName);
                if (!string.IsNullOrEmpty(fromVisual)) return fromVisual;
            }

            return null;
        }

        /// <summary>
        /// 在安装目录中按首选后缀匹配最佳图标文件；匹配不到时降级使用第一个文件。
        /// </summary>
        private static string PickBestIconFile(string installPath, string logoRelativePath)
        {
            string absolutePath = Path.Combine(installPath, logoRelativePath);
            string directory = Path.GetDirectoryName(absolutePath);
            if (directory == null || !Directory.Exists(directory)) return null;

            string baseName = Path.GetFileNameWithoutExtension(absolutePath);
            string extension = Path.GetExtension(absolutePath);

            string[] candidates = Directory.GetFiles(
                directory,
                baseName + "*" + extension,
                SearchOption.TopDirectoryOnly);

            if (candidates.Length == 0) return null;

            // 按首选后缀顺序匹配
            foreach (string suffix in PreferredIconSuffixes)
            {
                string expected = Path.Combine(directory, baseName + suffix);
                string match = candidates.FirstOrDefault(c =>
                    c.Equals(expected, StringComparison.OrdinalIgnoreCase));
                if (match != null) return match;
            }

            // 无首选匹配，降级用第一个
            return candidates[0];
        }

        // ==================== 对外查询接口 ====================

        /// <summary>
        /// 根据 AppUserModelId 获取图标路径。
        /// 两级回退策略：
        ///   1) 内存缓存 _iconPathCache；
        ///   2) 通过 PackageManager 按 PackageFamilyName 查包并取 Logo。
        /// 全部失败返回 null。
        /// </summary>
        public static string GetIconPath(string aumid)
        {
            if (string.IsNullOrEmpty(aumid)) return null;

            // 1) 缓存命中
            if (_iconPathCache != null && _iconPathCache.TryGetValue(aumid, out var cached))
            {
                return cached;
            }

            // 2) PackageManager 查包取 Logo
            try
            {
                string fromPackage = TryGetIconFromPackage(aumid);
                if (!string.IsNullOrEmpty(fromPackage)) return fromPackage;
            }
            catch
            {
                // 忽略，继续回退
            }

            return null;
        }

        /// <summary>
        /// 回退方案：通过 PackageManager 按 PackageFamilyName 查包并取 Logo。
        /// 优先返回 .altform-unplated.png（无底板，深色主题友好）版本。
        /// </summary>
        private static string TryGetIconFromPackage(string aumid)
        {
            if (string.IsNullOrEmpty(aumid) || !aumid.Contains('!')) return null;

            // AUMID 格式：PackageFamilyName!AppId
            string packageFamilyName = aumid.Split('!')[0];
            if (string.IsNullOrEmpty(packageFamilyName)) return null;

            try
            {
                Package package = _packageManager.FindPackageForUser(string.Empty, packageFamilyName);
                if (package == null) return null;

                Uri logo = package.Logo;
                if (logo == null || string.IsNullOrEmpty(logo.LocalPath)) return null;

                string localPath = logo.LocalPath;

                // 优先尝试 altform-unplated 版本
                string unplated = Path.ChangeExtension(localPath, ".altform-unplated.png");
                if (File.Exists(unplated)) return unplated;
                if (File.Exists(localPath)) return localPath;

                return null;
            }
            catch
            {
                return null;
            }
        }
    }
}