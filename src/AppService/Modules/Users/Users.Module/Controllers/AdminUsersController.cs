using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Users.Contracts;
using Users.Module.Services;

namespace Users.Module.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/users")]
[Authorize]
public sealed class AdminUsersController : ControllerBase
{
    private readonly AdminManagementService _service;

    public AdminUsersController(AdminManagementService service)
    {
        _service = service;
    }

    [HttpGet]
    [Authorize(Policy = "Permission:Users.Read")]
    public async Task<ActionResult<PagedResult<AdminUserListItem>>> GetUsers(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        return Ok(await _service.GetUsersAsync(
            search, page, pageSize, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "Permission:Users.Read")]
    public async Task<ActionResult<AdminUserDetails>> GetUser(
        Guid id,
        CancellationToken cancellationToken)
    {
        return Ok(await _service.GetUserAsync(id, cancellationToken));
    }

    [HttpPut("{id:guid}/roles")]
    [Authorize(Policy = "Permission:Users.ManageRoles")]
    public async Task<IActionResult> UpdateRoles(
        Guid id,
        [FromBody] UpdateUserRolesRequest request,
        CancellationToken cancellationToken)
    {
        var actingUserValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        Guid.TryParse(actingUserValue, out var actingUserId);

        await _service.UpdateUserRolesAsync(
            id,
            request.RoleIds,
            actingUserId == Guid.Empty ? null : actingUserId,
            cancellationToken);

        return NoContent();
    }
}
