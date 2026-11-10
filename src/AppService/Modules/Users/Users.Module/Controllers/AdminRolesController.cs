using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Users.Contracts;
using Users.Module.Services;

namespace Users.Module.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin")]
[Authorize]
public sealed class AdminRolesController : ControllerBase
{
    private readonly AdminManagementService _service;

    public AdminRolesController(AdminManagementService service)
    {
        _service = service;
    }

    [HttpGet("roles")]
    [Authorize(Policy = "Permission:Roles.Read")]
    public async Task<ActionResult<IReadOnlyCollection<AdminRoleDto>>> GetRoles(
        CancellationToken cancellationToken)
    {
        return Ok(await _service.GetRolesAsync(cancellationToken));
    }

    [HttpPost("roles")]
    [Authorize(Policy = "Permission:Roles.Create")]
    public async Task<ActionResult<AdminRoleDto>> CreateRole(
        [FromBody] CreateRoleRequest request,
        CancellationToken cancellationToken)
    {
        var role = await _service.CreateRoleAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetRoles), role);
    }

    [HttpPut("roles/{id:guid}")]
    [Authorize(Policy = "Permission:Roles.Update")]
    public async Task<ActionResult<AdminRoleDto>> UpdateRole(
        Guid id,
        [FromBody] UpdateRoleRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _service.UpdateRoleAsync(id, request, cancellationToken));
    }

    [HttpDelete("roles/{id:guid}")]
    [Authorize(Policy = "Permission:Roles.Delete")]
    public async Task<IActionResult> DeleteRole(
        Guid id,
        CancellationToken cancellationToken)
    {
        await _service.DeleteRoleAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpGet("permissions")]
    [Authorize(Policy = "Permission:Roles.Read")]
    public async Task<ActionResult<IReadOnlyCollection<AdminPermissionDto>>> GetPermissions(
        CancellationToken cancellationToken)
    {
        return Ok(await _service.GetPermissionsAsync(cancellationToken));
    }

    [HttpGet("roles/{id:guid}/permissions")]
    [Authorize(Policy = "Permission:Roles.Read")]
    public async Task<ActionResult<IReadOnlyCollection<AdminPermissionDto>>> GetRolePermissions(
        Guid id,
        CancellationToken cancellationToken)
    {
        return Ok(await _service.GetRolePermissionsAsync(id, cancellationToken));
    }

    [HttpPut("roles/{id:guid}/permissions")]
    [Authorize(Policy = "Permission:Roles.ManagePermissions")]
    public async Task<IActionResult> UpdateRolePermissions(
        Guid id,
        [FromBody] UpdateRolePermissionsRequest request,
        CancellationToken cancellationToken)
    {
        await _service.UpdateRolePermissionsAsync(
            id, request.PermissionIds, cancellationToken);

        return NoContent();
    }
}
