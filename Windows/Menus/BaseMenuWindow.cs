using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.ComponentModel;
using Quicker.Windows.Menus;
using Quicker.Managers;
using Quicker.Helpers;
using System.Windows;

namespace Quicker.Windows.Menus
{
    /// <summary>
    /// 所有菜单窗口的抽象基类
    /// 提供淡入淡出动画、失焦关闭等通用行为
    /// </summary>
    public abstract class BaseMenuWindow : Window
    {
        #region 显式属性声明（确保继承关系正确）
        /// <summary>
        /// 窗口可见性
        /// </summary>
        public new Visibility Visibility
        {
            get => base.Visibility;
            set => base.Visibility = value;
        }

        /// <summary>
        /// 窗口顶部位置
        /// </summary>
        public new double Top
        {
            get => base.Top;
            set => base.Top = value;
        }

        /// <summary>
        /// 窗口高度
        /// </summary>
        public new double Height
        {
            get => base.Height;
            set => base.Height = value;
        }

        /// <summary>
        /// 窗口宽度
        /// </summary>
        public new double Width
        {
            get => base.Width;
            set => base.Width = value;
        }

        /// <summary>
        /// 显示窗口并执行淡入动画
        /// </summary>
        public void ShowWithAnimation()
        {
            base.Show();
            if (UseAnimation)
                AnimationManager.FadeIn();
            else
                Opacity = 1;
        }

        /// <summary>
        /// 激活窗口
        /// </summary>
        public new bool Activate() => base.Activate();

        /// <summary>
        /// 查找资源
        /// </summary>
        public new object FindResource(object resourceKey) => base.FindResource(resourceKey);
        #endregion

        #region 动画与行为开关
        /// <summary>
        /// 是否启用淡入淡出动画（从数据库设置中读取）
        /// </summary>
        protected bool UseAnimation { get; set; }

        /// <summary>
        /// 淡入动画时长
        /// </summary>
        protected TimeSpan FadeInDuration { get; set; } = TimeSpan.FromMilliseconds(100);

        /// <summary>
        /// 淡出动画时长
        /// </summary>
        protected TimeSpan FadeOutDuration { get; set; } = TimeSpan.FromMilliseconds(50);

        /// <summary>
        /// 菜单创建时间，用于防止新菜单立即被关闭
        /// </summary>
        private readonly DateTime _creationTime = DateTime.Now;

        /// <summary>
        /// 最小显示时间（毫秒），防止菜单转瞬即逝
        /// 这个时间应该大于淡入动画时长，确保动画完成前菜单不会被关闭
        /// </summary>
        protected int MinDisplayTimeMs { get; set; } = 250;

        /// <summary>
        /// 是否正在关闭（防止重复触发关闭动画）
        /// </summary>
        private bool _isClosing;
        #endregion

        #region 动画管理器（内置）
        /// <summary>
        /// 内置动画管理器，子类可直接使用
        /// </summary>
        protected AnimationManager AnimationManager { get; private set; }
        #endregion

        #region 公共事件
        /// <summary>
        /// 窗口即将关闭或隐藏时触发（在 OnClosing 阶段触发，早于 Closed）
        /// </summary>
        public event Action ClosingOrHiding;
        #endregion

        #region 构造与初始化
        protected BaseMenuWindow()
        {
            LoadUseAnimationFromSettings(); // 从设置中读取是否启用动画
            AnimationManager = new AnimationManager(this, FadeInDuration, FadeOutDuration); // 初始化动画管理器
            Opacity = 0; // 默认透明，等待淡入

            // 注册生命周期事件
            Loaded += OnWindowLoaded;
            Deactivated += OnWindowDeactivated;

            MenuManager.RegisterMenu(this); // 注册到菜单管理器（关闭其他菜单）
        }

        /// <summary>
        /// 从设置中加载是否启用菜单动画
        /// </summary>
        private void LoadUseAnimationFromSettings()
        {
            UseAnimation = Quicker.Database.Core.SettingDatabase.GetAllConventions().FirstOrDefault().UseMenuAnimation;
        }
        #endregion

