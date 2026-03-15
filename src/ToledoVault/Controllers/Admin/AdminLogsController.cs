using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ToledoVault.Data;
using ToledoVault.Shared.DTOs;

namespace ToledoVault.Controllers.Admin;

[ApiController]
[Route("api/admin/logs")]
[Authorize(Roles = "admin")]
public class AdminLogsController(
    ApplicationDbContext db,
    ILogger<AdminLogsController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetLogs([FromQuery] LogQueryRequest query)
    {
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var page = Math.Max(query.Page, 1);
        var offset = (page - 1) * pageSize;

        var whereClauses = new List<string>();
        var parameters = new List<SqlParameter>();

        if (!string.IsNullOrEmpty(query.Level))
        {
            whereClauses.Add("[Level] = @Level");
            parameters.Add(new SqlParameter("@Level", query.Level));
        }

        if (query.From.HasValue)
        {
            whereClauses.Add("[TimeStamp] >= @From");
            parameters.Add(new SqlParameter("@From", query.From.Value));
        }

        if (query.To.HasValue)
        {
            whereClauses.Add("[TimeStamp] <= @To");
            parameters.Add(new SqlParameter("@To", query.To.Value));
        }

        if (!string.IsNullOrEmpty(query.Search))
        {
            whereClauses.Add("[Message] LIKE @Search");
            parameters.Add(new SqlParameter("@Search", $"%{query.Search}%"));
        }

        var whereClause = whereClauses.Count > 0 ? "WHERE " + string.Join(" AND ", whereClauses) : "";

        // Count query
        var countSql = $"SELECT COUNT(*) FROM [LogEntries] {whereClause}";
        var totalCount = await db.Database
            .SqlQueryRaw<int>(countSql, parameters.ToArray())
            .FirstOrDefaultAsync();

        // Data query
        var dataSql = $@"SELECT [Id], [TimeStamp] AS [Timestamp], [Level], [Message],
                         NULL AS [Source], [Exception]
                         FROM [LogEntries] {whereClause}
                         ORDER BY [TimeStamp] DESC
                         OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

        var dataParams = new List<SqlParameter>(parameters.Select(p => new SqlParameter(p.ParameterName, p.Value)));
        dataParams.Add(new SqlParameter("@Offset", offset));
        dataParams.Add(new SqlParameter("@PageSize", pageSize));

        var items = await db.Database
            .SqlQueryRaw<LogEntryResponse>(dataSql, dataParams.ToArray())
            .ToListAsync();

        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

        return Ok(new PaginatedResponse<LogEntryResponse>(items, totalCount, page, pageSize, totalPages));
    }

    [HttpDelete]
    public async Task<IActionResult> DeleteLogs([FromQuery] DateTimeOffset olderThan)
    {
        var sql = "DELETE FROM [LogEntries] WHERE [TimeStamp] < @OlderThan";
        var count = await db.Database.ExecuteSqlRawAsync(sql, new SqlParameter("@OlderThan", olderThan));

        logger.LogInformation("Admin deleted {Count} log entries older than {OlderThan}", count, olderThan);
        return Ok(new LogDeleteResponse(count));
    }
}
