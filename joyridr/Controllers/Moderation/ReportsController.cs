using Joyridr.DTOs;
using Joyridr.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Joyridr.Controllers;

/// <summary>Reporting a user, a chat message or a review to the JOYRIDR team.</summary>
[Route("api/reports")]
[Authorize]
public class ReportsController(ReportService reports) : ApiControllerBase
{
    /// <summary>Report one user, message or review (set exactly one of userId, messageId, reviewId).</summary>
    [HttpPost]
    public async Task<IActionResult> Create(CreateReportRequest request, CancellationToken ct)
    {
        await reports.CreateAsync(CurrentUserId, request, ct);
        return NoContent();
    }
}
