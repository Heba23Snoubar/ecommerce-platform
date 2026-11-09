using Microsoft.EntityFrameworkCore;
using Shared.Errors;
using Users.Contracts;
using Users.Module.Data;

namespace Users.Module.Services;

public sealed class AdminManagementService
{
    private static readonly Guid AdminRoleId =
        Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly UsersDbContext _dbContext;

    public AdminManagementService(UsersDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PagedResult<AdminUserListItem>> GetUsersAsync(
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _dbContext.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var value = search.Trim();
            query = query.Where(user =>
                user.FullName.Contains(value) ||
                user.Email.Contains(value));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var users = await query
            .OrderBy(user => user.FullName)
            .ThenBy(user => user.Email)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(user => new AdminUserListItem(
                user.Id,
                user.FullName,
                user.Email,
                user.IsActive,
                user.CreatedAtUtc,
                user.UserRoles
                    .Select(assignment => assignment.Role.Name)
                    .OrderBy(name => name)
                    .ToArray()))
            .ToListAsync(cancellationToken);

        return new PagedResult<AdminUserListItem>(
            users, page, pageSize, totalCount);
    }

    public async Task<AdminUserDetails> GetUserAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var entity = await _dbContext.Users
            .AsNoTracking()
            .Include(item => item.UserRoles)
                .ThenInclude(assignment => assignment.Role)
            .FirstOrDefaultAsync(item => item.Id == userId, cancellationToken);

        if (entity is null)
        {
            throw new AppException(
            404, "USER_NOT_FOUND", "The requested user was not found.");
        }

        return new AdminUserDetails(
            entity.Id,
            entity.FullName,
            entity.Email,
            entity.IsActive,
            entity.CreatedAtUtc,
            entity.UserRoles
                .Select(assignment => new AdminRoleSummary(
                    assignment.RoleId,
                    assignment.Role.Name))
                .OrderBy(role => role.Name)
                .ToArray());
    }

