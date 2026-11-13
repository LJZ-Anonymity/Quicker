
namespace Quicker.Internal
{
    public sealed class InternalCommandManager
    {
        private static readonly Lazy<InternalCommandManager> lazyInstance = new(() => new InternalCommandManager()); // 单例实例懒加载

        public static InternalCommandManager Instance => lazyInstance.Value; // 单例实例
        public event EventHandler<InternalCommand> CommandPublished; // 内部命令发布事件
        private readonly object syncLock = new(); // 同步锁 用于同步内部命令的访问
        private InternalCommand latestCommand; // 最新内部命令 用于存储最新的内部命令

        /// <summary>
        /// 发布内部命令
        /// </summary>
        /// <param name="command">内部命令</param>
        public void PublishCommand(InternalCommand command)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));

            lock (syncLock)
            {
                latestCommand = command;
            }

            CommandPublished?.Invoke(this, command);
        }

        /// <summary>
        /// 尝试获取最新内部命令
        /// </summary>
        /// <param name="command">最新内部命令</param>
        /// <returns>是否成功获取</returns>
        public bool TryGetLatestCommand(out InternalCommand command)
        {
            lock (syncLock)
            {
                command = latestCommand;
                return command != null;
            }
        }

        /// <summary>
        /// 清除内部命令
        /// </summary>
        public void Clear()
        {
            lock (syncLock)
            {
                latestCommand = null;
            }
        }
    }
}