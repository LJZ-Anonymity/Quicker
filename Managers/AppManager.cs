using WinRTSize = Windows.Foundation.Size;
using AppInfo = Quicker.Models.AppInfo;
using System.Runtime.InteropServices;
using System.Collections.Concurrent;
using Windows.Management.Deployment;
using Windows.ApplicationModel.Core;
using System.Windows.Media.Imaging;
using System.Security.Cryptography;
using Windows.ApplicationModel;
using System.Windows.Interop;
using System.ComponentModel;
using System.Windows.Media;
using System.Diagnostics;
using Microsoft.Win32;
using Quicker.Helpers;
using System.Windows;
using System.Text;
using System.IO;

namespace Quicker.Managers
{
    /// <summary>
    /// 应用索引管理器：从注册表、UWP 包等来源汇总本机应用列表。
    /// </summary>
    public class AppManager
    {
        // ==================== 常量 ====================
        private const int SLR_NO_UI = 0x00000001;
        private const int MinAcceptableUwpIconSize = 64;
        private const int MaxUwpConcurrency = 4;
        private static readonly TimeSpan LinkCacheTtl = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan HashCacheTtl = TimeSpan.FromMinutes(10);

        // ==================== 静态共享 ====================
        private static readonly ConcurrentDictionary<string, WeakReference<ImageSource>> _iconCache = new();
        private static readonly Timer _cleanupTimer;
        private static readonly PackageManager _packageManager = new();
        private static readonly SemaphoreSlim _uwpThrottle =
            new(MaxUwpConcurrency, MaxUwpConcurrency);

        // UWP 图标缓存懒初始化
        private static readonly Lazy<Task> _uwpIconInitLazy =
            new(() => UwpIconResolver.BuildCacheAsync(),
                LazyThreadSafetyMode.ExecutionAndPublication);

        // ==================== 实例级缓存 ====================
        private readonly ConcurrentDictionary<string, (string Value, DateTime CachedAt)> _linkTargetCache = new();
        private readonly ConcurrentDictionary<string, (string Value, DateTime CachedAt)> _fileHashCache = new();
        private readonly ConcurrentDictionary<string, byte> _addedKeys =
            new(StringComparer.OrdinalIgnoreCase);