    public async Task UpdateUserRolesAsync(
        Guid userId,
        IReadOnlyCollection<Guid> roleIds,
        Guid? actingUserId,
        CancellationToken cancellationToken)
    {
        var requestedRoleIds = roleIds.Distinct().ToArray();
        var user = await _dbContext.Users
            .Include(item => item.UserRoles)
            .FirstOrDefaultAsync(item => item.Id == userId, cancellationToken);

        if (user is null)
            throw new AppException(404, "USER_NOT_FOUND", "The requested user was not found.");

        var roles = await _dbContext.Roles
            .Where(role => requestedRoleIds.Contains(role.Id))
            .ToListAsync(cancellationToken);

        if (roles.Count != requestedRoleIds.Length)
            throw new AppException(400, "INVALID_ROLE_IDS", "One or more roles do not exist.");

        var currentlyAdmin = user.UserRoles.Any(item => item.RoleId == AdminRoleId);
        var willBeAdmin = requestedRoleIds.Contains(AdminRoleId);

        if (currentlyAdmin && !willBeAdmin)
        {
            var activeAdminCount = await GetActiveAdminCountAsync(cancellationToken);
            if (activeAdminCount <= 1)
                throw new AppException(409, "LAST_ADMIN", "The last active administrator cannot be removed.");
        }

        if (actingUserId == userId && !willBeAdmin)
            throw new AppException(409, "CANNOT_REMOVE_OWN_ADMIN_ROLE", "You cannot remove your own administrator role.");

        var requestedRoleIdSet = requestedRoleIds.ToHashSet();
        var assignmentsToRemove = user.UserRoles
            .Where(assignment => !requestedRoleIdSet.Contains(assignment.RoleId))
            .ToArray();

        _dbContext.UserRoleAssignments.RemoveRange(assignmentsToRemove);

        var assignedRoleIds = user.UserRoles
            .Select(assignment => assignment.RoleId)
            .ToHashSet();

        foreach (var role in roles.Where(item => !assignedRoleIds.Contains(item.Id)))
        {
            user.UserRoles.Add(new UserRoleAssignment
            {
                UserId = user.Id,
                RoleId = role.Id,
                AssignedAtUtc = DateTime.UtcNow
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<AdminRoleDto>> GetRolesAsync(
        CancellationToken cancellationToken)
    {
        return await _dbContext.Roles
            .AsNoTracking()
            .OrderBy(role => role.Name)
            .Select(role => new AdminRoleDto(
                role.Id,
                role.Name,
                role.Description,
                role.IsSystem,
                role.CreatedAtUtc,
                role.UserRoles.Count,
                role.RolePermissions.Count))
            .ToListAsync(cancellationToken);
    }

    public async Task<AdminRoleDto> CreateRoleAsync(
        CreateRoleRequest request,
        CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        await EnsureRoleNameIsAvailableAsync(name, null, cancellationToken);

        var role = new Role
        {
            Name = name,
            NormalizedName = name.ToUpperInvariant(),
            Description = NormalizeDescription(request.Description),
            IsSystem = false,
            CreatedAtUtc = DateTime.UtcNow
        };

        _dbContext.Roles.Add(role);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetRoleAsync(role.Id, cancellationToken);
    }

    public async Task<AdminRoleDto> UpdateRoleAsync(
        Guid roleId,
        UpdateRoleRequest request,
        CancellationToken cancellationToken)
    {
        var role = await _dbContext.Roles
            .FirstOrDefaultAsync(item => item.Id == roleId, cancellationToken);

        if (role is null)
            throw new AppException(404, "ROLE_NOT_FOUND", "The requested role was not found.");

        if (role.IsSystem)
            throw new AppException(409, "SYSTEM_ROLE", "System roles cannot be modified.");

        var name = request.Name.Trim();
        await EnsureRoleNameIsAvailableAsync(name, roleId, cancellationToken);

        role.Name = name;
        role.NormalizedName = name.ToUpperInvariant();
        role.Description = NormalizeDescription(request.Description);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return await GetRoleAsync(roleId, cancellationToken);
    }

    public async Task DeleteRoleAsync(
        Guid roleId,
        CancellationToken cancellationToken)
    {
        var role = await _dbContext.Roles
            .Include(item => item.UserRoles)
            .FirstOrDefaultAsync(item => item.Id == roleId, cancellationToken);

        if (role is null)
            throw new AppException(404, "ROLE_NOT_FOUND", "The requested role was not found.");

        if (role.IsSystem)
            throw new AppException(409, "SYSTEM_ROLE", "System roles cannot be deleted.");

        if (role.UserRoles.Count > 0)
            throw new AppException(409, "ROLE_IN_USE", "The role is assigned to one or more users.");

        _dbContext.Roles.Remove(role);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<AdminPermissionDto>> GetPermissionsAsync(
        CancellationToken cancellationToken)
    {
        return await _dbContext.Permissions
            .AsNoTracking()
            .OrderBy(permission => permission.Module)
            .ThenBy(permission => permission.Name)
            .Select(permission => new AdminPermissionDto(
                permission.Id,
                permission.Name,
                permission.Module,
                permission.Description,
                permission.CreatedAtUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<AdminPermissionDto>> GetRolePermissionsAsync(
        Guid roleId,
        CancellationToken cancellationToken)
    {
        await EnsureRoleExistsAsync(roleId, cancellationToken);

        return await _dbContext.RolePermissions
            .AsNoTracking()
            .Where(item => item.RoleId == roleId)
            .OrderBy(item => item.Permission.Module)
            .ThenBy(item => item.Permission.Name)
            .Select(item => new AdminPermissionDto(
                item.Permission.Id,
                item.Permission.Name,
                item.Permission.Module,
                item.Permission.Description,
                item.Permission.CreatedAtUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateRolePermissionsAsync(
        Guid roleId,
        IReadOnlyCollection<Guid> permissionIds,
        CancellationToken cancellationToken)
    {
        await EnsureRoleExistsAsync(roleId, cancellationToken);
        var requestedPermissionIds = permissionIds.Distinct().ToArray();

        var existingPermissionIds = await _dbContext.Permissions
            .Where(permission => requestedPermissionIds.Contains(permission.Id))
            .Select(permission => permission.Id)
            .ToListAsync(cancellationToken);

        if (existingPermissionIds.Count != requestedPermissionIds.Length)
            throw new AppException(400, "INVALID_PERMISSION_IDS", "One or more permissions do not exist.");

        if (roleId == AdminRoleId)
        {
            var requiredPermissions = new[]
            {
                "Roles.Read",
                "Roles.ManagePermissions",
                "Users.ManageRoles"
            };

            var selectedNames = await _dbContext.Permissions
                .Where(permission => requestedPermissionIds.Contains(permission.Id))
                .Select(permission => permission.Name)
                .ToListAsync(cancellationToken);

            if (requiredPermissions.Any(required => !selectedNames.Contains(required, StringComparer.OrdinalIgnoreCase)))
                throw new AppException(409, "ADMIN_LOCKOUT", "The Admin role must retain its management permissions.");
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        await _dbContext.RolePermissions
            .Where(item => item.RoleId == roleId)
            .ExecuteDeleteAsync(cancellationToken);

        foreach (var permissionId in requestedPermissionIds)
        {
            _dbContext.RolePermissions.Add(new RolePermission
            {
                RoleId = roleId,
                PermissionId = permissionId,
                AssignedAtUtc = DateTime.UtcNow
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task<AdminRoleDto> GetRoleAsync(Guid roleId, CancellationToken cancellationToken)
    {
        var role = await _dbContext.Roles
            .AsNoTracking()
            .Where(item => item.Id == roleId)
            .Select(item => new AdminRoleDto(
                item.Id,
                item.Name,
                item.Description,
                item.IsSystem,
                item.CreatedAtUtc,
                item.UserRoles.Count,
                item.RolePermissions.Count))
            .FirstOrDefaultAsync(cancellationToken);

        return role ?? throw new AppException(404, "ROLE_NOT_FOUND", "The requested role was not found.");
    }

    private async Task EnsureRoleNameIsAvailableAsync(
        string name,
        Guid? excludedRoleId,
        CancellationToken cancellationToken)
    {
        var normalizedName = name.ToUpperInvariant();
        var exists = await _dbContext.Roles.AnyAsync(role =>
            role.NormalizedName == normalizedName &&
            (!excludedRoleId.HasValue || role.Id != excludedRoleId.Value),
            cancellationToken);

        if (exists)
            throw new AppException(409, "ROLE_NAME_ALREADY_EXISTS", "A role with this name already exists.");
    }

    private async Task EnsureRoleExistsAsync(Guid roleId, CancellationToken cancellationToken)
    {
        if (!await _dbContext.Roles.AnyAsync(role => role.Id == roleId, cancellationToken))
            throw new AppException(404, "ROLE_NOT_FOUND", "The requested role was not found.");
    }

    private Task<int> GetActiveAdminCountAsync(CancellationToken cancellationToken)
    {
        return _dbContext.UserRoleAssignments
            .Where(item => item.RoleId == AdminRoleId && item.User.IsActive)
            .Select(item => item.UserId)
            .Distinct()
            .CountAsync(cancellationToken);
    }

    private static string? NormalizeDescription(string? description)
    {
        return string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }
}
