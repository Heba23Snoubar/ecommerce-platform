using Microsoft.EntityFrameworkCore;

namespace Users.Module.Data;

internal sealed class UsersDbContext : DbContext
{
    public UsersDbContext(DbContextOptions<UsersDbContext> options)
        : base(options)
    {
    }

    internal DbSet<User> Users => Set<User>();

    internal DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    internal DbSet<Role> Roles => Set<Role>();

    internal DbSet<Permission> Permissions => Set<Permission>();

    internal DbSet<UserRoleAssignment> UserRoleAssignments
        => Set<UserRoleAssignment>();

    internal DbSet<RolePermission> RolePermissions
        => Set<RolePermission>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("users");

        // =========================================
        // User
        // =========================================

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");

            entity.HasKey(u => u.Id);

            entity.Property(u => u.FullName)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(u => u.Email)
                .IsRequired()
                .HasMaxLength(256);

            entity.Property(u => u.NormalizedEmail)
                .IsRequired()
                .HasMaxLength(256);

            entity.HasIndex(u => u.NormalizedEmail)
                .IsUnique();

            entity.Property(u => u.PasswordHash)
                .IsRequired();

            // Legacy role: retained temporarily for migration.
            entity.Property(u => u.Role)
                .IsRequired();

            entity.Property(u => u.IsActive)
                .IsRequired();

            entity.Property(u => u.CreatedAtUtc)
                .IsRequired();
        });

        // =========================================
        // Refresh Token
        // =========================================

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("refresh_tokens");

            entity.HasKey(token => token.Id);

            entity.Property(token => token.TokenHash)
                .IsRequired()
                .HasMaxLength(64);

            entity.HasIndex(token => token.TokenHash)
                .IsUnique();

            entity.Property(token => token.CreatedAtUtc)
                .IsRequired();

            entity.Property(token => token.ExpiresAtUtc)
                .IsRequired();

            entity.Property(token => token.ReplacedByTokenHash)
                .HasMaxLength(64);

            entity.HasOne(token => token.User)
                .WithMany()
                .HasForeignKey(token => token.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(token => token.UserId);
        });

        // =========================================
        // Role
        // =========================================

        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("roles");

            entity.HasKey(role => role.Id);

            entity.Property(role => role.Name)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(role => role.NormalizedName)
                .IsRequired()
                .HasMaxLength(100);

            entity.HasIndex(role => role.NormalizedName)
                .IsUnique();

            entity.Property(role => role.Description)
                .HasMaxLength(500);

            entity.Property(role => role.IsSystem)
                .IsRequired();

            entity.Property(role => role.CreatedAtUtc)
                .IsRequired();
        });

        // =========================================
        // Permission
        // =========================================

        modelBuilder.Entity<Permission>(entity =>
        {
            entity.ToTable("permissions");

            entity.HasKey(permission => permission.Id);

            entity.Property(permission => permission.Name)
                .IsRequired()
                .HasMaxLength(150);

            entity.HasIndex(permission => permission.Name)
                .IsUnique();

            entity.Property(permission => permission.Description)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(permission => permission.Module)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(permission => permission.CreatedAtUtc)
                .IsRequired();
        });

        // =========================================
        // User Role Assignment
        // =========================================

        modelBuilder.Entity<UserRoleAssignment>(entity =>
        {
            entity.ToTable("user_roles");

            entity.HasKey(assignment => new
            {
                assignment.UserId,
                assignment.RoleId
            });

            entity.Property(assignment => assignment.AssignedAtUtc)
                .IsRequired();

            entity.HasOne(assignment => assignment.User)
                .WithMany(user => user.UserRoles)
                .HasForeignKey(assignment => assignment.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(assignment => assignment.Role)
                .WithMany(role => role.UserRoles)
                .HasForeignKey(assignment => assignment.RoleId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(assignment => assignment.RoleId);
        });

        // =========================================
        // Role Permission
        // =========================================

        modelBuilder.Entity<RolePermission>(entity =>
        {
            entity.ToTable("role_permissions");

            entity.HasKey(rolePermission => new
            {
                rolePermission.RoleId,
                rolePermission.PermissionId
            });

            entity.Property(rolePermission => rolePermission.AssignedAtUtc)
                .IsRequired();

            entity.HasOne(rolePermission => rolePermission.Role)
                .WithMany(role => role.RolePermissions)
                .HasForeignKey(rolePermission => rolePermission.RoleId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(rolePermission => rolePermission.Permission)
                .WithMany(permission => permission.RolePermissions)
                .HasForeignKey(rolePermission => rolePermission.PermissionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(rolePermission => rolePermission.PermissionId);
        });
    }
}