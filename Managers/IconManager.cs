using Image = System.Windows.Controls.Image;
using System.Runtime.InteropServices;
using System.Windows.Media.Animation;
using System.Security.Cryptography;
using System.Windows.Media.Imaging;
using Quicker.Windows.ToolWindows;
using System.Windows.Controls;
using System.Windows.Interop;
using File = System.IO.File;
using Quicker.Windows.Menus;
using System.Windows.Media;
using IWshRuntimeLibrary;
using Quicker.Database;
using System.Net.Http;
using Quicker.Helpers;
using System.Windows;
using WpfAnimatedGif;
using System.Drawing;
using System.Text;
using System.Net;
using System.IO;
using Svg;

namespace Quicker.Managers
{
    internal class IconManager
    {
        #region 字段
        
        // 静态 HttpClient 实例，用于网络请求
        private static readonly HttpClient httpClient = new(new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate, // 启用压缩
            MaxConnectionsPerServer = 2, // 限制每个服务器的连接数
            UseProxy = false, // 禁用代理以提高性能
            ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true // 忽略SSL证书验证错误
        })
        {
            Timeout = TimeSpan.FromSeconds(10) // 设置超时时间
        };

        // 释放图标资源
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        // 获取文件图标
        [StructLayout(LayoutKind.Sequential)]
        private struct SHFILEINFO
        {
            public nint hIcon; // 图标句柄
            public int iIcon; // 图标索引
            public uint dwAttributes; // 文件属性
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName; // 文件显示名
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName; // 文件类型名
        } // 文件信息结构体
        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern nint SHGetFileInfo(string pszPath, uint dwFileAttributes, out SHFILEINFO psfi, uint cbFileInfo, uint uFlags); // 获取文件信息

        // 文件图标相关常量
        private const uint SHGFI_USEFILEATTRIBUTES = 0x000000010; // 使用文件属性
        private const uint FILE_ATTRIBUTE_NORMAL = 0x00000080; // 正常文件属性
        private const uint SHGFI_SYSICONINDEX = 0x000004000; // 系统图标索引
        private const uint SHGFI_LARGEICON = 0x000000000; // 大图标
        private const uint SHGFI_ICON = 0x000000100; // 图标
        private const uint ILD_NORMAL = 0x00000000; // 正常图标
        
        [DllImport("comctl32.dll")]
        private static extern IntPtr ImageList_GetIcon(IntPtr himl, int i, uint flags); // 从系统图像列表中获取图标（不带叠加图标）
        
        // 直接从文件提取图标（避免压缩标记）
        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern int ExtractIconEx(string lpszFile, int nIconIndex, out IntPtr phiconLarge, out IntPtr phiconSmall, int nIcons); // 从文件提取图标

        // 获取系统特殊文件夹图标
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct SHSTOCKICONINFO
        {
            public uint cbSize;
            public IntPtr hIcon;
            public int iSysIconIndex;
            public int iIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szPath;
        }

        [DllImport("shell32.dll", SetLastError = false, CharSet = CharSet.Unicode)]
        private static extern int SHGetStockIconInfo(uint siid, uint uFlags, ref SHSTOCKICONINFO psii);

        private const uint FILE_ATTRIBUTE_DIRECTORY = 0x00000010;
        private const uint SHGSI_LARGEICON = 0x000000000;
        private const uint SHGSI_ICON = 0x000000100;

        // 系统图标 ID
        private const uint SIID_DOWNLOADS = 184; // Win8+ 支持

        #endregion

        /// <summary>
        /// 将 HIcon 转换为 ImageSource
        /// </summary>
        /// <param name="hIcon">HIcon 指针</param>
        /// <returns>ImageSource 对象</returns>
        private static ImageSource ConvertHIconToImageSource(IntPtr hIcon)
        {
            if (hIcon == IntPtr.Zero) return null;
            ImageSource iconSource = Imaging.CreateBitmapSourceFromHIcon(
                hIcon,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions()
            );
            iconSource.Freeze();
            return iconSource;
        }

        /// <summary>
        /// 从快捷方式文件中获取目标路径
        /// </summary>
        /// <param name="linkFilePath">快捷方式文件路径</param>
        /// <returns>目标路径</returns>
        private static string GetTargetPathFromLinkFile(string linkFilePath)
        {
            if (string.IsNullOrEmpty(linkFilePath) || !File.Exists(linkFilePath)) return null;
            IWshShortcut shortcut = null;
            WshShell shell = null;
            try
            {
                shell = new WshShell();
                shortcut = (IWshShortcut)shell.CreateShortcut(linkFilePath);
                string targetPath = shortcut.TargetPath;
                if (string.IsNullOrEmpty(targetPath)) return null;
                return targetPath;
            }
            catch
            {
                return null;
            }
            finally
            {
                if (shortcut != null) Marshal.ReleaseComObject(shortcut);
                if (shell != null) Marshal.ReleaseComObject(shell);
            }
        }