        #region 生命周期钩子
        /// <summary>
        /// 窗口加载完成后执行淡入动画
        /// </summary>
        protected virtual void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            SetWindowTopmost(); // 设置窗口置顶
            if (UseAnimation && AnimationManager != null)
                AnimationManager.FadeIn();
            else
                Opacity = 1;
        }

        /// <summary>
        /// 失去焦点时统一处理
        /// </summary>
        protected virtual void OnWindowDeactivated(object sender, EventArgs e)
        {
            // 延迟执行，避免鼠标点击其他窗口时立即触发
            Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
            {
                if (!IsActive)
                {
                    // 检查菜单是否已经显示了足够长的时间
                    var displayTime = DateTime.Now - _creationTime;
                    if (displayTime.TotalMilliseconds < MinDisplayTimeMs)
                        return; // 菜单显示时间太短，不关闭

                    // 检查焦点是否真的离开了所有菜单
                    if (!MenuManager.IsFocusOnMenu())
                        HandleDeactivated();
                }
            }));
        }

        /// <summary>
        /// 窗口即将关闭：统一在此触发 ClosingOrHiding 事件
        /// （无论通过失焦、MenuManager 互斥关闭、还是用户按 × 关闭，都会经过此处）
        /// </summary>
        protected override void OnClosing(CancelEventArgs e)
        {
            if (!_isClosing)
            {
                _isClosing = true;
                try { ClosingOrHiding?.Invoke(); } catch { /* 订阅方异常不应阻止窗口关闭 */ }
            }
            base.OnClosing(e);
        }
        #endregion

        #region 失焦处理
        /// <summary>
        /// 实际执行失焦后的关闭/隐藏逻辑
        /// </summary>
        protected virtual void HandleDeactivated()
        {
            if (UseAnimation)
                CloseWithAnimation();
            else
                Close();
        }
        #endregion

        #region 动画辅助（供子类调用）
        /// <summary>
        /// 带动画的关闭（淡出→真正 Close），幂等：重复调用只生效一次
        /// </summary>
        public void CloseWithAnimation()
        {
            if (_isClosing) return; // 已经在关闭流程中，避免重复动画

            if (AnimationManager != null)
            {
                AnimationManager.CloseWithFade(); // 动画结束后内部会调用 Close()
            }
            else
            {
                Close();
            }
        }
        #endregion

        #region 窗口管理包装方法
        /// <summary>
        /// 获取当前窗口的 Window 实例
        /// </summary>
        private Window GetWindowInstance() => this as Window;

        /// <summary>
        /// 设置窗口置顶
        /// </summary>
        public void SetWindowTopmost() => WindowHelper.SetWindowTopmost(GetWindowInstance());

        /// <summary>
        /// 设置窗口位置到鼠标附近
        /// </summary>
        public void SetWindowPositionNearMouse() => WindowHelper.SetWindowPositionNearMouse(GetWindowInstance());

        /// <summary>
        /// 设置窗口左下角到鼠标附近
        /// </summary>
        public void SetWindowBottomLeftNearMouse() => WindowHelper.SetWindowBottomLeftNearMouse(GetWindowInstance());

        /// <summary>
        /// 隐藏主窗口
        /// </summary>
        public void HideMainWindow() => AppStateManager.MainWindowInstance.HideWindowSilently();

        /// <summary>
        /// 延时关闭菜单
        /// </summary>
        public async Task CloseMenuAsync() => await WindowHelper.CloseMenuAsync(GetWindowInstance());
        #endregion

        #region 资源清理
        protected override void OnClosed(EventArgs e)
        {
            Loaded -= OnWindowLoaded;
            Deactivated -= OnWindowDeactivated;

            // 从菜单管理器中注销（当前为空操作，保留调用以兼容未来实现）
            MenuManager.UnregisterMenu(this);

            // 释放动画管理器引用
            AnimationManager = null;

            base.OnClosed(e);
        }
        #endregion
    }
}