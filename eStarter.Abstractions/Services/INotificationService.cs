using System.Threading.Tasks;

namespace eStarter.Services
{
    public enum NotificationType
    {
        Info,
        Success,
        Warning,
        Error
    }

    /// <summary>
    /// Abstracts toast/notification display. Each UI platform provides its own implementation.
    /// </summary>
    public interface INotificationService
    {
        Task ShowAsync(string title, string message, NotificationType type = NotificationType.Info);
    }
}