        /// <summary>
        /// 检查路径是否为系统文件夹路径（CLSID 路径）
        /// </summary>
        /// <param name="path">路径</param>
        /// <returns>是否为系统文件夹路径</returns>
        private static bool IsSystemFolderPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            return path.StartsWith("::", StringComparison.Ordinal);
        }

        /// <summary>
        /// 检查目标路径是否有效（系统文件夹路径、文件或目录）
        /// </summary>
        /// <param name="targetPath">目标路径</param>
        /// <returns>是否有效</returns>
        private static bool IsValidTargetPath(string targetPath)
        {
            return !string.IsNullOrEmpty(targetPath) && 
                   (IsSystemFolderPath(targetPath) || File.Exists(targetPath) || Directory.Exists(targetPath));
        }

        /// <summary>
        /// 检查文件扩展名是否为可执行文件
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>是否为可执行文件</returns>
        private static bool IsExecutableFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return false;
            string ext = Path.GetExtension(filePath).ToLower();
            return ext == ".exe" || ext == ".dll";
        }

        /// <summary>
        /// 从目标路径获取图标，优先使用直接提取方法（避免压缩标记）
        /// </summary>
        /// <param name="targetPath">目标路径</param>
        /// <returns>图标对象 (ImageSource)，如果失败则返回 null</returns>
        private static ImageSource GetIconFromTargetPath(string targetPath)
        {
            if (!IsValidTargetPath(targetPath)) return null;
            if (IsSystemFolderPath(targetPath)) // 对于 CLSID 路径，直接使用 GetIconFromPath
            {
                return GetIconFromPath(targetPath);
            }

            // 对于 EXE/DLL 文件，优先直接从文件提取图标（完全避免压缩标记）
            if (IsExecutableFile(targetPath))
            {
                ImageSource directIcon = ExtractIconDirectlyFromFile(targetPath);
                if (directIcon != null)
                    return directIcon;
            }

            // 优先使用系统图标列表方法获取不带叠加图标的图标
            ImageSource targetIcon = GetIconFromImageList(targetPath);
            if (targetIcon != null)
                return targetIcon;

            return GetIconFromPath(targetPath); // 如果系统图标列表方法失败，回退到标准方法
        }

        /// <summary>
        /// 判断快捷方式是否为操作系统快捷方式
        /// </summary>
        /// <param name="linkFilePath">快捷方式文件路径</param>
        /// <returns>是否为操作系统快捷方式</returns>
        private static bool IsSystemShortcut(string linkFilePath)
        {
            if (string.IsNullOrEmpty(linkFilePath)) return false;
            try
            {
                string normalizedPath = Path.GetFullPath(linkFilePath).ToLowerInvariant();
                string appDataStartMenu = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Microsoft", "Windows", "Start Menu", "Programs"
                ).ToLowerInvariant();
                string commonPrograms = Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms).ToLowerInvariant();
                string userStartMenu = Environment.GetFolderPath(Environment.SpecialFolder.StartMenu).ToLowerInvariant();
                string commonStartMenu = Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu).ToLowerInvariant();

                return normalizedPath.StartsWith(appDataStartMenu, StringComparison.OrdinalIgnoreCase) ||
                       normalizedPath.StartsWith(commonPrograms, StringComparison.OrdinalIgnoreCase) ||
                       normalizedPath.StartsWith(commonStartMenu, StringComparison.OrdinalIgnoreCase) ||
                       normalizedPath.StartsWith(userStartMenu, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 获取操作系统快捷方式的源图标（不带快捷方式标识）
        /// </summary>
        /// <param name="linkFilePath">快捷方式文件路径</param>
        /// <returns>操作系统快捷方式的源图标（不带快捷方式标识）</returns>
        private static ImageSource GetSystemShortcutIcon(string linkFilePath)
        {
            if (string.IsNullOrEmpty(linkFilePath) || !File.Exists(linkFilePath)) return null;
            try // 对于快捷方式，始终从目标路径获取图标，而不是从快捷方式文件本身
            {
                string targetPath = GetTargetPathFromLinkFile(linkFilePath);
                return GetIconFromTargetPath(targetPath);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 从指定路径获取图标
        /// </summary>
        /// <param name="path">文件或文件夹路径</param>
        /// <returns>图标对象 (ImageSource)，如果失败则返回 null</returns>
        private static ImageSource GetIconFromPath(string path)
        {
            SHFILEINFO shfi = new();
            uint flags = SHGFI_ICON | SHGFI_LARGEICON;
            
            IntPtr hIcon = SHGetFileInfo(
                path,
                FILE_ATTRIBUTE_NORMAL,
                out shfi,
                (uint)Marshal.SizeOf(shfi),
                flags
            );
            
            if (hIcon != IntPtr.Zero && shfi.hIcon != IntPtr.Zero)
            {
                try
                {
                    ImageSource iconSource = ConvertHIconToImageSource(shfi.hIcon);
                    DestroyIcon(shfi.hIcon);
                    return iconSource;
                }
                catch
                {
                    if (shfi.hIcon != IntPtr.Zero)
                        DestroyIcon(shfi.hIcon);
                    return null;
                }
            }
            
            return null;
        }

        /// <summary>
        /// 从指定路径的文件中提取图标，确保移除快捷方式箭头等叠加图标。
        /// </summary>
        /// <param name="filePath">文件路径</param>
        public static ImageSource GetIcon(string filePath)
        {
            // 对于文件夹
            if (Directory.Exists(filePath))
            {
                // 优先获取系统内置特殊图标（如桌面），彻底防止被压缩属性污染
                ImageSource specialIcon = GetSpecialFolderIcon(filePath);
                if (specialIcon != null) return specialIcon;

                // 尝试解析 desktop.ini 自定义图标，直接提源文件，避免系统覆盖
                ImageSource customIcon = GetFolderCustomIcon(filePath);
                if (customIcon != null) return customIcon;

                // 兜底：强制返回标准文件夹图标（不带任何覆盖符号）
                return GetStandardFolderIcon(filePath) ?? GetIconFromImageList(filePath) ?? GetIconFromPath(filePath);
            }

            #region 对于快捷方式

            bool isLinkFile = Path.GetExtension(filePath).Equals(".lnk", StringComparison.OrdinalIgnoreCase);
            if (isLinkFile)
            {
                // 2.1 优先读取 IconLocation（例如模拟器游戏快捷方式往往在这里记录真实图标）
                string iconLocation = GetIconLocationFromLinkFile(filePath);
                if (!string.IsNullOrWhiteSpace(iconLocation))
                {
                    string iconPath = iconLocation;
                    int iconIndex = 0;

                    // IconLocation 格式通常是 "C:\path\app.exe,0" 或 "C:\path\icon.ico"
                    int commaIndex = iconLocation.LastIndexOf(',');
                    if (commaIndex > 0)
                    {
                        iconPath = iconLocation.Substring(0, commaIndex).Trim();
                        string indexStr = iconLocation.Substring(commaIndex + 1).Trim();
                        int.TryParse(indexStr, out iconIndex);
                    }

                    iconPath = iconPath.Trim('"');

                    if (File.Exists(iconPath))
                    {
                        // 如果是 exe 或 dll，直接通过 ExtractIconEx 提取，彻底避免快捷方式箭头
                        if (IsExecutableFile(iconPath))
                        {
                            ImageSource resIcon = ExtractIconDirectlyFromFileWithIndex(iconPath, iconIndex);
                            if (resIcon != null) return resIcon;
                        }
                        else
                        {
                            // 如果是 ico 图片等，直接读取该文件
                            ImageSource imgIcon = GetIconFromImageList(iconPath) ?? GetIconFromPath(iconPath);
                            if (imgIcon != null) return imgIcon;
                        }
                    }
                }

                // 2.2 如果没有有效的 IconLocation，尝试提取 TargetPath 的图标
                string targetPath = GetTargetPathFromLinkFile(filePath);
                if (IsValidTargetPath(targetPath))
                {
                    ImageSource targetIcon = GetIconFromTargetPath(targetPath);
                    if (targetIcon != null) return targetIcon;
                }

                // 2.3 如果所有途径都失败了，绝不对 .lnk 本身提取（必然带小箭头），交由上层处理
                return null;
            }

            #endregion

            // 对于普通文件，使用标准方法
            return GetIconWithStandardMethod(filePath, false, false);
        }

        /// <summary>
        /// 从系统图标列表中获取图标（不带叠加图标）
        /// </summary>
        /// <param name="filePath">文件或文件夹路径（可以是快捷方式、普通文件或CLSID路径）</param>
        /// <returns>图标对象 (ImageSource)，如果失败则返回 null</returns>
        private static ImageSource GetIconFromImageList(string filePath)
        {
            SHFILEINFO shfi = new();
            uint flags = SHGFI_SYSICONINDEX | SHGFI_LARGEICON;
            IntPtr hImageList = SHGetFileInfo(
                filePath,
                FILE_ATTRIBUTE_NORMAL,
                out shfi,
                (uint)Marshal.SizeOf(shfi),
                flags
            );

            if (hImageList == IntPtr.Zero || shfi.iIcon < 0)
                return null;

            IntPtr hIconFromList = ImageList_GetIcon(hImageList, shfi.iIcon, ILD_NORMAL);
            if (hIconFromList == IntPtr.Zero)
                return null;

            try
            {
                ImageSource iconSource = ConvertHIconToImageSource(hIconFromList);
                DestroyIcon(hIconFromList);
                return iconSource;
            }
            catch
            {
                DestroyIcon(hIconFromList);
                return null;
            }
        }

        /// <summary>
        /// 使用标准方法获取图标
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="isLinkFile">是否为快捷方式文件</param>
        /// <param name="hasValidTarget">快捷方式是否有有效的目标路径（仅当 isLinkFile 为 true 时有效）</param>
        private static ImageSource GetIconWithStandardMethod(string filePath, bool isLinkFile, bool hasValidTarget = false)
        {
            SHFILEINFO shfi = new();
            uint flags = SHGFI_ICON | SHGFI_LARGEICON;

            // 对于普通文件或无法获取目标路径的快捷方式，使用 SHGFI_USEFILEATTRIBUTES 可以提高性能
            // 对于快捷方式的目标文件，不使用此标志以确保获取原始图标
            if (!(isLinkFile && hasValidTarget))
                flags |= SHGFI_USEFILEATTRIBUTES;

            IntPtr hIcon = SHGetFileInfo(
                filePath,
                FILE_ATTRIBUTE_NORMAL,
                out shfi,
                (uint)Marshal.SizeOf(shfi),
                flags
            );

            if (hIcon == IntPtr.Zero || shfi.hIcon == IntPtr.Zero)
                return null;

            try
            {
                ImageSource iconSource = ConvertHIconToImageSource(shfi.hIcon);
                DestroyIcon(shfi.hIcon);
                return iconSource;
            }
            catch
            {
                DestroyIcon(shfi.hIcon);
                return null;
            }
        }

        /// <summary>
        /// 检查缓存的图标
        /// </summary>
        /// <param name="filePath"> 文件路径 </param>
        /// <returns> 图标文件路径 </returns>
        public string CheckCachedIcon(string filePath)
        {
            try
            {
                string hash = GetFileContentHash(filePath); // 获取文件内容哈希值
                if (Directory.Exists(filePath)) // 检查是否为文件夹
                {
                    string iconPath = Path.Combine(AppPathHelper.LocalIconsFolder, $"{hash}.png"); // 拼接图标文件路径
                    return File.Exists(iconPath) ? iconPath : null; // 如果文件存在，返回路径
                }
                else // 文件使用原始扩展名
                {
                    string ext = Path.GetExtension(filePath).ToLower(); // 获取文件扩展名
                    string iconPath = Path.Combine(AppPathHelper.LocalIconsFolder, $"{hash}{ext}"); // 拼接图标文件路径
                    return File.Exists(iconPath) ? iconPath : null; // 如果文件存在，返回路径
                }
            }
            catch
            {
                ShowToast("检查缓存图标失败。", ToastType.Error); // 显示Toast
                return null; // 如果出现异常，返回 null
            }
        }

        /// <summary>
        /// 保存ImageSource为本地图标目录的PNG图片，文件名为内容哈希，避免重复
        /// </summary>
        /// <param name="imageSource">图片源</param>
        /// <returns>保存路径</returns>
        public string SaveIconToFile(ImageSource imageSource)
        {
            try
            {
                BitmapSource bitmapSource = imageSource as BitmapSource; // 将 ImageSource 转换为 BitmapSource
                if (bitmapSource == null) return null; // 如果转换失败，返回 null
                using (MemoryStream stream = new()) // 创建内存流
                {
                    PngBitmapEncoder encoder = new(); // 创建 PNG 编码器
                    encoder.Frames.Add(BitmapFrame.Create(bitmapSource)); // 将 BitmapSource 添加到编码器
                    encoder.Save(stream); // 保存 BitmapSource 到内存流
                    byte[] pngBytes = stream.ToArray(); // 将内存流转换为字节数组
                    // 用内容哈希命名
                    string hash = BitConverter.ToString(System.Security.Cryptography.SHA256.HashData(pngBytes)).Replace("-", "").ToLower();
                    string targetPath = Path.Combine(AppPathHelper.LocalIconsFolder, $"{hash}.png"); // 生成目标路径
                    if (!File.Exists(targetPath)) // 如果目标路径不存在
                    {
                        File.WriteAllBytes(targetPath, pngBytes); // 写入文件
                    }
                    return targetPath; // 返回目标路径
                }
            }
            catch
            {
                ShowToast("保存图标失败。", ToastType.Error); // 显示Toast
                return null; // 如果出现异常，返回 null
            }
        }

        /// <summary>
        /// 获取网站图标
        /// </summary>
        /// <param name="websiteUrl"> 网站地址 </param>
        /// <returns> 网站图标 </returns>
        public async Task<ImageSource> GetWebsiteIconAsync(string websiteUrl)
        {
            try
            {
                Uri uri = new(websiteUrl); // 创建 Uri 对象
                string apiFaviconUrl = $"https://icon.bqb.cool?url={uri.Host}"; // 拼接 API 地址
                byte[] iconData = await httpClient.GetByteArrayAsync(apiFaviconUrl);
                if (iconData == null || iconData.Length == 0) // 验证下载的数据是否为有效的图像
                {
                    ShowToast("获取网站图标失败：API返回数据为空。", ToastType.Error);
                    return null;
                }

                return LoadIconFromData(iconData); // 解码逻辑仍可以在 UI 线程上执行，如果耗时，可能需要进一步优化
            }
            catch (HttpRequestException ex)
            {
                ShowToast($"获取网站图标失败：网络请求错误 - {ex.Message}", ToastType.Error);
                return null;
            }
            catch (Exception ex) // 捕获所有未被 LoadIconFromData 处理的顶级异常
            {
                ShowToast($"获取网站图标失败：未知错误 - {ex.Message}", ToastType.Error);
                return null;
            }
        }

        /// <summary>
        /// 从字节数组中加载并解码图标
        /// </summary>
        /// <param name="iconData">图标的字节数据</param>
        /// <returns>ImageSource 对象</returns>
        private ImageSource LoadIconFromData(byte[] iconData)
        {
            // 尝试处理 SVG 格式 (增强容错) ---
            // SVG 文件以 '<' (0x3C) 开头。
            if (iconData[0] == 0x3C)
            {
                try
                {
                    string svgContent = Encoding.UTF8.GetString(iconData);

                    // SvgDocument.FromSvg 会捕获 "multiple root elements" 等错误，避免崩溃
                    var svgDocument = SvgDocument.FromSvg<SvgDocument>(svgContent);
                    return ConvertBitmapSourceToBitmapImage(RenderSvgToBitmapSource(svgDocument));
                }
                catch (Exception)
                {
                    // 捕获所有 SVG 解析失败的情况（包括 API 返回的错误页面、多根元素等）
                    ShowToast($"获取网站图标失败：解析 SVG 时出错 - API返回了非标准的SVG数据。", ToastType.Error);
                    return null;
                }
            }

            // 尝试处理 WebP 格式 (解决不支持的图像格式错误) ---
            // 检查 WebP 魔术数字：RIFF (0-3) + WEBP (8-11)
            if (iconData.Length >= 12 &&
                iconData[0] == 0x52 && iconData[1] == 0x49 && iconData[2] == 0x46 && iconData[3] == 0x46 &&
                iconData[8] == 0x57 && iconData[9] == 0x45 && iconData[10] == 0x42 && iconData[11] == 0x50)
            {
                // WARNING: WPF 默认不支持 WebP。
                ShowToast("获取网站图标失败：检测到 WebP 格式，但缺少解码器支持。", ToastType.Error);
                return null;
            }

            // 尝试处理原生 WPF 格式 (PNG, JPG, GIF, ICO, BMP) ---
            // 确保数据是已知的原生图像格式
            if (!IsValidImageData(iconData))
            {
                ShowToast("获取网站图标失败：无效的图像数据。", ToastType.Error);
                return null;
            }

            // 使用 BitmapImage 加载原生格式
            try
            {
                BitmapImage bitmapImage = new();
                using (MemoryStream stream = new(iconData))
                {
                    bitmapImage.BeginInit();
                    bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                    stream.Seek(0, SeekOrigin.Begin);
                    bitmapImage.StreamSource = stream;
                    // NotSupportedException 会在这里被抛出（如果格式原生不支持）
                    bitmapImage.EndInit();
                    bitmapImage.Freeze();
                }

                if (IsImageEmpty(bitmapImage))
                {
                    ShowToast("获取网站图标失败：图标为空或透明。", ToastType.Error);
                    return null;
                }
                return bitmapImage;
            }
            catch (NotSupportedException) // 捕获原生 BitmapImage 无法解码的格式错误
            {
                ShowToast($"获取网站图标失败：不支持的图像格式 - 未找到适用于完成此操作的图像处理组件。", ToastType.Error);
                return null;
            }
        }

        /// <summary>
        /// 验证图像数据是否为有效的图像格式
        /// </summary>
        /// <param name="imageData">图像数据</param>
        /// <returns>是否为有效的图像</returns>
        private static bool IsValidImageData(byte[] imageData)
        {
            if (imageData == null || imageData.Length == 0)
                return false;

            // 局部函数：用于检查字节序列是否匹配
            static bool CheckSignature(byte[] data, byte[] signature)
            {
                if (data.Length < signature.Length) return false;
                for (int i = 0; i < signature.Length; i++)
                {
                    if (data[i] != signature[i]) return false;
                }
                return true;
            }

            // 1. SVG/XML: 以 '<' (0x3C) 开头
            if (imageData[0] == 0x3C) return true;

            // 2. PNG: 89 50 4E 47 0D 0A 1A 0A
            if (CheckSignature(imageData, new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A })) return true;

            // 3. JPEG: FF D8 FF
            if (CheckSignature(imageData, new byte[] { 0xFF, 0xD8, 0xFF })) return true;

            // 4. GIF: 47 49 46 38 (GIF8)
            if (CheckSignature(imageData, new byte[] { 0x47, 0x49, 0x46, 0x38 })) return true;

            // 5. ICO: 00 00 01 00
            if (CheckSignature(imageData, new byte[] { 0x00, 0x00, 0x01, 0x00 })) return true;

            // 6. BMP: 42 4D (BM)
            if (CheckSignature(imageData, new byte[] { 0x42, 0x4D })) return true;

            return false;
        }

        /// <summary>
        /// 判断获取的网站图片是否为空图片
        /// </summary>
        /// <param name="bitmapImage"> 网站图标 </param>
        /// <returns> 是否为空图片 </returns>
        private bool IsImageEmpty(BitmapImage bitmapImage)
        {
            try
            {
                if (bitmapImage == null || bitmapImage.PixelWidth == 0 || bitmapImage.PixelHeight == 0)
                    return true; // 如果宽度、高度均为 0，则认为是空图片

                int stride = bitmapImage.PixelWidth * 4; // 计算每行像素的字节数
                byte[] pixels = new byte[bitmapImage.PixelHeight * stride]; // 创建字节数组
                FormatConvertedBitmap formatConvertedBitmap = new(); // 创建 FormatConvertedBitmap 对象
                formatConvertedBitmap.BeginInit(); // 开始初始化 FormatConvertedBitmap
                formatConvertedBitmap.Source = bitmapImage; // 设置 BitmapImage 作为源
                formatConvertedBitmap.DestinationFormat = PixelFormats.Pbgra32; // 转换格式
                formatConvertedBitmap.EndInit(); // 转换格式
                formatConvertedBitmap.CopyPixels(pixels, stride, 0); // 复制像素数据到字节数组
                for (int i = 0; i < pixels.Length; i += 4)
                {
                    if (pixels[i + 3] != 0) return false; // 如果有非透明像素，则不是空图片
                }
                return true; // 所有像素的 Alpha 值均为 0，则认为是空图片
            }
            catch
            {
                return true; // 处理异常情况，返回空图片
            }
        }

        /// <summary>
        /// 处理图片路径，返回 BitmapImage 或 BitmapFrame[]（动图）
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>处理后的 BitmapImage 或 BitmapFrame[]</returns>
        public object ProcessIcon(string filePath)
        {
            try
            {
                string extension = Path.GetExtension(filePath).ToLower(); // 获取文件扩展名
                switch (extension)
                {
                    case ".png":
                    case ".jpg":
                    case ".jpeg":
                    case ".bmp":
                        return LoadBitmapImage(filePath); // 加载普通图片文件
                    case ".ico":
                        return LoadBitmapImage(filePath); // ICO 也用BitmapImage加载，支持多尺寸
                    case ".gif":
                        return LoadGifImage(filePath); // 加载GIF，支持动图
                    case ".svg":
                        return LoadSvgToBitmapImage(filePath); // 加载 SVG 文件
                    case ".exe":
                        return ExtractIconFromExe(filePath); // 从 EXE 文件中提取图标
                    default:
                        throw new NotSupportedException($"不支持的文件格式: {extension}"); // 处理不支持的文件格式
                }
            }
            catch
            {
                ShowToast("处理图标失败。", ToastType.Error); // 显示Toast
                throw;
            }
        }

        /// <summary>
        /// 创建 BitmapImage 并加载文件
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>加载的 BitmapImage</returns>
        private static BitmapImage CreateBitmapImageFromFile(string filePath)
        {
            BitmapImage bi = new();
            bi.BeginInit();
            bi.CacheOption = BitmapCacheOption.OnLoad;
            bi.UriSource = new(filePath, UriKind.Absolute);
            bi.EndInit();
            bi.Freeze();
            return bi;
        }

        /// <summary>
        /// 加载普通图片文件（如 PNG、JPG、JPEG、BMP、ICO）
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>加载的 BitmapImage</returns>
        private static BitmapImage LoadBitmapImage(string filePath)
        {
            try
            {
                return CreateBitmapImageFromFile(filePath);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"加载图片时出错: {filePath}", ex);
            }
        }

        /// <summary>
        /// 加载 GIF 文件，支持动图，返回第一帧（BitmapImage）或所有帧（BitmapFrame[]）
        /// </summary>
        /// <param name="filePath">GIF 文件路径</param>
        /// <returns>BitmapImage（静态）或 BitmapFrame[]（动图）</returns>
        private object LoadGifImage(string filePath)
        {
            try
            {
                using (FileStream stream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    GifBitmapDecoder decoder = new(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                    if (decoder.Frames.Count == 1) // 静态GIF，直接返回BitmapImage
                    {
                        return CreateBitmapImageFromFile(filePath);
                    }
                    else // 动图，返回所有帧
                    {
                        return decoder.Frames.ToArray();
                    }
                }
            }
            catch (Exception ex)
            {
                ShowToast($"加载GIF图片时出错: {filePath}", ToastType.Error); // 显示Toast
            }
            return null;
        }

        /// <summary>
        /// 加载 SVG 文件并转换为 BitmapImage
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>加载的 BitmapImage</returns>
        public static BitmapImage LoadSvgToBitmapImage(string filePath)
        {
            try
            {
                string svgContent = File.ReadAllText(filePath);// 读取SVG文件内容
                var svgDocument = SvgDocument.FromSvg<SvgDocument>(svgContent);// 解析SVG内容为SvgDocument
                var bitmapSource = RenderSvgToBitmapSource(svgDocument);
                var bitmapImage = ConvertBitmapSourceToBitmapImage(bitmapSource);
                return bitmapImage;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"加载 SVG 文件时出错: {filePath}", ex);
            }
        }

        /// <summary>
        /// 将SVG文档渲染为BitmapSource图像
        /// </summary>
        /// <param name="svgDocument">要渲染的SVG文档</param>
        /// <returns>渲染后的BitmapSource图像</returns>
        private static BitmapSource RenderSvgToBitmapSource(Svg.SvgDocument svgDocument)
        {
            // 获取SVG文档的宽度和高度
            double width = svgDocument.Width.Value;
            double height = svgDocument.Height.Value;

            // 创建一个新的DrawingVisual对象用于绘制
            DrawingVisual visual = new();
            using (DrawingContext context = visual.RenderOpen())
            {
                using var bitmap = svgDocument.Draw(); // 使用SVG文档创建位图
                {
                    BitmapSource bitmapSource = Imaging.CreateBitmapSourceFromHBitmap(
                        bitmap.GetHbitmap(),  // 获取位图的HBitmap句柄
                        IntPtr.Zero,         // 默认调色板
                        Int32Rect.Empty,    // 使用整个源矩形
                        BitmapSizeOptions.FromEmptyOptions()  // 使用空选项
                    );

                    // 在绘制上下文中绘制图像
                    context.DrawImage(bitmapSource, new(new System.Windows.Point(0, 0), new System.Windows.Size(width, height)));
                }
            }

            // 创建渲染目标位图
            RenderTargetBitmap rtb = new(
                (int)width,       // 宽度
                (int)height,      // 高度
                96,               // 水平DPI
                96,               // 垂直DPI
                PixelFormats.Pbgra32  // 像素格式
            );

            rtb.Render(visual); // 将DrawingVisual渲染到RenderTargetBitmap
            rtb.Freeze(); // 冻结位图以提高性能
            return rtb;
        }

        /// <summary>
        /// 将BitmapSource转换为BitmapImage
        /// </summary>
        /// <param name="bitmapSource">BitmapSource</param>
        /// <returns>BitmapImage</returns>
        private static BitmapImage ConvertBitmapSourceToBitmapImage(BitmapSource bitmapSource)
        {
            BitmapImage bitmapImage = new(); // 创建 BitmapImage 对象
            using MemoryStream memoryStream = new(); // 创建内存流
            PngBitmapEncoder encoder = new(); // 创建 PNG 编码器
            encoder.Frames.Add(BitmapFrame.Create(bitmapSource)); // 将 BitmapSource 添加到编码器
            encoder.Save(memoryStream); // 保存 BitmapSource 到内存流
            bitmapImage.BeginInit(); // 开始初始化 BitmapImage
            bitmapImage.StreamSource = new MemoryStream(memoryStream.ToArray()); // 设置内存流为 BitmapImage 的源
            bitmapImage.EndInit(); // 结束初始化 BitmapImage
            return bitmapImage;
        }

        /// <summary>
        /// 直接从文件提取图标（避免压缩标记等叠加图标）
        /// </summary>
        /// <param name="filePath">文件路径（通常是 EXE 或 DLL）</param>
        /// <returns>提取的图标作为 ImageSource，如果失败则返回 null</returns>
        private static ImageSource ExtractIconDirectlyFromFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return null;

            IntPtr hIconLarge = IntPtr.Zero;
            IntPtr hIconSmall = IntPtr.Zero;
            try
            {
                // 使用 ExtractIconEx 直接从文件提取大图标（索引0通常是主图标）
                int iconCount = ExtractIconEx(filePath, 0, out hIconLarge, out hIconSmall, 1);
                if (iconCount > 0 && hIconLarge != IntPtr.Zero)
                {
                    ImageSource iconSource = ConvertHIconToImageSource(hIconLarge);
                    // ConvertHIconToImageSource 会复制图标数据，所以可以立即释放原始句柄
                    DestroyIcon(hIconLarge);
                    hIconLarge = IntPtr.Zero; // 标记已释放
                    if (hIconSmall != IntPtr.Zero)
                    {
                        DestroyIcon(hIconSmall);
                        hIconSmall = IntPtr.Zero; // 标记已释放
                    }
                    return iconSource;
                }

                // 如果 ExtractIconEx 失败，尝试使用 Icon.ExtractAssociatedIcon
                try
                {
                    Icon icon = Icon.ExtractAssociatedIcon(filePath);
                    if (icon != null)
                    {
                        ImageSource iconSource = ConvertIconToImageSource(icon);
                        icon.Dispose();
                        return iconSource;
                    }
                }
                catch { }
                return null;
            }
            catch
            {
                return null;
            }
            finally
            {
                // 清理资源（如果之前没有释放）
                if (hIconLarge != IntPtr.Zero) DestroyIcon(hIconLarge);
                if (hIconSmall != IntPtr.Zero) DestroyIcon(hIconSmall);
            }
        }

        /// <summary>
        /// 将 Icon 转换为 ImageSource
        /// </summary>
        /// <param name="icon">图标</param>
        /// <returns>转换后的 ImageSource</returns>
        private static ImageSource ConvertIconToImageSource(Icon icon)
        {
            if (icon == null) return null;
            try
            {
                return ConvertHIconToImageSource(icon.Handle);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 将 Icon 转换为 BitmapImage
        /// </summary>
        /// <param name="icon">图标</param>
        /// <returns>转换后的 BitmapImage</returns>
        private static BitmapImage ConvertIconToBitmapImage(Icon icon)
        {
            if (icon == null) return null;
            try
            {
                using (MemoryStream ms = new()) // 创建内存流
                {
                    icon.Save(ms); // 保存图标到内存流
                    ms.Seek(0, SeekOrigin.Begin); // 定位到流的开始位置
                    BitmapImage bi = new(); // 创建 BitmapImage 对象
                    bi.BeginInit(); // 开始初始化 BitmapImage
                    bi.CacheOption = BitmapCacheOption.OnLoad; // 设置缓存选项
                    bi.StreamSource = ms; // 设置内存流为 BitmapImage 的源
                    bi.EndInit(); // 结束初始化 BitmapImage
                    return bi; // 返回 BitmapImage
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("转换图标时出错", ex);
            }
        }

        /// <summary>
        /// 从 EXE 文件中提取图标
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>提取的图标作为 BitmapImage</returns>
        private BitmapImage ExtractIconFromExe(string filePath)
        {
            try
            {
                Icon icon = Icon.ExtractAssociatedIcon(filePath); // 提取图标
                return ConvertIconToBitmapImage(icon); // 转换为 BitmapImage
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"从 EXE 文件提取图标时出错: {filePath}", ex);
            }
        }

        // 手动释放资源
        public void Dispose()
        {
            GC.Collect(); // 强制垃圾回收
            GC.WaitForPendingFinalizers(); // 等待垃圾回收完成
            GC.Collect(); // 再次强制垃圾回收
        }

        /// <summary>
        /// 获取文件内容哈希值
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>文件内容哈希值</returns>
        public static string GetFileContentHash(string filePath)
        {
            try
            {
                using var sha256 = SHA256.Create(); // 创建 SHA256 哈希算法
                byte[] hash;
                if (Directory.Exists(filePath)) // 对于文件夹，使用路径字符串的哈希值
                {
                    var pathBytes = Encoding.UTF8.GetBytes(filePath); // 转换为字节数组
                    hash = sha256.ComputeHash(pathBytes); // 计算哈希值
                }
                else // 对于文件，使用文件内容的哈希值
                {
                    using var stream = File.OpenRead(filePath); // 打开文件流
                    hash = sha256.ComputeHash(stream); // 计算哈希值
                }
                return BitConverter.ToString(hash).Replace("-", "").ToLower(); // 返回哈希值字符串
            }
            catch // 如果出现异常，使用路径字符串的哈希值作为后备方案
            {
                using var sha256 = SHA256.Create(); // 创建 SHA256 哈希算法
                var pathBytes = Encoding.UTF8.GetBytes(filePath); // 转换为字节数组
                var hash = sha256.ComputeHash(pathBytes); // 计算哈希值
                return BitConverter.ToString(hash).Replace("-", "").ToLower(); // 返回哈希值字符串
            }
        }

        /// <summary>
        /// 保存图片文件到本地图标目录，文件名为内容哈希，避免重复
        /// </summary>
        /// <param name="filePath">图片文件路径</param>
        /// <returns>保存路径</returns>
        public string SaveImageToLocalIcons(string filePath)
        {
            string ext = Path.GetExtension(filePath).ToLower(); // 获取文件扩展名
            string hash = GetFileContentHash(filePath); // 获取文件内容哈希值
            string targetPath = Path.Combine(AppPathHelper.LocalIconsFolder, $"{hash}{ext}"); // 生成目标路径
            if (!File.Exists(targetPath)) // 如果目标路径不存在
            {
                File.Copy(filePath, targetPath); // 复制文件
            }
            return targetPath; // 返回目标路径
        }

        /// <summary>
        /// 加载背景图片（支持 GIF 动图、SVG 和普通图片），自动处理 WpfAnimatedGif 兼容性。
        /// </summary>
        /// <param name="imageControl">目标 Image 控件</param>
        /// <param name="path">图片路径</param>
        public void SetImageWithGifSupport(Image imageControl, string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            if (!File.Exists(path))
            {
                ShowToast("背景图片不存在！", ToastType.Error); // 显示Toast
            }
            try
            {
                string ext = Path.GetExtension(path).ToLower(); // 获取文件扩展名
                if (ext == ".gif")
                {
                    SetGifImage(imageControl, path); // 设置GIF动图
                }
                else if (ext == ".svg")
                {
                    SetSvgImage(imageControl, path); // 设置SVG图片
                }
                else
                {
                    SetNormalImage(imageControl, path); // 设置普通图片
                }
            }
            catch
            {
                ClearImage(imageControl); // 清空图片
                ShowToast("背景图片设置失败！", ToastType.Error); // 显示Toast
            }
        }

        /// <summary>
        /// 设置 GIF 动图到 Image 控件
        /// </summary>
        /// <param name="imageControl">目标 Image 控件</param>
        /// <param name="path">图片路径</param>
        private void SetGifImage(Image imageControl, string path)
        {
            var bitmap = CreateBitmapImageFromFile(path);
            ImageBehavior.SetAnimatedSource(imageControl, bitmap); // 设置动画源
        }

        /// <summary>
        /// 设置 SVG 图片到 Image 控件
        /// </summary>
        /// <param name="imageControl">目标 Image 控件</param>
        /// <param name="path">图片路径</param>
        private void SetSvgImage(Image imageControl, string path)
        {
            ImageBehavior.SetAnimatedSource(imageControl, null); // 设置动画源为空
            var svgBitmap = LoadSvgToBitmapImage(path); // 加载SVG图片
            imageControl.Source = svgBitmap; // 设置图片源
        }

        /// <summary>
        /// 设置普通图片到 Image 控件
        /// </summary>
        /// <param name="imageControl">目标 Image 控件</param>
        /// <param name="path">图片路径</param>
        private void SetNormalImage(Image imageControl, string path)
        {
            ImageBehavior.SetAnimatedSource(imageControl, null); // 设置动画源为空
            var bitmap = CreateBitmapImageFromFile(path);
            imageControl.Source = bitmap; // 设置图片源
        }

        /// <summary>
        /// 清空图片显示
        /// </summary>
        /// <param name="imageControl">目标 Image 控件</param>
        private void ClearImage(Image imageControl)
        {
            imageControl.Source = null; // 设置图片为空
            ImageBehavior.SetAnimatedSource(imageControl, null); // 设置动画源为空
        }

        /// <summary>
        /// 显示Toast
        /// </summary>
        /// <param name="message">消息</param>
        /// <param name="title">标题</param>
        private void ShowToast(string message, ToastType title = ToastType.Error)
        {
            using var toast = new ToastManager(); // 创建ToastManager对象
            toast.Show(message, title); // 显示Toast
        }

        /// <summary>
        /// 从快捷方式文件中获取 IconLocation（图标位置）
        /// </summary>
        /// <param name="linkFilePath">快捷方式文件路径</param>
        /// <returns>图标路径（可能带索引，如 "C:\app.exe,0"）</returns>
        private static string GetIconLocationFromLinkFile(string linkFilePath)
        {
            if (string.IsNullOrEmpty(linkFilePath) || !File.Exists(linkFilePath)) return null;
            IWshShortcut shortcut = null;
            WshShell shell = null;
            try
            {
                shell = new WshShell();
                shortcut = (IWshShortcut)shell.CreateShortcut(linkFilePath);
                string iconLoc = shortcut.IconLocation;
                return string.IsNullOrWhiteSpace(iconLoc) ? null : iconLoc;
            }
            catch
            {
                return null;
            }
            finally
            {
                if (shortcut != null) Marshal.ReleaseComObject(shortcut);
                if (shell != null) Marshal.ReleaseComObject(shell);
            }
        }

        /// <summary>
        /// 从指定文件及索引提取图标（直接读取 PE 资源，避免系统附加的快捷方式标记）
        /// </summary>
        private static ImageSource ExtractIconDirectlyFromFileWithIndex(string filePath, int iconIndex)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return null;

            IntPtr hIconLarge = IntPtr.Zero;
            IntPtr hIconSmall = IntPtr.Zero;
            try
            {
                int iconCount = ExtractIconEx(filePath, iconIndex, out hIconLarge, out hIconSmall, 1);
                if (iconCount > 0 && hIconLarge != IntPtr.Zero)
                {
                    return ConvertHIconToImageSource(hIconLarge);
                }
                return null;
            }
            catch
            {
                return null;
            }
            finally
            {
                if (hIconLarge != IntPtr.Zero) DestroyIcon(hIconLarge);
                if (hIconSmall != IntPtr.Zero) DestroyIcon(hIconSmall);
            }
        }

        /// <summary>
        /// 获取系统内置特殊文件夹的纯净图标（如桌面、下载，不受压缩等覆盖属性影响）
        /// </summary>
        private static ImageSource GetSpecialFolderIcon(string filePath)
        {
            try
            {
                string normalizedPath = Path.GetFullPath(filePath).TrimEnd('\\', '/');

                // 只识别下载文件夹（下载文件夹的 SIID_DOWNLOADS = 184 是正确的）
                string downloadsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads").TrimEnd('\\', '/');
                if (string.Equals(normalizedPath, downloadsPath, StringComparison.OrdinalIgnoreCase))
                {
                    SHSTOCKICONINFO info = new();
                    info.cbSize = (uint)Marshal.SizeOf(info);
                    if (SHGetStockIconInfo(SIID_DOWNLOADS, SHGSI_ICON | SHGSI_LARGEICON, ref info) == 0 && info.hIcon != IntPtr.Zero)
                    {
                        ImageSource icon = ConvertHIconToImageSource(info.hIcon);
                        DestroyIcon(info.hIcon);
                        return icon;
                    }
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// 获取用户自定义的文件夹图标（通过解析 desktop.ini，避免系统叠加压缩标记）
        /// </summary>
        private static ImageSource GetFolderCustomIcon(string folderPath)
        {
            try
            {
                string desktopIniPath = Path.Combine(folderPath, "desktop.ini");
                if (File.Exists(desktopIniPath))
                {
                    string[] lines = File.ReadAllLines(desktopIniPath);
                    string iconResource = null;
                    foreach (var line in lines)
                    {
                        if (line.StartsWith("IconResource=", StringComparison.OrdinalIgnoreCase))
                        {
                            iconResource = line.Substring("IconResource=".Length).Trim();
                            break;
                        }
                    }

                    if (!string.IsNullOrEmpty(iconResource))
                    {
                        string iconPath = iconResource;
                        int iconIndex = 0;

                        // 解析路径中的索引，例如 "C:\path\app.exe,0"
                        int commaIndex = iconResource.LastIndexOf(',');
                        if (commaIndex > 0)
                        {
                            iconPath = iconResource.Substring(0, commaIndex).Trim();
                            if (int.TryParse(iconResource.Substring(commaIndex + 1).Trim(), out int index))
                                iconIndex = index;
                        }

                        iconPath = iconPath.Trim('"');
                        iconPath = Environment.ExpandEnvironmentVariables(iconPath);

                        // 处理相对路径
                        if (!Path.IsPathRooted(iconPath))
                            iconPath = Path.Combine(folderPath, iconPath);

                        if (File.Exists(iconPath))
                        {
                            if (IsExecutableFile(iconPath))
                            {
                                return ExtractIconDirectlyFromFileWithIndex(iconPath, iconIndex);
                            }
                            else if (iconPath.EndsWith(".ico", StringComparison.OrdinalIgnoreCase))
                            {
                                return LoadBitmapImage(iconPath);
                            }
                            else
                            {
                                return GetIconFromImageList(iconPath) ?? GetIconFromPath(iconPath);
                            }
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// 获取纯净的标准文件夹图标（强制忽略 NTFS 压缩、共享等属性，不带任何覆盖箭头）
        /// </summary>
        private static ImageSource GetStandardFolderIcon(string folderPath)
        {
            SHFILEINFO shfi = new();
            uint flags = SHGFI_ICON | SHGFI_LARGEICON | SHGFI_USEFILEATTRIBUTES;

            IntPtr hIcon = SHGetFileInfo(
                folderPath,
                FILE_ATTRIBUTE_DIRECTORY, // 强制视作普通文件夹
                out shfi,
                (uint)Marshal.SizeOf(shfi),
                flags
            );

            if (hIcon != IntPtr.Zero && shfi.hIcon != IntPtr.Zero)
            {
                try
                {
                    ImageSource iconSource = ConvertHIconToImageSource(shfi.hIcon);
                    DestroyIcon(shfi.hIcon);
                    return iconSource;
                }
                catch
                {
                    if (shfi.hIcon != IntPtr.Zero) DestroyIcon(shfi.hIcon);
                    return null;
                }
            }
            return null;
        }
    }
}