namespace Quicker.Contracts.Hosting
{
    public enum HostErrorCode
    {
        None = 0,
        ProcessCrashed = 1,
        Timeout = 2,
        InvalidCommand = 3,
        ModuleLoadFailed = 4,
        ExecutionFailed = 5,
        UnknownError = 99
    }
}