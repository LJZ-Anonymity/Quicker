using System.Text.RegularExpressions;
using Quicker.Windows.MainWindows;
using System.Windows.Resources;
using System.Windows.Controls;
using Quicker.Database.Core;
using Quicker.UserControls;
using System.Windows.Input;
using System.Diagnostics;
using Quicker.Managers;
using System.Text.Json;
using Quicker.Helpers;
using System.Net.Http;
using System.Windows;
using System.Text;
using System.IO;

namespace Quicker.UserControls.SettingWindow.BasicSettings
{
    public partial class AboutQuickerGrid : UserControl
    {
        private WeakReference<Quicker.Windows.MainWindows.SettingWindow> weakSettingWindow; // 弱引用设置窗口
        SettingManager settingManager; // 读取设置的管理器

        public AboutQuickerGrid(Quicker.Windows.MainWindows.SettingWindow settingWindow)
        {
            InitializeComponent();
            
            // 注册编码提供程序，支持GBK等中文编码
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            
            settingManager = settingWindow._settingManager; // 创建设置管理器
            weakSettingWindow = new(settingWindow); // 保存设置窗口
            VersionLabel.Content = $"版本：{AppVersionHelper.CurrentVersion}"; // 加载版本信息

            RefreshPathsAndEnsureDirectories(); // 初始化路径与目录
            LoadQQUserInfo(); // 加载QQ用户信息
        }

        // 基础设置-关于Quicker-关于Quicker
        private void AboutQuickerButton_Click(object sender, RoutedEventArgs e)
        {
            settingManager.SetGridVisible(AboutQuickerButtonGrid, MainGrid); // 设置Grid可见性
            settingManager.ButtonStyle3_Click(AboutQuickerButton, MainGrid); // 保存Button类型3边框设置
        }

        // 打开更新历史文件（从资源型JSON反序列化）
        private void OpenUpdateHistory(object sender, MouseButtonEventArgs e)
        {
            const string resourceName = "VersionInfo.json"; // 资源文件名
            Uri resourceUri = new(resourceName, UriKind.Relative); // 资源URI
            StreamResourceInfo streamInfo = Application.GetResourceStream(resourceUri); // 通过资源URI获取资源流
            using (StreamReader reader = new(streamInfo.Stream)) // 读取资源流内容
            {
                string json = reader.ReadToEnd(); // 读取JSON字符串
                var versionInfo = JsonSerializer.Deserialize<VersionInfoRoot>(json); // 反序列化为对象
                StringBuilder sb = new();
                foreach (var v in versionInfo.Versions) // 遍历所有版本信息，拼接为文本
                {
                    sb.AppendLine($"{v.Version}\t{v.ReleaseDate}"); // 版本号和发布日期
                    var changelogs = v.Changelog.Split('\n'); // 按行分割变更日志
                    foreach (var log in changelogs)
                    {
                        sb.AppendLine(log.Trim('~', '.', '\r')); // 去除前缀符号后写入
                    }
                    sb.AppendLine(); // 每个版本之间空一行
                }

                // 写入临时文件并用记事本打开
                string tempPath = Path.GetTempPath(); // 获取临时文件夹路径
                string tempFilePath = Path.Combine(tempPath, "更新历史.txt"); // 构造临时文件路径
                File.WriteAllText(tempFilePath, sb.ToString()); // 写入临时文件
                System.Diagnostics.Process.Start("notepad.exe", tempFilePath); // 用记事本打开临时文件
            }
        }

        /// <summary>
        /// 在默认浏览器中打开URL
        /// </summary>
        /// <param name="url"> URL </param>
        private void OpenUrlInDefaultBrowser(string url)
        {
            using var actionManager = new ActionManager(); // 创建 ActionManager 的实例
            actionManager.LaunchDefaultBrowser(url); // 在默认浏览器中打开URL
        }

        // 前往图标网站www.iconfont.cn
        private void www_iconfont_cn_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            OpenUrlInDefaultBrowser("https://www.iconfont.cn"); // 在默认浏览器中打开URL
        }

