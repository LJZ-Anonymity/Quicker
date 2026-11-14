using System.Collections.Generic;
using System;

namespace Quicker.Helpers
{
    /// <summary>
    /// 域名 / URL 解析相关公共工具：TLD 后缀表、根域名提取。
    /// </summary>
    public static class DomainHelper
    {
        /// <summary>
        /// 常见顶级域名（不含前导点，忽略大小写）。
        /// </summary>
        private static readonly HashSet<string> KnownTlds = new(StringComparer.OrdinalIgnoreCase)
        {
            "com", "cn", "net", "org", "gov", "edu", "io", "co", "me", "info", "biz",
            "tv", "cc", "us", "uk", "jp", "kr", "de", "fr", "ru", "in", "au", "ca",
            "br", "it", "es", "nl", "se", "no", "fi", "dk", "pl", "ch", "at", "be",
            "pt", "gr", "cz", "hu", "ro", "sk", "bg", "hr", "si", "ee", "lv", "lt",
            "lu", "mt", "cy", "hk", "tw", "mo", "sg", "my", "id", "th", "vn", "ph",
            "nz", "za", "mx", "ar", "app", "dev", "ai", "cloud", "xyz", "top",
            "site", "online", "tech", "store", "shop", "vip", "club", "fun",
            "live", "pro", "asia"
        };

        /// <summary>
        /// 常见二级公共后缀（如 com.cn、co.uk），忽略大小写。
        /// </summary>
        private static readonly HashSet<string> KnownSecondLevelSuffixes = new(StringComparer.OrdinalIgnoreCase)
        {
            "com.cn", "net.cn", "org.cn", "gov.cn", "edu.cn", "ac.cn",
            "com.hk", "com.tw", "com.mo", "com.sg", "com.my",
            "com.au", "net.au", "org.au",
            "com.br", "com.mx", "com.ar",
            "co.uk", "org.uk", "ac.uk", "gov.uk",
            "co.jp", "ne.jp", "or.jp",
            "co.kr", "or.kr",
            "co.nz", "co.in", "co.za", "co.id", "co.th", "co.il",
            "com.vn", "com.ph"
        };

        /// <summary>
        /// 检查主机名是否以已知的顶级域名后缀结尾（含二级后缀）。
        /// </summary>
        public static bool EndsWithKnownTld(string host)
        {
            if (string.IsNullOrWhiteSpace(host)) return false;

            // 先匹配更具体的二级后缀（com.cn / co.uk 等）
            foreach (var suffix in KnownSecondLevelSuffixes)
            {
                if (host.EndsWith("." + suffix, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            // 再匹配最后一段是否为已知一级后缀
            int lastDot = host.LastIndexOf('.');
            if (lastDot < 0 || lastDot == host.Length - 1) return false;
            return KnownTlds.Contains(host[(lastDot + 1)..]);
        }

        /// <summary>
        /// 从 URL 中提取网站根域名名称（自动剥离协议、子域名和公共后缀）。
        /// 例：https://cn.example.com -> example
        /// </summary>
        public static string GetWebsiteNameFromUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return url;

            Uri uri;
            try
            {
                string trimmed = url.Trim();
                if (!trimmed.Contains("://", StringComparison.Ordinal))
                    trimmed = "https://" + trimmed.TrimStart('/');
                uri = new Uri(trimmed);
            }
            catch
            {
                return url;
            }

            if (uri.HostNameType == UriHostNameType.IPv4 || uri.HostNameType == UriHostNameType.IPv6)
                return uri.Host;

            string host = uri.Host;
            if (string.IsNullOrWhiteSpace(host)) return url;

            string[] parts = host.Split('.', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length <= 1) return host;

            int suffixLength = 1;
            if (parts.Length >= 2 && KnownSecondLevelSuffixes.Contains(parts[^2] + "." + parts[^1]))
                suffixLength = 2;

            int bodyCount = parts.Length - suffixLength;
            if (bodyCount <= 0) return host;

            return parts[bodyCount - 1];
        }
    }
}