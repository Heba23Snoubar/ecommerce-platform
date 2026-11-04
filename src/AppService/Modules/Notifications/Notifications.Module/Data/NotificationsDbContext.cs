using Microsoft.EntityFrameworkCore;

namespace Notifications.Module.Data;

internal class NotificationsDbContext : DbContext
{
    public NotificationsDbContext(
        DbContextOptions<NotificationsDbContext> options)
        : base(options)
    {
    }

    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.HasDefaultSchema("notifications");

        b.Entity<Notification>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Message).HasMaxLength(500);
            e.HasIndex(x => x.UserId);
        });
    }
}