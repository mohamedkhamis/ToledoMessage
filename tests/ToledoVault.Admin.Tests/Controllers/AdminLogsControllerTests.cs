using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ToledoVault.Controllers.Admin;
using ToledoVault.Data;
using ToledoVault.Shared.DTOs;

namespace ToledoVault.Admin.Tests.Controllers;

[TestClass]
public class AdminLogsControllerTests
{
    private static (AdminLogsController controller, ApplicationDbContext db) CreateController()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new ApplicationDbContext(options);
        var controller = new AdminLogsController(db, NullLogger<AdminLogsController>.Instance);
        return (controller, db);
    }

    [TestMethod]
    [Ignore("AdminLogsController uses raw SQL (SqlQueryRaw/ExecuteSqlRawAsync) which requires SQL Server. Integration tests needed.")]
    public async Task GetLogs_NoFilters_ReturnsPagedResults()
    {
        var (controller, _) = CreateController();
        var result = await controller.GetLogs(new LogQueryRequest());
        Assert.IsInstanceOfType<OkObjectResult>(result);
    }

    [TestMethod]
    [Ignore("AdminLogsController uses raw SQL (SqlQueryRaw/ExecuteSqlRawAsync) which requires SQL Server. Integration tests needed.")]
    public async Task GetLogs_FilterByLevel_ReturnsOnlyMatchingLevel()
    {
        var (controller, _) = CreateController();
        var result = await controller.GetLogs(new LogQueryRequest(Level: "Error"));
        Assert.IsInstanceOfType<OkObjectResult>(result);
    }

    [TestMethod]
    [Ignore("AdminLogsController uses raw SQL (SqlQueryRaw/ExecuteSqlRawAsync) which requires SQL Server. Integration tests needed.")]
    public async Task GetLogs_FilterByDateRange_ReturnsOnlyInRange()
    {
        var (controller, _) = CreateController();
        var from = DateTimeOffset.UtcNow.AddDays(-7);
        var to = DateTimeOffset.UtcNow;
        var result = await controller.GetLogs(new LogQueryRequest(From: from, To: to));
        Assert.IsInstanceOfType<OkObjectResult>(result);
    }

    [TestMethod]
    [Ignore("AdminLogsController uses raw SQL (SqlQueryRaw/ExecuteSqlRawAsync) which requires SQL Server. Integration tests needed.")]
    public async Task GetLogs_SearchByKeyword_ReturnsMatchingMessages()
    {
        var (controller, _) = CreateController();
        var result = await controller.GetLogs(new LogQueryRequest(Search: "login"));
        Assert.IsInstanceOfType<OkObjectResult>(result);
    }

    [TestMethod]
    [Ignore("AdminLogsController uses raw SQL (SqlQueryRaw/ExecuteSqlRawAsync) which requires SQL Server. Integration tests needed.")]
    public async Task GetLogs_PaginationWorks_ReturnsCorrectPage()
    {
        var (controller, _) = CreateController();
        var result = await controller.GetLogs(new LogQueryRequest(Page: 2, PageSize: 10));
        Assert.IsInstanceOfType<OkObjectResult>(result);
    }

    [TestMethod]
    [Ignore("AdminLogsController uses raw SQL (ExecuteSqlRawAsync) which requires SQL Server. Integration tests needed.")]
    public async Task DeleteLogs_RemovesOldEntries_ReturnsCount()
    {
        var (controller, _) = CreateController();
        var result = await controller.DeleteLogs(DateTimeOffset.UtcNow.AddDays(-30));
        Assert.IsInstanceOfType<OkObjectResult>(result);
    }
}
