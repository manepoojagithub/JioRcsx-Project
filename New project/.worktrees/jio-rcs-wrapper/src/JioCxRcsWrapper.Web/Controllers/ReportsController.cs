using System.Text;
using JioCxRcsWrapper.Application.Common.Pagination;
using JioCxRcsWrapper.Application.Reports;
using JioCxRcsWrapper.Application.Security;
using JioCxRcsWrapper.Web.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using JioCxRcsWrapper.Application.Common.Interfaces;
using JioCxRcsWrapper.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace JioCxRcsWrapper.Web.Controllers;

[Authorize]
public sealed class ReportsController : Controller
{
    private readonly IReportService _reports;
    private readonly ICsvReportExporter _csvExporter;
    private readonly IPdfReportExporter _pdfExporter;
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public ReportsController(IReportService reports, ICsvReportExporter csvExporter, IPdfReportExporter pdfExporter, ICurrentUser currentUser, IUnitOfWork unitOfWork)
    {
        _reports = reports;
        _csvExporter = csvExporter;
        _pdfExporter = pdfExporter;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    [RequirePermission("Reports", "View")]
    public async Task<IActionResult> Index(ReportFilter filter, int pageNumber = 1, int pageSize = 10, CancellationToken cancellationToken = default)
    {
        var reports = await _reports.GetCampaignReportsAsync(filter, cancellationToken);
        ViewBag.TotalSent = reports.Sum(report => report.TotalSent);
        ViewBag.Delivered = reports.Sum(report => report.Delivered);
        ViewBag.Failed = reports.Sum(report => report.Failed);
        ViewBag.CampaignCount = reports.Count;
        return View(PagedResult<CampaignReportSummary>.Create(reports, pageNumber, pageSize));
    }

    [RequirePermission("Reports", "View")]
    public async Task<IActionResult> Details(int id, ContactReportFilter filter, int pageNumber = 1, int pageSize = 10, CancellationToken cancellationToken = default)
    {
        var result = await _reports.GetContactReportAsync(id, filter, cancellationToken);
        if (!result.IsSuccess)
        {
            return NotFound();
        }

        ViewData["CampaignId"] = id;
        ViewBag.TotalContacts = result.Rows.Count;
        ViewBag.Opened = result.Rows.Count(row => row.Opened);
        ViewBag.Clicked = result.Rows.Count(row => row.Clicked);
        ViewBag.Errors = result.Rows.Count(row => !string.IsNullOrWhiteSpace(row.LastError));
        return View(PagedResult<ContactReportRow>.Create(result.Rows, pageNumber, pageSize));
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetTechnicalTrace(int campaignId, CancellationToken cancellationToken)
    {
        var logs = await _unitOfWork.Repository<MessageLog>().Query()
            .Where(l => l.CampaignId == campaignId && (l.RequestPayload != null || l.ResponseJson != null))
            .OrderByDescending(l => l.Timestamp)
            .Take(20)
            .Select(l => new {
                l.Status,
                l.Timestamp,
                l.RequestPayload,
                l.ResponseJson
            })
            .ToListAsync(cancellationToken);

        return Json(logs);
    }

    [RequirePermission("Reports", "Download")]
    public async Task<IActionResult> ExportCsv(int id, CancellationToken cancellationToken)
    {
        var result = await _reports.GetContactReportAsync(id, null, cancellationToken);
        if (!result.IsSuccess)
        {
            return NotFound();
        }

        return File(Encoding.UTF8.GetBytes(_csvExporter.Export(result.Rows, _currentUser.IsDeveloper)), "text/csv", $"campaign-{id}-report.csv");
    }

    [RequirePermission("Reports", "Download")]
    public async Task<IActionResult> ExportPdf(int id, CancellationToken cancellationToken)
    {
        var result = await _reports.GetContactReportAsync(id, null, cancellationToken);
        if (!result.IsSuccess)
        {
            return NotFound();
        }

        return File(_pdfExporter.Export(result.Rows), "application/pdf", $"campaign-{id}-report.pdf");
    }

    [RequirePermission("Reports", "Download")]
    public async Task<IActionResult> ExportBulkCsv(int[] ids, CancellationToken cancellationToken)
    {
        if (ids == null || ids.Length == 0)
        {
            return BadRequest("No campaigns selected.");
        }

        var csv = await _reports.GenerateBulkReportAsync(ids, cancellationToken);
        return File(csv, "text/csv", $"bulk-campaign-report-{DateTime.Now:yyyyMMdd}.csv");
    }
}
