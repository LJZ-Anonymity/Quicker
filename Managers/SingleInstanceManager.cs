using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Windows;
using System.Linq;
using System.Text;

namespace Quicker.Managers
{
    internal class SingleInstanceManager
    {
        private static Mutex? _mutex = null; // 互斥锁

        /// <summary>
        /// 检查是否已经存在实例
        /// </summary>
        /// <param name="mutexName"> 互斥锁名称 </param>
        /// <param name="isNewInstance"> 是否是新实例 </param>
        /// <returns> 是否是新实例 </returns>
        public static bool CheckForOtherInstances(string mutexName, out bool isNewInstance)
        {
            isNewInstance = true; // 默认为新实例
            try
            {
                _mutex = new Mutex(true, mutexName, out isNewInstance); // 尝试创建互斥锁
            }
            catch
            {
                isNewInstance = false; // 不是管理员权限，无法创建互斥锁
            }
            return isNewInstance; // 返回是否是新实例
        }

        // 释放互斥锁
        public static void ReleaseMutex()
        {
            if (_mutex != null)
            {
                _mutex.Dispose(); // 释放互斥锁资源
                _mutex = null; // 清除互斥锁引用
            }
        }
    }
}