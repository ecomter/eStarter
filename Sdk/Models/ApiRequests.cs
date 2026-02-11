namespace eStarter.Sdk.Models
{
    public class NotificationRequest
    {
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string Icon { get; set; } = "Info"; // Info, Warning, Error
        public int DurationSeconds { get; set; } = 5;
    }

    public class AppLaunchRequest
    {
        public string TargetAppId { get; set; } = string.Empty;
        public string Arguments { get; set; } = string.Empty;
    }
}
