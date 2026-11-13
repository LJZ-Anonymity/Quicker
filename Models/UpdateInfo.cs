namespace Quicker.Models
{
    // 更新信息类
    public class UpdateInfo
    {
        public string Version { get; set; } // 版本号
        public string DownloadUrl { get; set; } // 下载地址
        public string DownloadUrlWithNet { get; set; } // 下载地址（内置.NET 运行时）
        public string Changelog { get; set; } // 更新日志
        public string ReleaseDate { get; set; } // 发布日期
        public bool IsLatest { get; set; } // 是否为最新版本
    }
}