namespace Quicker.Contracts.Extensions
{
    public static class ExtensionHostBridge
    {
        // Host 进程会订阅这个事件
        public static Action<string, string>? OnShowToast { get; set; }

        public static void ShowToast(string message, string toastType)
        {
            OnShowToast?.Invoke(message, toastType);
        }
    }
}