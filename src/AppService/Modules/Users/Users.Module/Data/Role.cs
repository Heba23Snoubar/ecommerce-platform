
namespace Users.Module.Data;

internal sealed class Role
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public string NormalizedName { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsSystem { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<UserRoleAssignment> UserRoles { get; set; }
        = new HashSet<UserRoleAssignment>();

    public ICollection<RolePermission> RolePermissions { get; set; }
        = new HashSet<RolePermission>();
}
