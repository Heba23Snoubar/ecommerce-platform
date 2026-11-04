namespace Notifications.Contracts;

public interface INotificationsApi
{
    Task NotifyAsync(Guid userId, string message);

    Task<IReadOnlyList<NotificationDto>> GetForUserAsync(Guid userId);
}