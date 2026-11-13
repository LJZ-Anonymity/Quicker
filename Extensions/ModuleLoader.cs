using System.Reflection;
using Quicker.Managers;
using System.IO;

namespace Quicker.Extend
{
    public class ModuleLoader
    {
        private readonly Dictionary<string, Assembly> _loadedAssemblies = new(); // 已加载的程序集
        private readonly ToastManager _toast = new(); // 通知管理器

        /// <summary>
        /// 加载模块
        /// </summary>
        /// <param name="modulePath"> 模块路径 </param>
        public void LoadModule(string modulePath)
        {
            try
            {
                string assemblyName = Path.GetFileNameWithoutExtension(modulePath); // 获取文件名
                Assembly moduleAssembly = Assembly.LoadFrom(modulePath);
                _loadedAssemblies[assemblyName] = moduleAssembly;
                foreach (Type type in moduleAssembly.GetTypes()) // 只查找并加载指定 DLL 里的扩展模块
                {
                    if (typeof(IExtensionModule).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract)
                    {
                        IExtensionModule module = (IExtensionModule)Activator.CreateInstance(type); // 实例化模块
                        module.Activate(); // 激活模块
                        break; // 找到后直接 break
                    }
                }
            }
            catch (Exception ex)
            {
                _toast.Show($"加载模块时出错：{ex.Message}", ToastType.Error); // 通知用户
            }
        }
    }
}