        static AppManager()
        {
            _cleanupTimer = new Timer(
                CleanupIconCache, null,
                TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
        }

        public AppManager() { }

        // 缓存清理

        public void ClearCache()
        {
            _iconCache.Clear();
            _linkTargetCache.Clear();
            _fileHashCache.Clear();
            _addedKeys.Clear();
        }

        private static void CleanupIconCache(object state)
        {
            var expiredKeys = _iconCache.Keys
                .Where(key => !_iconCache.TryGetValue(key, out var wr) || !wr.TryGetTarget(out _))
                .ToList();

            foreach (var key in expiredKeys)
                _iconCache.TryRemove(key, out _);
        }

        // 顶层加载

        public async Task<List<AppInfo>> LoadAllApplicationsAsync()
        {
            _addedKeys.Clear();

            var allApps = new ConcurrentBag<AppInfo>();
            await Task.WhenAll(
                LoadFromRegistryAsync(allApps),
                LoadUWPAppsAsync(allApps)
            );

            return allApps
                .OrderBy(app => app.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        // 注册表应用

        private async Task LoadFromRegistryAsync(ConcurrentBag<AppInfo> appsList)
        {
            try
            {
                var subKeys = GetRegistryAppSubKeys().ToList();
                if (subKeys.Count == 0) return;

                await Parallel.ForEachAsync(
                    subKeys,
                    new ParallelOptions { MaxDegreeOfParallelism = 8 },
                    async (subKeyName, ct) => await TryAddRegistryAppAsync(subKeyName, appsList));
            }
            catch { }
        }

        private IEnumerable<string> GetRegistryAppSubKeys()
        {
            using RegistryKey key = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths");
            return key?.GetSubKeyNames() ?? Array.Empty<string>();
        }

        private async Task TryAddRegistryAppAsync(string subKeyName, ConcurrentBag<AppInfo> appsList)
        {
            try
            {
                using RegistryKey subKey = Registry.LocalMachine.OpenSubKey(
                    $@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\{subKeyName}");

                string path = (string)subKey?.GetValue("");
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;

                string appName = Path.GetFileNameWithoutExtension(path);
                var icon = await GetIconAsync(path);

                AddAppInfo(appsList, appName, path, icon, path, path);
            }
            catch { }
        }

        // 统一添加入口

        private void AddAppInfo(
            ConcurrentBag<AppInfo> appsList,
            string name,
            string location,
            ImageSource icon,
            string tag,
            string launchTarget = null)
        {
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(location))
                return;

            if (!TryMarkAdded(name, location))
                return;

            appsList.Add(new AppInfo
            {
                Name = name,
                Location = location,
                Icon = icon,
                Tag = tag
            });
        }

        private bool TryMarkAdded(string name, string location)
        {
            string key = (name ?? string.Empty) + "\u0001" + (location ?? string.Empty);
            return _addedKeys.TryAdd(key, 0);
        }

        // ==================== UWP 应用 ====================

        private async Task LoadUWPAppsAsync(ConcurrentBag<AppInfo> appsList)
        {
            try
            {
                // ① 预热图标缓存（懒加载，仅一次）
                await _uwpIconInitLazy.Value;

                // ② 枚举所有 UWP 包
                var packages = _packageManager.FindPackagesForUser("").ToList();

                // ③ 信号量限流
                var tasks = packages.Select(async package =>
                {
                    await _uwpThrottle.WaitAsync();
                    try { await ProcessUwpPackageAsync(package, appsList); }
                    catch { }
                    finally { _uwpThrottle.Release(); }
                });

                await Task.WhenAll(tasks);
            }
            catch { }
        }

        /// <summary>
        /// 判断 UWP 包是否需要纳入搜索列表。
        /// 只过滤运行时/框架包，保留计算器、记事本等系统应用。
        /// </summary>
        private bool IsValidUwpPackage(Package package)
        {
            try
            {
                if (package.IsFramework || package.IsResourcePackage || package.IsBundle)
                    return false;
                if (string.IsNullOrWhiteSpace(package.DisplayName))
                    return false;
                if (package.DisplayName.Equals(package.Id.Name, StringComparison.OrdinalIgnoreCase))
                    return false;
                if (package.IsDevelopmentMode)
                    return false;

                string name = package.Id.Name;
                if (name.StartsWith("Microsoft.VCLibs", StringComparison.OrdinalIgnoreCase)) return false;
                if (name.StartsWith("Microsoft.NET.", StringComparison.OrdinalIgnoreCase)) return false;
                if (name.StartsWith("Microsoft.UI.Xaml", StringComparison.OrdinalIgnoreCase)) return false;
                if (name.StartsWith("Microsoft.Services.Store.Engagement", StringComparison.OrdinalIgnoreCase)) return false;
                if (name.StartsWith("Microsoft.WindowsAppRuntime", StringComparison.OrdinalIgnoreCase)) return false;
                if (name.StartsWith("Microsoft.WinJS", StringComparison.OrdinalIgnoreCase)) return false;

                return true;
            }
            catch { return false; }
        }

        private async Task ProcessUwpPackageAsync(Package package, ConcurrentBag<AppInfo> appsList)
        {
            if (!IsValidUwpPackage(package)) return;

            IReadOnlyList<AppListEntry> appEntries;
            try { appEntries = await package.GetAppListEntriesAsync(); }
            catch { return; }

            foreach (var appEntry in appEntries)
            {
                try { await AddUwpAppEntryAsync(appEntry, package, appsList); }
                catch { }
            }
        }

        private async Task AddUwpAppEntryAsync(
            AppListEntry appEntry, Package package, ConcurrentBag<AppInfo> appsList)
        {
            string aumid = appEntry.AppUserModelId;
            if (string.IsNullOrEmpty(aumid)) return;

            string displayName = appEntry.DisplayInfo?.DisplayName;
            if (string.IsNullOrWhiteSpace(displayName)) return;

            ImageSource icon = await GetUwpAppIconAsync(appEntry, package.Id.FamilyName, aumid);

            AddAppInfo(
                appsList,
                displayName,
                aumid,
                icon,
                "[Windows 商店应用]",
                aumid);
        }

        /// <summary>
        /// 获取 UWP 图标（三级回退）：
        ///   ① UwpIconResolver 的磁盘缓存（含 unplated 优先）
        ///   ② WinRT DisplayInfo.GetLogo(400x400)
        ///   ③ 返回 null
        /// </summary>
        private async Task<ImageSource> GetUwpAppIconAsync(
            AppListEntry appEntry, string packageFamilyName, string aumid)
        {
            // ① 磁盘缓存
            try
            {
                string iconPath = UwpIconResolver.GetIconPath(aumid);
                if (!string.IsNullOrEmpty(iconPath) && File.Exists(iconPath))
                {
                    var fromFile = LoadIconFromPath(iconPath);
                    if (fromFile != null) return fromFile;
                }
            }
            catch { }

            // ② WinRT DisplayInfo 回退
            try
            {
                var streamRef = appEntry.DisplayInfo.GetLogo(new WinRTSize(400, 400));
                using var randomAccessStream = await streamRef.OpenReadAsync();

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.StreamSource = randomAccessStream.AsStream();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();

                if (bitmap.PixelWidth >= MinAcceptableUwpIconSize)
                    return bitmap;
            }
            catch { }

            return null;
        }

        /// <summary>
        /// 从文件路径加载图标
        /// </summary>
        /// <param name="filePath"> 文件路径 </param>
        /// <returns></returns>
        private async Task<ImageSource> GetIconAsync(string filePath)
        {
            if (_iconCache.TryGetValue(filePath, out var weakRef)
                && weakRef.TryGetTarget(out var cachedIcon))
                return cachedIcon;

            ImageSource icon = null;
            try
            {
                using var iconEx = System.Drawing.Icon.ExtractAssociatedIcon(filePath);
                if (iconEx != null)
                {
                    icon = Imaging.CreateBitmapSourceFromHIcon(
                        iconEx.Handle, Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());
                }
            }
            catch { }

            if (icon != null)
                _iconCache[filePath] = new WeakReference<ImageSource>(icon);

            return icon;
        }

        /// <summary>
        ///从 .lnk 解析目标路径（带 TTL）
        /// </summary>
        /// <param name="linkFilePath"> .lnk 文件路径 </param>
        /// <returns></returns>
        private string GetTargetPathFromLinkFile(string linkFilePath)
        {
            if (_linkTargetCache.TryGetValue(linkFilePath, out var entry)
                && DateTime.UtcNow - entry.CachedAt < LinkCacheTtl)
                return entry.Value;

            try
            {
                IShellLink shellLink = (IShellLink)new ShellLink();
                shellLink.SetPath(linkFilePath);

                var path = new StringBuilder(260);
                shellLink.GetPath(path, path.Capacity, out _, SLR_NO_UI);
                string targetPath = path.ToString();

                _linkTargetCache[linkFilePath] = (targetPath, DateTime.UtcNow);
                return targetPath;
            }
            catch { return null; }
        }

        /// <summary>
        /// 获取图标（三级回退）
        /// </summary>
        /// <param name="linkFilePath"> .lnk 文件路径 </param>
        /// <returns></returns>
        private ImageSource ShellIconFromLink(string linkFilePath)
        {
            string cacheKey = GetCacheKey(linkFilePath);
            if (cacheKey == null) return null;

            if (_iconCache.TryGetValue(cacheKey, out var wr) && wr.TryGetTarget(out var cached))
                return cached;

            try
            {
                string targetPath = GetTargetPathFromLinkFile(linkFilePath);
                if (!string.IsNullOrEmpty(targetPath))
                {
                    var icon1 = ExtractIconFromPath(targetPath);
                    if (icon1 != null)
                    {
                        _iconCache[cacheKey] = new WeakReference<ImageSource>(icon1);
                        return icon1;
                    }
                }

                var icon2 = ExtractIconFromPath(linkFilePath);
                if (icon2 != null)
                {
                    _iconCache[cacheKey] = new WeakReference<ImageSource>(icon2);
                    return icon2;
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// 生成缓存键
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        private string GetCacheKey(string filePath)
        {
            if (_fileHashCache.TryGetValue(filePath, out var entry)
                && DateTime.UtcNow - entry.CachedAt < HashCacheTtl)
                return entry.Value;

            var fileInfo = new FileInfo(filePath);
            if (!fileInfo.Exists) return null;

            string cacheKey;
            if (fileInfo.Length < 1024 * 1024)
            {
                using var md5 = MD5.Create();
                using var stream = File.OpenRead(filePath);
                byte[] hashBytes = md5.ComputeHash(stream);
                cacheKey = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            }
            else
            {
                cacheKey = $"{fileInfo.Length}_{fileInfo.LastWriteTimeUtc.Ticks}";
            }

            _fileHashCache[filePath] = (cacheKey, DateTime.UtcNow);
            return cacheKey;
        }

        /// <summary>
        /// 从磁盘路径提取图标
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        private ImageSource ExtractIconFromPath(string filePath)
        {
            try
            {
                using var icon = System.Drawing.Icon.ExtractAssociatedIcon(filePath);
                if (icon != null)
                {
                    return Imaging.CreateBitmapSourceFromHIcon(
                        icon.Handle, Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// 从磁盘路径加载图标
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        private ImageSource LoadIconFromPath(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    string directory = Path.GetDirectoryName(filePath);
                    string fileName = Path.GetFileNameWithoutExtension(filePath);
                    string extension = Path.GetExtension(filePath);

                    foreach (var scale in new[] { ".scale-100", ".scale-200", ".scale-400" })
                    {
                        string scaledPath = Path.Combine(directory, fileName + scale + extension);
                        if (File.Exists(scaledPath)) { filePath = scaledPath; break; }
                    }
                    if (!File.Exists(filePath)) return null;
                }

                var bitmap = new BitmapImage();
                using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = stream;
                    bitmap.EndInit();
                }
                bitmap.Freeze();
                return bitmap;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"加载图标失败: {ex.Message}");
                return null;
            }
        }

        // 资源清理
        public void Cleanup()
        {
            CleanupIconCache(null);
            _linkTargetCache?.Clear();
            _fileHashCache?.Clear();
            _addedKeys?.Clear();
        }

        // Shell 快捷方式 COM 接口

        [ComImport]
        [Guid("0000010c-0000-0000-C000-000000000046")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IShellLink
        {
            void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile,
                int cchMaxPath, out int pFlags, int fResolve);
            void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
        }

        [ComImport]
        [Guid("00021401-0000-0000-C000-000000000046")]
        [ClassInterface(ClassInterfaceType.None)]
        [ProgId("Shell.Application")]
        public class ShellLink { }
    }
}