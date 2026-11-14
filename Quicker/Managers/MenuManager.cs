using System.Collections.Generic;
using Quicker.Windows.Menus;
using System.Windows;

namespace Quicker.Managers
{
    /// <summary>
    /// 菜单管理器 - 确保同一时刻只有一个菜单窗口处于活跃状态。
    /// 以 Application.Current.Windows 作为唯一数据源，不维护额外集合。
    /// </summary>
    public static class MenuManager
    {
        #region 公共方法
        /// <summary>
        /// 注册菜单窗口（打开前关闭其他菜单，确保互斥）
        /// </summary>
        /// <param name="menu">菜单窗口</param>
        public static void RegisterMenu(BaseMenuWindow menu)
        {
            if (menu == null) return;
            RunOnUIThread(() => CloseOtherMenus(menu));
        }

        /// <summary>
        /// 注销菜单窗口（WPF 自动维护窗口列表，此方法保留仅为兼容 API）
        /// </summary>
        /// <param name="menu">菜单窗口</param>
        public static void UnregisterMenu(BaseMenuWindow menu)
        {
            // Application.Current.Windows 由 WPF 自动维护，无需手动注销。
        }

        /// <summary>
        /// 检查焦点是否在菜单窗口上
        /// </summary>
        public static bool IsFocusOnMenu()
        {
            var app = Application.Current;
            if (app == null) return false;
            return app.Windows.OfType<BaseMenuWindow>().Any(w => w.IsActive);
        }
        #endregion

        #region 私有方法
        /// <summary>
        /// 关闭除指定菜单外的所有其他菜单
        /// </summary>
        /// <param name="excludeMenu">要排除的菜单</param>
        private static void CloseOtherMenus(BaseMenuWindow excludeMenu)
        {
            // 先快照，避免遍历过程中窗口集合被修改
            var toClose = GetActiveMenus()
                .Where(m => !ReferenceEquals(m, excludeMenu))
                .ToList();

            foreach (var m in toClose)
            {
                try
                {
                    m.CloseWithAnimation();
                }
                catch
                {
                    // 忽略单个菜单关闭异常，保证其他菜单继续关闭
                }
            }
        }

        /// <summary>
        /// 获取所有活跃的菜单窗口（在 WPF 的 Windows 集合基础上过滤）
        /// </summary>
        private static IEnumerable<BaseMenuWindow> GetActiveMenus()
        {
            var app = Application.Current;
            if (app == null) yield break;

            foreach (Window w in app.Windows)
            {
                if (w is BaseMenuWindow menu && menu.IsVisible)
                    yield return menu;
            }
        }

        /// <summary>
        /// 确保在 UI 线程执行
        /// </summary>
        private static void RunOnUIThread(Action action)
        {
            if (action == null) return;

            var app = Application.Current;
            if (app == null) return;

            var dispatcher = app.Dispatcher;
            if (dispatcher == null) return;

            if (dispatcher.CheckAccess())
                action();
            else
                dispatcher.BeginInvoke(action);
        }
        #endregion
    }
}