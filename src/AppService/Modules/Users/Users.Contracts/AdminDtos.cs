using System.ComponentModel.DataAnnotations;

namespace Users.Contracts;

public sealed record AdminUserListItem(
    Guid Id,
    string FullName,
    string Email,
    bool IsActive,
    DateTime CreatedAtUtc,
    IReadOnlyCollection<string> Roles
);

public sealed record AdminUserDetails(
    Guid Id,
    string FullName,
    string Email,
    bool IsActive,
    DateTime CreatedAtUtc,
    IReadOnlyCollection<AdminRoleSummary> Roles
);

public sealed record AdminRoleSummary(Guid Id, string Name);

public sealed record AdminRoleDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsSystem,
    DateTime CreatedAtUtc,
    int UserCount,
    int PermissionCount
);

public sealed record AdminPermissionDto(
    Guid Id,
    string Name,
    string Module,
    string Description,
    DateTime CreatedAtUtc
);

public sealed record CreateRoleRequest(
    [Required]
    [StringLength(100, MinimumLength = 2)]
    string Name,

    [StringLength(500)]
    string? Description
);

public sealed record UpdateRoleRequest(
    [Required]
    [StringLength(100, MinimumLength = 2)]
    string Name,

    [StringLength(500)]
    string? Description
);

public sealed record UpdateRolePermissionsRequest(
    IReadOnlyCollection<Guid> PermissionIds
);

public sealed record UpdateUserRolesRequest(
    IReadOnlyCollection<Guid> RoleIds
);

public sealed record PagedResult<T>(
    IReadOnlyCollection<T> Items,
    int Page,
    int PageSize,
    int TotalCount
);
