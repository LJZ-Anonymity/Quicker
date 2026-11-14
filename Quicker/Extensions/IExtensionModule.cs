namespace Quicker.Extend
{
    public interface IExtensionModule
    {
        // 模块元数据
        string Name { get; } // 模块名称
        string Version { get; } // 版本号
        string Author { get; } // 作者
        byte[] IconData { get; }  // 扩展图标
        string Description { get; } // 描述

        // 生命周期方法
        void Activate(); // 激活扩展
        void Deactivate(); // 停用扩展

        // 右键菜单相关
        bool HasContextMenu { get; } // 是否具有右键菜单
    }
}