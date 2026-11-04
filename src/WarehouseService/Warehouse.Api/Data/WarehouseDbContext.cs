using Microsoft.EntityFrameworkCore;
using Warehouse.Api.Entities;

namespace Warehouse.Api.Data;

public class WarehouseDbContext : DbContext
{
    public WarehouseDbContext(DbContextOptions<WarehouseDbContext> options) : base(options)
    {
    }

    public DbSet<Stock> Stocks => Set<Stock>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<ReservationItem> ReservationItems => Set<ReservationItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ضبط جدول Stocks
        modelBuilder.Entity<Stock>(b =>
        {
            b.HasKey(s => s.ProductId);
            b.Property(s => s.ProductId).HasMaxLength(64);
            b.Property(s => s.Quantity).IsRequired();
        });

        // ضبط جدول Reservations
        modelBuilder.Entity<Reservation>(b =>
        {
            b.HasKey(r => r.ReservationId);
            b.Property(r => r.ReservationId).HasMaxLength(64);
            b.Property(r => r.CreatedAt).IsRequired();
            b.Property(r => r.IsReleased).IsRequired();

            b.HasMany(r => r.Items)
             .WithOne(i => i.Reservation)
             .HasForeignKey(i => i.ReservationId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ضبط جدول ReservationItems
        modelBuilder.Entity<ReservationItem>(b =>
        {
            b.HasKey(i => i.Id);
            b.Property(i => i.ProductId).HasMaxLength(64).IsRequired();
            b.Property(i => i.Quantity).IsRequired();
        });
    }
}
