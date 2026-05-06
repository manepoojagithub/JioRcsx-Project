using System.Security.Claims;
using JioCxRcsWrapper.Application.Common.Pagination;
using JioCxRcsWrapper.Application.Permissions;
using JioCxRcsWrapper.Application.Users;
using JioCxRcsWrapper.Web.Filters;
using JioCxRcsWrapper.Web.Models.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace JioCxRcsWrapper.Web.Controllers;

[Authorize]
public sealed class UsersController : Controller
{
    private readonly IUserManagementService _users;
    private readonly IPermissionManagementService _permissions;

    public UsersController(IUserManagementService users, IPermissionManagementService permissions)
    {
        _users = users;
        _permissions = permissions;
    }

    [RequirePermission("Users", "View")]
    public async Task<IActionResult> Index(UserFilter filter, int pageNumber = 1, int pageSize = 10, CancellationToken cancellationToken = default)
    {
        return View(PagedResult<UserSummary>.Create(await _users.ListAsync(filter, cancellationToken), pageNumber, pageSize));
    }

    [RequirePermission("Users", "Add")]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        await PopulateListsAsync(cancellationToken);
        return View(new CreateUserViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission("Users", "Add")]
    public async Task<IActionResult> Create(CreateUserViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await PopulateListsAsync(cancellationToken);
            return View(model);
        }

        try
        {
            await _users.CreateAsync(new CreateUserRequest(model.Name, model.Email, model.Password, model.RoleId, model.ClientId, model.IsActive, model.IsDeveloper), CurrentUserId(), cancellationToken);
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            await PopulateListsAsync(cancellationToken);
            return View(model);
        }
    }

    [RequirePermission("Users", "Update")]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var user = await _users.GetForEditAsync(id, cancellationToken);
        if (user is null)
        {
            return NotFound();
        }

        await PopulateListsAsync(cancellationToken);
        return View(new EditUserViewModel
        {
            Id = user.Id,
            Name = user.Name,
            Email = user.Email,
            RoleId = user.RoleId,
            ClientId = user.ClientId,
            IsActive = user.IsActive,
            IsDeveloper = user.IsDeveloper
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission("Users", "Update")]
    public async Task<IActionResult> Edit(EditUserViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await PopulateListsAsync(cancellationToken);
            return View(model);
        }

        try
        {
            await _users.UpdateAsync(new UpdateUserRequest(model.Id, model.Name, model.RoleId, model.ClientId, model.IsActive, model.IsDeveloper), CurrentUserId(), cancellationToken);
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            await PopulateListsAsync(cancellationToken);
            return View(model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission("Users", "Disable")]
    public async Task<IActionResult> Disable(int id, CancellationToken cancellationToken)
    {
        await _users.DisableAsync(id, CurrentUserId(), cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission("Users", "Delete")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _users.DeleteAsync(id, CurrentUserId(), cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    [RequirePermission("Users", "Update")]
    public async Task<IActionResult> Permissions(int? roleId, CancellationToken cancellationToken)
    {
        var roles = await _permissions.ListRolesAsync(cancellationToken);
        var selectedRoleId = roleId ?? roles.FirstOrDefault()?.Id ?? 0;
        ViewBag.Roles = roles.Select(role => new SelectListItem(role.Name, role.Id.ToString(), role.Id == selectedRoleId)).ToArray();
        return View(await _permissions.GetEditorAsync(selectedRoleId, cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission("Users", "Update")]
    public async Task<IActionResult> Permissions(int roleId, bool isDeveloper, Dictionary<string, int[]> selectedPermissions, CancellationToken cancellationToken)
    {
        var normalized = selectedPermissions.ToDictionary(
            value => value.Key,
            value => (IReadOnlyList<int>)value.Value);
        await _permissions.UpdateAsync(roleId, normalized, isDeveloper, CurrentUserId(), cancellationToken);
        return RedirectToAction(nameof(Permissions), new { roleId });
    }

    private async Task PopulateListsAsync(CancellationToken cancellationToken)
    {
        ViewBag.Roles = (await _users.ListRolesAsync(cancellationToken))
            .Select(role => new SelectListItem(role.Name, role.Id.ToString()))
            .ToArray();
        ViewBag.Clients = (await _users.ListClientsAsync(cancellationToken))
            .Select(client => new SelectListItem(client.BrandName, client.Id.ToString()))
            .Prepend(new SelectListItem("None", string.Empty))
            .ToArray();
    }

    private int CurrentUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
