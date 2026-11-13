using System.Threading.Tasks;
using Quicker.Database.Core;
using Quicker.Managers;
using System.Text.Json;
using Quicker.Helpers;
using System.Net.Http;
using Quicker.Models;
using System.Net;
using System.IO;

namespace Quicker.Managers
{
    public class AppUpdateManager : IDisposable
    {
        private const string UpdateInfoUrl = "https://raw.githubusercontent.com/LJZ-Anonymity/Quicker/Quicker/VersionInfo.json"; // 更新信息的 URL 地址
        private static readonly HttpClient httpClient = new(new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate, // 启用压缩
            MaxConnectionsPerServer = 2, // 限制每个服务器的连接数
            UseProxy = false // 禁用代理以提高性能
        })
        {
            Timeout = TimeSpan.FromSeconds(30) // 增加超时时间到30秒
        }; // 静态 HttpClient 实例
        public List<UpdateInfo> Versions { get; private set; } = new(); // 版本列表
        private bool isDisposed = false; // 是否已释放资源

        // 同步检查更新
        public void CheckForUpdate()
        {
            ReadJsonFromUrl(); // 读取 JSON 数据
            using var toast = new ToastManager(); // 弹窗管理器
            if (Versions.Count > 0)  // 如果有版本信息
            {
                var latestVersion = GetLatestVersion(); // 获取最新版本信息
                if (latestVersion != null && VersionHelper.IsNewVersionAvailable(latestVersion.Version))
                {
                    AppStateManager.HasNewVersion = true; // 设置有新版本
                    toast.Show($"发现新版本：{latestVersion.Version}", ToastType.Common); // 弹窗提示
                }
            }
        }

        /// <summary>
        /// 获取最新版本信息
        /// </summary>
        /// <returns>最新版本信息</returns>
        public UpdateInfo GetLatestVersion()
        {
            if (Versions.Count == 0)
                return null;

            return Versions.FirstOrDefault(v => v.IsLatest) ??
                   Versions.OrderByDescending(v => v.Version).First();
        }

        /// <summary>
        /// 获取版本历史记录
        /// </summary>
        /// <param name="count">返回的版本数量，默认返回所有版本</param>
        /// <returns>版本历史记录</returns>
        public List<UpdateInfo> GetVersionHistory(int count = 0)
        {
            if (Versions.Count == 0)
                return new List<UpdateInfo>();

            var versions = Versions.OrderByDescending(v => v.Version).ToList();
            return count > 0 && count < versions.Count ? versions.Take(count).ToList() : versions;
        }

        // 同步读取 JSON 数据
        public void ReadJsonFromUrl()
        {
            using var toast = new ToastManager(); // 创建一次ToastManager实例
            try
            {
                string jsonResponse = httpClient.GetStringAsync(UpdateInfoUrl).GetAwaiter().GetResult(); // 使用 HttpClient 获取 JSON 数据
                var container = JsonSerializer.Deserialize<UpdateInfoContainer>(jsonResponse); // 反序列化 JSON 数据
                Versions = container?.Versions ?? new();
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException) // 处理超时异常
            {
                Versions = new();
                toast.Show("检查更新失败：网络连接超时，请检查网络连接。", ToastType.Error);
            }
            catch (HttpRequestException) // 处理HTTP请求异常
            {
                Versions = new();
                toast.Show($"检查更新失败：网络请求失败！", ToastType.Error);
            }
            catch (Exception ex) // 处理其他异常
            {
                Versions = new();
                toast.Show($"检查更新失败：{ex.Message}", ToastType.Error);
            }
        }

        // 释放资源
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this); // 告知垃圾回收器不需要调用终结器
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        /// <param name="disposing"> 是否释放托管资源 </param>
        protected virtual void Dispose(bool disposing)
        {
            if (!isDisposed)
            {
                Versions?.Clear(); // 清空版本列表
                isDisposed = true;
            }
        }

        ~AppUpdateManager()
        {
            Dispose(false);
        }

        // 更新信息容器类（用于JSON反序列化）
        private class UpdateInfoContainer
        {
            public List<UpdateInfo> Versions { get; set; } = new(); // 版本列表
        }
    }
}