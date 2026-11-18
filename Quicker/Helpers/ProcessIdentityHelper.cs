using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Text;
using System.IO;

namespace Quicker.Helpers
{
    /// <summary>
    /// 前台程序的识别结果
    /// </summary>
    /// <param name="Name">规范化的进程名（小写、无扩展名）；识别失败时为空串</param>
    /// <param name="ExePath">exe 完整路径；取不到时为空串</param>
    public readonly record struct ProcessIdentity(string Name, string ExePath)
    {
        /// <summary> 完全无法识别（既没有进程名也没有路径） </summary>
        public static readonly ProcessIdentity Unknown = new(string.Empty, string.Empty);

        public bool IsUnknown => Name.Length == 0;
        public bool HasPath => ExePath.Length > 0;
    }

    /// <summary>
    /// 识别进程：优先用 QueryFullProcessImageName 取 exe 路径（轻量，不创建 Process 对象），失败再退回 Process.ProcessName
    /// </summary>
    public static class ProcessIdentityHelper
    {
        private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern nint OpenProcess(uint desiredAccess, bool inheritHandle, uint processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(nint handle);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool QueryFullProcessImageName(nint process, uint flags, StringBuilder exeName, ref uint size);

        /// <summary>
        /// 识别当前前台窗口所属的程序
        /// </summary>
        public static ProcessIdentity GetForeground()
        {
            return FromWindow(WindowHelper.GetCurrentForegroundWindow());
        }

        /// <summary>
        /// 识别指定窗口所属的程序
        /// </summary>
        public static ProcessIdentity FromWindow(nint windowHandle)
        {
            if (windowHandle == IntPtr.Zero) return ProcessIdentity.Unknown;

            try
            {
                uint processId = WindowHelper.GetWindowProcessId(windowHandle);
                return FromProcessId(processId);
            }
            catch
            {
                return ProcessIdentity.Unknown;
            }
        }

        /// <summary>
        /// 识别指定进程
        /// </summary>
        public static ProcessIdentity FromProcessId(uint processId)
        {
            if (processId == 0) return ProcessIdentity.Unknown;

            // 1) 优先取完整路径：同时得到进程名和路径
            string exePath = TryGetImagePath(processId);
            if (exePath.Length > 0)
            {
                string name = BlacklistRuleNormalizer.NormalizeProcessName(exePath);
                if (name.Length > 0) return new ProcessIdentity(name, exePath);
            }

            // 2) 退回 Process.ProcessName（只有进程名，没有路径）
            try
            {
                using var process = Process.GetProcessById((int)processId);
                string name = BlacklistRuleNormalizer.NormalizeProcessName(process.ProcessName);
                if (name.Length > 0) return new ProcessIdentity(name, string.Empty);
            }
            catch
            {
                // 进程已退出或无权限
            }

            return ProcessIdentity.Unknown;
        }

        private static string TryGetImagePath(uint processId)
        {
            nint handle = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, processId);
            if (handle == IntPtr.Zero) return string.Empty;

            try
            {
                var buffer = new StringBuilder(1024);
                uint size = (uint)buffer.Capacity;
                return QueryFullProcessImageName(handle, 0, buffer, ref size) ? buffer.ToString() : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
            finally
            {
                CloseHandle(handle);
            }
        }
    }
}
