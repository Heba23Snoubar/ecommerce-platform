using Notifications.Contracts;
using Notifications.Module.Data;
using Microsoft.EntityFrameworkCore;

namespace Notifications.Module.Services;

internal class NotificationsService : INotificationsApi
{
    private readonly NotificationsDbContext _db;

    public NotificationsService(NotificationsDbContext db)
    {
        _db = db;
    }

    public async Task NotifyAsync(Guid userId, string message)
    {
        _db.Notifications.Add(new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Message = message
        });

        await _db.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<NotificationDto>> GetForUserAsync(Guid userId)
    {
        return await _db.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => new NotificationDto(n.Id, n.Message, n.CreatedAt))
            .ToListAsync();
    }
}