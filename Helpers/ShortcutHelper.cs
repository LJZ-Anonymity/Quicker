using System.Windows.Input;

namespace Quicker.Helpers
{
    /// <summary>
    /// 快捷键辅助类，提供快捷键字符串生成、友好显示、比对等功能。
    /// </summary>
    public static class ShortcutHelper
    {
        /// <summary>
        /// 将按键事件(KeyEventArgs)转换为标准快捷键字符串（如 Ctrl+Alt+S）。
        /// </summary>
        /// <param name="e">按键事件参数</param>
        public static string GetShortcutString(KeyEventArgs e)
        {
            List<string> keys = new List<string>();
            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                keys.Add("Ctrl");
            if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
                keys.Add("Shift");
            if ((Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt)
                keys.Add("Alt");
            if ((Keyboard.Modifiers & ModifierKeys.Windows) == ModifierKeys.Windows)
                keys.Add("Windows");

            // 获取主键（排除修饰键）
            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            if (!IsModifierKey(key))
            {
                keys.Add(GetFriendlyKeyName(key));
            }
            return string.Join("+", keys);
        }

        /// <summary>
        /// 判断指定的Key是否为修饰键（Ctrl、Shift、Alt、Windows）。
        /// </summary>
        public static bool IsModifierKey(Key key) => key == Key.LeftCtrl || key == Key.RightCtrl ||
                   key == Key.LeftShift || key == Key.RightShift ||
                   key == Key.LeftAlt || key == Key.RightAlt ||
                   key == Key.LWin || key == Key.RWin;

        /// <summary>
        /// 获取按键的友好显示名称（如数字、常见符号等）。
        /// </summary>
        /// <param name="key">按键</param>
        public static string GetFriendlyKeyName(Key key)
        {
            // 主键盘数字
            if (key >= Key.D0 && key <= Key.D9)
                return ((char)('0' + (key - Key.D0))).ToString();
            // 小键盘数字
            if (key >= Key.NumPad0 && key <= Key.NumPad9)
                return "Num" + (key - Key.NumPad0);
            // 常见符号键
            switch (key)
            {
                case Key.OemMinus: return "-";
                case Key.OemPlus: return "=";
                case Key.OemOpenBrackets: return "[";
                case Key.OemCloseBrackets: return "]";
                case Key.OemPipe: return "\\";
                case Key.OemSemicolon: return ";";
                case Key.OemQuotes: return "'";
                case Key.OemComma: return ",";
                case Key.OemPeriod: return ".";
                case Key.OemQuestion: return "/";
                case Key.OemTilde: return "`";
            }
            // 其它按键直接ToString
            return key.ToString();
        }

        /// <summary>
        /// 比较两个快捷键字符串是否一致（忽略大小写）。
        /// </summary>
        /// <param name="shortcut1">第一个快捷键字符串</param>
        /// <param name="shortcut2">第二个快捷键字符串</param>
        public static bool IsShortcutMatch(string shortcut1, string shortcut2)
        {
            return string.Equals(shortcut1, shortcut2, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 将虚拟键码和修饰键状态转换为标准快捷键字符串
        /// </summary>
        /// <param name="virtualKeyCode">虚拟键码</param>
        /// <param name="ctrl">Ctrl是否按下</param>
        /// <param name="shift">Shift是否按下</param>
        /// <param name="alt">Alt是否按下</param>
        /// <param name="win">Win是否按下</param>
        public static string GetShortcutStringFromHook(
            int virtualKeyCode, bool ctrl, bool shift, bool alt, bool win)
        {
            List<string> keys = new List<string>();
            if (ctrl) keys.Add("Ctrl");
            if (shift) keys.Add("Shift");
            if (alt) keys.Add("Alt");
            if (win) keys.Add("Windows");

            // 将虚拟键码转换为Key
            Key key = KeyInterop.KeyFromVirtualKey(virtualKeyCode);
            if (!IsModifierKey(key))
                keys.Add(GetFriendlyKeyName(key));

            return string.Join("+", keys);
        }
    }
}