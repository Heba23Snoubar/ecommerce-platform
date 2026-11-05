
using Microsoft.EntityFrameworkCore;

namespace Users.Module.Data;

internal static class RbacSeeder
{
    private static readonly Guid AdminRoleId =
        Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static readonly Guid CustomerRoleId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");

    public static async Task SeedAsync(
        UsersDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        // Verify that system roles exist.
        var rolesExist = await dbContext.Roles
            .AnyAsync(role => role.Id == AdminRoleId, cancellationToken)
            &&
            await dbContext.Roles
                .AnyAsync(role => role.Id == CustomerRoleId, cancellationToken);

        if (!rolesExist)
        {
            throw new InvalidOperationException(
                "System roles are missing. Apply the RBAC migration first.");
        }

        // Seed permissions without creating duplicates.
        var existingPermissionNames = await dbContext.Permissions
            .Select(permission => permission.Name)
            .ToListAsync(cancellationToken);

        var existingNames = existingPermissionNames.ToHashSet(
            StringComparer.OrdinalIgnoreCase);

        var newPermissions = RbacPermissions.All
            .Where(name => !existingNames.Contains(name))
            .Select(name =>
            {
                var parts = name.Split('.', 2);

                return new Permission
                {
                    Id = Guid.NewGuid(),
                    Name = name,
                    Module = parts[0],
                    Description = $"{parts[1]} permission",
                    CreatedAtUtc = now
                };
            })
            .ToList();

        if (newPermissions.Count > 0)
        {
            dbContext.Permissions.AddRange(newPermissions);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        // Give Admin all registered permissions.
        var existingRolePermissionIds = await dbContext.RolePermissions
            .Where(item => item.RoleId == AdminRoleId)
            .Select(item => item.PermissionId)
            .ToListAsync(cancellationToken);

        var assignedPermissionIds = existingRolePermissionIds.ToHashSet();

        var adminAssignments = await dbContext.Permissions
            .Where(permission => !assignedPermissionIds.Contains(permission.Id))
            .Select(permission => permission.Id)
            .ToListAsync(cancellationToken);

        foreach (var permissionId in adminAssignments)
        {
            dbContext.RolePermissions.Add(new RolePermission
            {
                RoleId = AdminRoleId,
                PermissionId = permissionId,
                AssignedAtUtc = now
            });
        }

        if (adminAssignments.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}