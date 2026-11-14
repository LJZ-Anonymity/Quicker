namespace Quicker.Contracts.Hosting
{
    /// <summary>
    /// IPC 命令 DTO
    /// </summary>
    public sealed record HostCommand(string Action, string? Message = null, string? ToastType = null)
    {

    }
}