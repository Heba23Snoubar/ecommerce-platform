namespace Notifications.Contracts;

public record NotificationDto(Guid Id, string Message, DateTime CreatedAt);