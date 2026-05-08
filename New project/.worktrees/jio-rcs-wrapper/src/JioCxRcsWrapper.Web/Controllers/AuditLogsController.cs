using System.Text;
using JioCxRcsWrapper.Application.Common.Interfaces;
using JioCxRcsWrapper.Application.Common.Pagination;
using JioCxRcsWrapper.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JioCxRcsWrapper.Web.Controllers;

[Authorize(Roles = "Admin")]
public sealed class AuditLogsController : Controller
{
    private readonly IUnitOfWork _unitOfWork;

    public AuditLogsController(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<IActionResult> Index(int pageNumber = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var query = _unitOfWork.Repository<AuditLog>().Query()
            .OrderByDescending(log => log.Timestamp);

        var totalItems = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var userEmails = await _unitOfWork.Repository<User>().Query()
            .ToDictionaryAsync(u => u.Id, u => u.Email, cancellationToken);

        ViewBag.UserEmails = userEmails;

        return View(PagedResult<AuditLog>.FromPaged(items, pageNumber, pageSize, totalItems));
    }

    public async Task<IActionResult> DownloadCsv(CancellationToken cancellationToken)
    {
        var logs = await _unitOfWork.Repository<AuditLog>().Query()
            .OrderByDescending(log => log.Timestamp)
            .Take(1000)
            .ToListAsync(cancellationToken);

        var userEmails = await _unitOfWork.Repository<User>().Query()
            .ToDictionaryAsync(u => u.Id, u => u.Email, cancellationToken);

        var builder = new StringBuilder();
        builder.AppendLine("Timestamp,User,Module,Action,Request,Response");

        foreach (var log in logs)
        {
            var email = userEmails.TryGetValue(log.UserId, out var e) ? e : log.UserId.ToString();
            builder.AppendLine($"\"{log.Timestamp:g}\",\"{email}\",\"{log.Module}\",\"{log.Action}\",\"{Escape(log.RequestPayload)}\",\"{Escape(log.ResponseJson)}\"");
        }

        return File(Encoding.UTF8.GetBytes(builder.ToString()), "text/csv", "audit-logs.csv");
    }

    private static string Escape(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        return value.Replace("\"", "\"\"");
    }
}
