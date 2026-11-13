using Quicker.Windows.EditWindows;
using Quicker.Managers;
using System.Windows;

namespace Quicker.Windows.Menus
{
    public partial class EditSceneMenu : BaseMenuWindow
    {
        private string SceneTag { get; set; } // 场景标签
        public EditSceneMenu(string sceneTag)
        {
            InitializeComponent();
            SceneTag = sceneTag; // 设置场景标签
        }

        // 重写基类的窗口加载方法
        protected override void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            base.OnWindowLoaded(sender, e); // 调用基类方法处理动画
            base.SetWindowPositionNearMouse(); // 设置窗口位置
        }

        // 点击按钮后打开编辑场景窗口
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            EditSceneWindow editSceneWindow = new(SceneTag); // 实例化编辑场景窗口
            editSceneWindow.ShowDialog(); // 显示编辑场景窗口
        }

        // 重写基类的失焦处理方法
        protected override void HandleDeactivated()
        {
            base.CloseWithAnimation();
            base.HandleDeactivated();
        }
    }
}