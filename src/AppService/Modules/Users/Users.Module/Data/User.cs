
using Users.Contracts;

namespace Users.Module.Data;

internal sealed class User
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string NormalizedEmail { get; set; } = string.Empty;

    // We store the password hash, never the plain password.
    public string PasswordHash { get; set; } = string.Empty;

    // Legacy role. Will be removed after data migration.
    public UserRole Role { get; set; } = UserRole.Customer;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    // RBAC roles
    public ICollection<UserRoleAssignment> UserRoles { get; set; }
        = new HashSet<UserRoleAssignment>();
}
