namespace Quicker.Contracts.Hosting
{
    /// <summary>
    /// IPC 命令 DTO
    /// </summary>
    public sealed record HostCommand(string Action)
    {
        // 如果以后需要传参数，可以在这里加属性，例如：
        // public string? Parameter { get; init; }
    }
}