        // 前往图标网站icons8.com
        private void icons8_com_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            OpenUrlInDefaultBrowser("https://icons8.com/"); // 在默认浏览器中打开URL
        }

        // 前往图标网站fontawesome.com
        private void fontawesome_com_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            OpenUrlInDefaultBrowser("https://fontawesome.com/"); // 在默认浏览器中打开URL
        }

        // 前往icon11社区图标库
        private void icon11_community_github_io_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            OpenUrlInDefaultBrowser("https://icon11-community.github.io/icons/"); // 在默认浏览器中打开URL
        }

        // BUG反馈、需求
        private void FeedBack_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            OpenUrlInDefaultBrowser("https://github.com/LJZ-Anonymity/Quicker/issues"); // 在默认浏览器中打开URL
        }

        // 入正
        private void getquicker_net_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            OpenUrlInDefaultBrowser("https://getquicker.net/"); // 在默认浏览器中打开URL
        }

        // 基础设置-关于Quicker-隐私声明
        public void Privacy_StatementButton_Click(object sender, RoutedEventArgs e)
        {
            settingManager.SetGridVisible(Privacy_StatementButtonGrid, MainGrid); // 设置Grid可见性
            settingManager.ButtonStyle3_Click(Privacy_StatementButton, MainGrid); // 保存Button类型3边框设置
        }

        /// <summary>
        /// 打开指定文件夹
        /// </summary>
        /// <param name="folderPath"> 文件夹路径 </param>
        private void OpenFolder(string folderPath)
        {
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }
            Process.Start(new ProcessStartInfo(folderPath) { UseShellExecute = true });
        }

        // 前往程序数据根目录
        private void RootFolderPath_MouseDown(object sender, MouseButtonEventArgs e)
        {
            OpenFolder(AppPathHelper.AppDataRoot);
        }

        // 前往程序数据库目录
        private void DatabaseFolderPath_MouseDown(object sender, MouseButtonEventArgs e)
        {
            OpenFolder(AppPathHelper.DatabaseFolder);
        }

        // 前往程序图标目录
        private void LocalIconsFolderPath_MouseDown(object sender, MouseButtonEventArgs e)
        {
            OpenFolder(AppPathHelper.LocalIconsFolder);
        }

        // 备份数据
        private void BackupDataButton_Click(object sender, RoutedEventArgs e)
        {
            using var folderDialog = new System.Windows.Forms.FolderBrowserDialog() { Description = "选择备份路径", UseDescriptionForTitle = true };
            if (folderDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string folderPath = folderDialog.SelectedPath;
                string sourceFolderPath = AppPathHelper.AppDataRoot;
                string backupFolderPath = Path.Combine(folderPath, "QuickerData");
                if (!Directory.Exists(backupFolderPath)) Directory.CreateDirectory(backupFolderPath);
                CopyFolder(sourceFolderPath, backupFolderPath);
                using var toast = new ToastManager();
                toast.Show("操作完成！", ToastType.Success);
                OpenFolder(folderPath);
            }
        }

        /// <summary>
        /// 复制文件夹内容到目标路径
        /// </summary>
        /// <param name="sourceFolder"> 源文件夹路径 </param>
        /// <param name="targetFolder"> 目标文件夹路径 </param>
        private void CopyFolder(string sourceFolder, string targetFolder)
        {
            try
            {
                Directory.CreateDirectory(targetFolder); // 创建目标文件夹
                string[] files = Directory.GetFiles(sourceFolder); // 获取源文件夹中的所有文件
                foreach (string file in files) // 遍历并复制每个文件
                {
                    string name = Path.GetFileName(file); // 获取文件名
                    string dest = Path.Combine(targetFolder, name); // 构造目标文件路径
                    File.Copy(file, dest, true); // 复制文件并覆盖已存在的文件
                }

                string[] folders = Directory.GetDirectories(sourceFolder); // 获取源文件夹中的所有子文件夹
                foreach (string folder in folders) // 遍历并递归复制每个子文件夹
                {
                    string name = Path.GetFileName(folder); // 获取子文件夹名
                    string dest = Path.Combine(targetFolder, name); // 构造目标文件夹路径
                    CopyFolder(folder, dest); // 递归复制子文件夹
                }
            }
            catch
            {
                using var toast = new ToastManager(); // 创建 ToastManager 的实例
                toast.Show("备份失败！", ToastType.Error); // 显示提示
            }
        }

        // 获取临时数据大小
        private async void GetTempDataSize()
        {
            TempSize.Text = await GetTempDataSizeAsync();
        }

        // 获取临时数据大小（异步）
        public async Task<string> GetTempDataSizeAsync()
        {
            string tempDataPath = AppPathHelper.TempDataFolder;
            if (Directory.Exists(tempDataPath)) // 检查文件夹是否存在
            {
                try
                {
                    long folderSize = 0;
                    string folderSizeString = "B"; // 默认单位为字节
                    DirectoryInfo directoryInfo = new DirectoryInfo(tempDataPath); // 创建 DirectoryInfo 对象
                    var files = directoryInfo.EnumerateFiles("*", SearchOption.AllDirectories); // 获取所有文件（包括子文件夹）                 
                    foreach (var file in files) // 遍历所有文件并累加大小
                    {
                        folderSize += file.Length;
                    }

                    // 使用 DataConversionManager 转换数据大小和单位
                    using var convertionManager = new DataSizeHelper();
                    folderSize = convertionManager.ConversionData((int)folderSize); // 转换数据大小
                    folderSizeString = convertionManager.ConversionUnits((int)folderSize); // 转换单位

                    if (folderSize > 0)
                        CleanTempDataButton.IsEnabled = true; // 启用清理按钮
                    return $"{folderSize} {folderSizeString}"; // 返回数据大小和单位
                }
                catch{}
            }
            return "0 B"; // 返回0字节
        }

        // 清理临时数据
        private void CleanTempDataButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string tempDataPath = AppPathHelper.TempDataFolder;
                if (Directory.Exists(tempDataPath))
                {
                    Directory.Delete(tempDataPath, true); // 递归删除整个文件夹
                }
                Directory.CreateDirectory(tempDataPath); // 重新创建空文件夹
                using var toastSuccess = new ToastManager();
                toastSuccess.Show("清理完成！", ToastType.Success);
            }
            catch (Exception ex)
            {
                using var toastError = new ToastManager();
                toastError.Show("清理失败：" + ex.Message, ToastType.Error);
            }
        }

        // 控件关闭释放资源
        private void AboutQuickerGrid_Unloaded(object sender, RoutedEventArgs e)
        {
            MainGrid.Children.Clear(); // 清理UI元素

            // 清理事件处理程序
            VersionLabel.Content = string.Empty; // 清理文本内容
            TempSize.Text = null; // 清理文本内容
        }

        // 点击按钮修改配置文件，更改数据文件夹路径
        private void ChangeDataFolderButton_Click(object sender, RoutedEventArgs e)
        {
            using var folderDialog = new System.Windows.Forms.FolderBrowserDialog() { Description = "选择新的数据根目录", UseDescriptionForTitle = true };
            if (folderDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string selectedPath = folderDialog.SelectedPath;
                string oldRoot = AppPathHelper.AppDataRoot; // 切换前旧根目录
                using var toast = new ToastManager();
                if (AppPathHelper.TrySetAppDataRoot(selectedPath, out var resolved))
                {
                    string newRoot = resolved ?? AppPathHelper.AppDataRoot;
                    HandleMigrationIfNeeded(oldRoot, newRoot, toast); // 根据用户选择执行数据迁移与可选删除旧目录

                    // 迁移数据库中路径引用
                    try
                    {
                        using var buttonDb = new ButtonDatabase();
                        buttonDb.MigrateImagePathRoot(oldRoot, newRoot);
                        SettingDatabase.MigrateAppearanceBackgroundImagePathRoot(oldRoot, newRoot);
                    }
                    catch { }

                    RefreshPathsAndEnsureDirectories(); // 刷新路径展示并确保目录存在，同时更新临时大小
                    toast.Show($"设置成功！", ToastType.Success);
                }
                else
                {
                    toast.Show("设置失败，请选择有效路径或检查权限。", ToastType.Error);
                }
            }
        }

        /// <summary>
        /// 刷新路径展示并确保目录存在，同时更新临时大小
        /// </summary>
        private void RefreshPathsAndEnsureDirectories()
        {
            AppPathHelper.EnsureAllDirectoriesExist(); // 确保所有目录存在
            RootFolderPath.Text = AppPathHelper.AppDataRoot; // 设置根文件夹路径
            DatabaseFolderPath.Text = AppPathHelper.DatabaseFolder; // 设置数据库文件夹路径
            LocalIconsFolderPath.Text = AppPathHelper.LocalIconsFolder; // 设置图标文件夹路径
            GetTempDataSize(); // 获取临时数据大小
        }

        /// <summary>
        /// 根据用户选择执行数据迁移与可选删除旧目录。
        /// </summary>
        /// <param name="oldRoot">旧根目录</param>
        /// <param name="newRoot">新根目录</param>
        /// <param name="toast">ToastManager 实例</param>
        private void HandleMigrationIfNeeded(string oldRoot, string newRoot, ToastManager toast)
        {
            if (IsSamePath(oldRoot, newRoot))
            {
                return;
            }

            if (IsSubdirectory(oldRoot, newRoot))
            {
                toast.Show("新目录位于旧目录内部，已取消自动迁移。", ToastType.Warning);
                return;
            }

            var migrate = MessageBox.Show(
                $"是否将现有数据从:\n{oldRoot}\n迁移(覆盖)到:\n{newRoot}?",
                "迁移数据",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (migrate == MessageBoxResult.Yes)
            {
                try
                {
                    CopyFolder(oldRoot, newRoot);
                    var deleteOld = MessageBox.Show(
                        $"迁移完成。是否删除旧数据目录？\n{oldRoot}",
                        "删除旧数据",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);
                    if (deleteOld == MessageBoxResult.Yes)
                    {
                        TryDeleteDirectory(oldRoot, toast);
                    }
                }
                catch
                {
                    toast.Show("迁移失败，请检查磁盘空间与权限。", ToastType.Error);
                }
            }
        }

        /// <summary>
        /// 判断路径是否相同（忽略大小写，规范化）
        /// </summary>
        /// <param name="pathA">路径A</param>
        /// <param name="pathB">路径B</param>
        /// <returns>是否相同</returns>
        private static bool IsSamePath(string pathA, string pathB)
        {
            try
            {
                var a = Path.GetFullPath(pathA).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar); // 规范化路径A
                var b = Path.GetFullPath(pathB).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar); // 规范化路径B
                return string.Equals(a, b, StringComparison.OrdinalIgnoreCase); // 判断是否相同
            }
            catch { return false; }
        }

        /// <summary>
        /// 判断 child 是否位于 parent 子目录中
        /// </summary>
        /// <param name="parent">父目录路径</param>
        /// <param name="child">子目录路径</param>
        /// <returns>是否为子目录</returns>
        private static bool IsSubdirectory(string parent, string child)
        {
            try
            {
                var parentFull = Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar; // 规范化父目录路径
                var childFull = Path.GetFullPath(child).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar; // 规范化子目录路径
                return childFull.StartsWith(parentFull, StringComparison.OrdinalIgnoreCase); // 判断是否为子目录
            }
            catch { return false; }
        }

        /// <summary>
        /// 尝试删除目录
        /// </summary>
        /// <param name="folderPath">目录路径</param>
        /// <param name="toast">ToastManager 实例</param>
        private static void TryDeleteDirectory(string folderPath, ToastManager toast)
        {
            try
            {
                Directory.Delete(folderPath, true);
            }
            catch (DirectoryNotFoundException){ /* 目录不存在，忽略 */ }
            catch
            {
                toast.Show("删除旧数据目录失败，请手动清理。", ToastType.Warning);
            }
        }

        /// <summary>
        /// 加载QQ用户信息
        /// </summary>
        private void LoadQQUserInfo()
        {
            var teamMembers = new[]
            {
                (zhuzi, "331433038", "Anonymity"),
                (mj, "2260995976", "墨分璃"),
                (luang_sang, "2574357344", "巍巍噻")
            }; // 开发团队成员信息

            // 后台异步获取真实昵称，不阻塞UI
            _ = Task.Run(async () =>
            {
                var tasks = teamMembers.Select(async member =>
                {
                    try
                    {
                        string qqNickname = await GetQQNicknameAsync(member.Item2); // 获取QQ用户名
                        //Debug.WriteLine($"QQ {member.Item2} 获取结果: {qqNickname}");
                        if (!string.IsNullOrEmpty(qqNickname) && qqNickname != member.Item3)
                        {
                            Dispatcher.Invoke(() =>
                            {
                                string currentContent = member.Item1.Content.ToString(); // 获取当前昵称
                                member.Item1.Content = currentContent.Replace($"@{member.Item3}", $"@{qqNickname}"); // 替换昵称
                            });
                        }
                    }
                    catch { }
                });

                await Task.WhenAll(tasks);
            });
        }

        /// <summary>
        /// 异步获取QQ昵称（使用新API）
        /// </summary>
        /// <param name="qqNumber">QQ号</param>
        /// <returns>QQ昵称</returns>
        public async Task<string> GetQQNicknameAsync(string qqNumber)
        {
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(10);
            using var toast = new ToastManager();
            try
            {
                // 调用新API端点
                var response = await httpClient.GetAsync($"https://uapis.cn/api/v1/social/qq/userinfo?qq={qqNumber}");

                // 解析JSON响应
                var jsonResponse = await response.Content.ReadAsStringAsync();
                return ParseNicknameFromJsonResponse(jsonResponse);
            }
            catch (Exception ex)
            {
                toast.Show($"获取QQ昵称异常: {ex.Message}", ToastType.Error);
                return null;
            }
        }

        /// <summary>
        /// 从JSON响应中解析昵称
        /// </summary>
        /// <param name="jsonResponse">JSON响应</param>
        /// <returns>QQ昵称</returns>
        private static string ParseNicknameFromJsonResponse(string jsonResponse)
        {
            using var toast = new ToastManager();
            try
            {
                using var doc = JsonDocument.Parse(jsonResponse);
                var root = doc.RootElement;

                // 根据新的响应格式解析字段
                if (root.TryGetProperty("nickname", out var nicknameElement) &&
                    nicknameElement.ValueKind == JsonValueKind.String)
                {
                    return nicknameElement.GetString();
                }

                // 检查错误响应
                if (root.TryGetProperty("code", out var errorCode) &&
                    root.TryGetProperty("message", out var errorMessage))
                {
                    toast.Show($"API返回错误: {errorCode} - {errorMessage}", ToastType.Error);
                }

                return null;
            }
            catch (JsonException ex)
            {
                toast.Show($"JSON解析失败: {ex.Message}", ToastType.Error);
                return null;
            }
        }

        // 查看开源组件与协议
        private void ViewOpenSourceButton_Click(object sender, RoutedEventArgs e)
        {
            const string resourceName = "ComponentLicenses.txt"; // 资源文件名
            Uri resourceUri = new(resourceName, UriKind.Relative); // 资源URI
            StreamResourceInfo streamInfo = Application.GetResourceStream(resourceUri); // 通过资源URI获取资源流
            using (StreamReader reader = new(streamInfo.Stream)) // 读取资源流内容
            {
                string content = reader.ReadToEnd(); // 读取文件内容
                string tempPath = Path.Combine(Path.GetTempPath(), resourceName); // 构造临时文件路径
                File.WriteAllText(tempPath, content); // 写入临时文件
                System.Diagnostics.Process.Start("notepad.exe", tempPath); // 用记事本打开临时文件
            }
        }
    }

    /// <summary>
    /// 版本信息根对象，对应VersionInfo.json的结构
    /// </summary>
    public class VersionInfoRoot
    {
        public string LatestVersion { get; set; }// 最新版本号
        public List<VersionItem> Versions { get; set; }// 所有版本的详细信息列表
    }
    /// <summary>
    /// 单个版本的信息
    /// </summary>
    public class VersionItem
    {
        public string Version { get; set; }// 版本号
        public string DownloadUrl { get; set; }// 下载地址
        public string DownloadUrlWithNet { get; set; }// 备用下载地址
        public string Changelog { get; set; }// 变更日志（多行字符串）
        public string ReleaseDate { get; set; }// 发布日期
        public bool IsLatest { get; set; }// 是否为最新版本
    }
}