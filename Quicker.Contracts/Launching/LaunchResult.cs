namespace Quicker.Contracts.Launching
{
    public sealed record LaunchResult(bool Success, string? ErrorMessage = null)
    {
        public static LaunchResult Ok() => new(true);
        public static LaunchResult Fail(string message) => new(false, message);
    }
}