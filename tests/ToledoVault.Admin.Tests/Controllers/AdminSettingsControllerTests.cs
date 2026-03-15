using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Toledo.SharedKernel.Helpers;
using ToledoVault.Controllers.Admin;
using ToledoVault.Data;
using ToledoVault.Models;
using ToledoVault.Services;
using ToledoVault.Shared.DTOs;

namespace ToledoVault.Admin.Tests.Controllers;

[TestClass]
public class AdminSettingsControllerTests
{
    private static (AdminSettingsController controller, ApplicationDbContext db) CreateController()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new ApplicationDbContext(options);

        SeedSettings(db);

        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new GlobalSettingsService(db, cache, NullLogger<GlobalSettingsService>.Instance);
        var controller = new AdminSettingsController(service, NullLogger<AdminSettingsController>.Instance);
        SetAdminUser(controller);

        return (controller, db);
    }

    private static void SetAdminUser(ControllerBase controller, string username = "admin")
    {
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, username),
                    new Claim(ClaimTypes.Role, "admin")
                ], "TestScheme"))
            }
        };
    }

    private static void SeedSettings(ApplicationDbContext db)
    {
        var now = DateTimeOffset.UtcNow;
        db.GlobalSettings.AddRange(
            new GlobalSetting
            {
                Id = IdGenerator.GetNewId(),
                Key = "security.encryptionKeyLength",
                DisplayName = "Encryption Key Length",
                Description = "AES key size in bits for message encryption",
                Category = "Security",
                ValueType = "selection",
                CurrentValue = "256",
                DefaultValue = "256",
                ValidationRules = """{"options": ["128", "192", "256"]}""",
                SortOrder = 0,
                LastModifiedAt = now
            },
            new GlobalSetting
            {
                Id = IdGenerator.GetNewId(),
                Key = "security.pbkdf2Iterations",
                DisplayName = "PBKDF2 Iterations",
                Description = "Number of iterations for password-based key derivation",
                Category = "Security",
                ValueType = "integer",
                CurrentValue = "600000",
                DefaultValue = "600000",
                ValidationRules = """{"min": 100000, "max": 1000000}""",
                SortOrder = 1,
                LastModifiedAt = now
            },
            new GlobalSetting
            {
                Id = IdGenerator.GetNewId(),
                Key = "features.readReceipts",
                DisplayName = "Read Receipts",
                Description = "Allow users to see when messages are read",
                Category = "Features",
                ValueType = "boolean",
                CurrentValue = "true",
                DefaultValue = "true",
                ValidationRules = "{}",
                SortOrder = 0,
                LastModifiedAt = now
            },
            new GlobalSetting
            {
                Id = IdGenerator.GetNewId(),
                Key = "appearance.defaultTheme",
                DisplayName = "Default Theme",
                Description = "Default theme for new users",
                Category = "Appearance",
                ValueType = "selection",
                CurrentValue = "default",
                DefaultValue = "default",
                ValidationRules = """{"options": ["default", "default-dark", "whatsapp"]}""",
                SortOrder = 0,
                LastModifiedAt = now
            }
        );
        db.SaveChanges();
    }

    [TestMethod]
    public async Task GetSettings_ReturnsAllSettingsGroupedByCategory()
    {
        var (controller, _) = CreateController();

        var result = await controller.GetSettings();

        Assert.IsInstanceOfType<OkObjectResult>(result);
        var ok = (OkObjectResult)result;
        Assert.IsInstanceOfType<List<SettingCategoryResponse>>(ok.Value);
        var categories = (List<SettingCategoryResponse>)ok.Value;

        // We seeded 3 categories: Appearance, Features, Security
        Assert.AreEqual(3, categories.Count);

        var categoryNames = categories.Select(c => c.Category).OrderBy(c => c).ToList();
        CollectionAssert.AreEqual(
            new List<string> { "Appearance", "Features", "Security" },
            categoryNames);

        // Security has 2 settings
        var security = categories.First(c => c.Category == "Security");
        Assert.AreEqual(2, security.Settings.Count);
    }

    [TestMethod]
    public async Task UpdateSetting_WithValidValue_Returns204()
    {
        var (controller, db) = CreateController();

        var result = await controller.UpdateSetting(
            "security.encryptionKeyLength",
            new UpdateSettingRequest("192"));

        Assert.IsInstanceOfType<NoContentResult>(result);

        // Verify the value was updated in DB
        var setting = await db.GlobalSettings.FirstAsync(s => s.Key == "security.encryptionKeyLength");
        Assert.AreEqual("192", setting.CurrentValue);
    }

    [TestMethod]
    public async Task UpdateSetting_WithInvalidValue_Returns400()
    {
        var (controller, _) = CreateController();

        // 50000 is below the min of 100000 for pbkdf2Iterations
        var result = await controller.UpdateSetting(
            "security.pbkdf2Iterations",
            new UpdateSettingRequest("50000"));

        Assert.IsInstanceOfType<BadRequestObjectResult>(result);
    }

    [TestMethod]
    public async Task UpdateSetting_WithNonExistentKey_Returns404()
    {
        var (controller, _) = CreateController();

        var result = await controller.UpdateSetting(
            "nonexistent.setting",
            new UpdateSettingRequest("value"));

        Assert.IsInstanceOfType<NotFoundResult>(result);
    }

    [TestMethod]
    public async Task ResetSetting_ResetsToDefault_Returns204()
    {
        var (controller, db) = CreateController();

        // First, update the setting away from default
        await controller.UpdateSetting(
            "security.encryptionKeyLength",
            new UpdateSettingRequest("128"));

        // Verify it was changed
        var changed = await db.GlobalSettings.FirstAsync(s => s.Key == "security.encryptionKeyLength");
        Assert.AreEqual("128", changed.CurrentValue);

        // Reset it
        var result = await controller.ResetSetting("security.encryptionKeyLength");

        Assert.IsInstanceOfType<NoContentResult>(result);

        // Reload from DB to verify
        await db.Entry(changed).ReloadAsync();
        Assert.AreEqual("256", changed.CurrentValue);
    }
}
