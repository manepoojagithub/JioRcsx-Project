using JioCxRcsWrapper.Application.Dashboard;
using JioCxRcsWrapper.Web.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JioCxRcsWrapper.Web.Controllers;

[Authorize]
public sealed class DashboardController : Controller
{
    private readonly IDashboardService _dashboard;

    public DashboardController(IDashboardService dashboard)
    {
        _dashboard = dashboard;
    }

    [RequirePermission("Dashboard", "View")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        return View(await _dashboard.GetSummaryAsync(cancellationToken));
    }

    [RequirePermission("Dashboard", "View")]
    public async Task<IActionResult> Data(CancellationToken cancellationToken)
    {
        return Json(await _dashboard.GetSummaryAsync(cancellationToken));
    }
}
