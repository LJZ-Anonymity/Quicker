using Quicker.Contracts.Models;

namespace Quicker.Contracts.Hosting
{
    /// <summary>
    /// 定义外部宿主进程的能力（用于未来隔离启动外部程序、加载扩展等操作）
    /// </summary>
    public interface IActionHost
    {
        /// <summary>
        /// 执行动作（同步/异步）
        /// </summary>
        Task<HostResult> ExecuteActionAsync(ButtonData data, CancellationToken cancellationToken = default);

        /// <summary>
        /// 加载扩展
        /// </summary>
        Task<HostResult> LoadExtensionAsync(string dllPath, CancellationToken cancellationToken = default);
    }
}