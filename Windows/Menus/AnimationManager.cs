using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Windows;

namespace Quicker.Windows.Menus
{
    public class AnimationManager
    {
        private readonly TimeSpan _fadeInDuration; // 淡入时长
        private readonly TimeSpan _fadeOutDuration; // 淡出时长
        private readonly Window _window; // 目标窗口

        public AnimationManager(BaseMenuWindow menuWindow, TimeSpan? fadeInDuration = null, TimeSpan? fadeOutDuration = null)
        {
            _fadeInDuration = fadeInDuration ?? TimeSpan.FromMilliseconds(100); // 默认淡入时长100ms
            _fadeOutDuration = fadeOutDuration ?? TimeSpan.FromMilliseconds(50); // 默认淡出时长50ms
            _window = menuWindow;
        }

        /// <summary>
        /// 淡入动画
        /// </summary>
        /// <param name="completedCallback">淡入完成回调</param>
        public void FadeIn(Action completedCallback = null)
        {
            var fadeInAnimation = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = new Duration(_fadeInDuration),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };

            fadeInAnimation.Completed += (s, e) =>
            {
                completedCallback?.Invoke();
            };

            _window.BeginAnimation(UIElement.OpacityProperty, fadeInAnimation);
        }

        /// <summary>
        /// 淡出动画
        /// </summary>
        /// <param name="completedCallback">淡出完成回调</param>
        public void FadeOut(Action completedCallback = null)
        {
            var fadeOutAnimation = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = new Duration(_fadeOutDuration),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };

            fadeOutAnimation.Completed += (s, e) =>
            {
                completedCallback?.Invoke();
                _window.BeginAnimation(UIElement.OpacityProperty, null); // 清除动画，防止影响后续操作
            };

            _window.BeginAnimation(UIElement.OpacityProperty, fadeOutAnimation);
        }

        /// <summary>
        /// 带淡出的关闭窗口
        /// </summary>
        public void CloseWithFade()
        {
            FadeOut(() =>
            {
                _window.Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
                {
                    _window.Close();
                }));
            });
        }
    }
}