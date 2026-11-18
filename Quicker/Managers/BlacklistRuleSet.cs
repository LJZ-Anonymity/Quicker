using Quicker.Helpers;
using Quicker.Models.Settings;

namespace Quicker.Managers
{
    /// <summary>
    /// 黑白名单的内存规则集：加载设置时一次性构建，之后只读。
    /// 重建时生成新的实例再整体替换引用即可，判断线程无需加锁。
    /// </summary>
    public sealed class BlacklistRuleSet
    {
        public static readonly BlacklistRuleSet Empty = new(Array.Empty<BlacklistRule>());

        private readonly HashSet<string> _blackProcesses = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _whiteProcesses = new(StringComparer.OrdinalIgnoreCase);
        private readonly string[] _blackFolders;
        private readonly string[] _whiteFolders;
        private readonly bool _blackUnknown;
        private readonly bool _whiteUnknown;

        public BlacklistRuleSet(IEnumerable<BlacklistRule> rules)
        {
            var blackFolders = new List<string>();
            var whiteFolders = new List<string>();

            foreach (var rule in rules)
            {
                bool isBlack = rule.ListType == BlacklistListType.Blacklist;
                switch (rule.MatchType)
                {
                    case BlacklistMatchType.ProcessName:
                        (isBlack ? _blackProcesses : _whiteProcesses).Add(rule.Target);
                        break;

                    case BlacklistMatchType.Folder:
                        // 补上末尾分隔符，前缀匹配时才能按"路径边界"匹配（C:\Games 不会匹配到 C:\GamesOld）
                        (isBlack ? blackFolders : whiteFolders).Add(WithTrailingSeparator(rule.Target));
                        break;

                    case BlacklistMatchType.Unknown:
                        if (isBlack) _blackUnknown = true;
                        else _whiteUnknown = true;
                        break;
                }
            }

            _blackFolders = blackFolders.ToArray();
            _whiteFolders = whiteFolders.ToArray();
        }

        /// <summary> 是否在黑名单中（始终禁用 Quicker） </summary>
        public bool IsBlacklisted(ProcessIdentity process)
        {
            return Match(process, _blackProcesses, _blackFolders, _blackUnknown);
        }

        /// <summary> 是否在白名单中（全屏/最大化禁用时仍启用 Quicker） </summary>
        public bool IsWhitelisted(ProcessIdentity process)
        {
            return Match(process, _whiteProcesses, _whiteFolders, _whiteUnknown);
        }

        private static bool Match(ProcessIdentity process, HashSet<string> processes, string[] folders, bool matchUnknown)
        {
            // 完全识别不了的程序，只由"无法识别"规则决定
            if (process.IsUnknown) return matchUnknown;

            if (processes.Contains(process.Name)) return true;

            if (process.HasPath)
            {
                for (int i = 0; i < folders.Length; i++)
                {
                    if (process.ExePath.StartsWith(folders[i], StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            return false;
        }

        private static string WithTrailingSeparator(string folder)
        {
            return folder.EndsWith('\\') ? folder : folder + "\\";
        }
    }
}
