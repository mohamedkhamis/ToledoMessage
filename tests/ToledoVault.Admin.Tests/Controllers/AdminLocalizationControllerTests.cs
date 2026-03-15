using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using ToledoVault.Controllers.Admin;
using ToledoVault.Data;
using ToledoVault.Services;
using ToledoVault.Shared.DTOs;

namespace ToledoVault.Admin.Tests.Controllers;

[TestClass]
public class AdminLocalizationControllerTests
{
    private static (AdminLocalizationController controller, ApplicationDbContext db, LocalizationOverrideService service) CreateController()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new ApplicationDbContext(options);

        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new LocalizationOverrideService(db, cache, NullLogger<LocalizationOverrideService>.Instance);
        var controller = new AdminLocalizationController(service, NullLogger<AdminLocalizationController>.Instance);
        SetAdminUser(controller);

        return (controller, db, service);
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

    [TestMethod]
    public async Task GetLocalization_ReturnsMergedResxAndOverrides()
    {
        var (controller, _, service) = CreateController();

        // Add a DB override so we have at least one "override" source entry
        await service.UpdateOverrideAsync("TestKey.CustomOverride", "en", "Custom English Value");

        var result = await controller.GetLocalization();

        Assert.IsInstanceOfType<OkObjectResult>(result);
        var ok = (OkObjectResult)result;
        Assert.IsInstanceOfType<LocalizationListResponse>(ok.Value);
        var response = (LocalizationListResponse)ok.Value;

        // Should have entries (baseline .resx entries + our override)
        Assert.IsTrue(response.TotalKeys > 0);
        Assert.IsTrue(response.Entries.Count > 0);
        Assert.IsNotNull(response.Languages);
        Assert.IsTrue(response.Languages.Contains("en"));
        Assert.IsTrue(response.Languages.Contains("ar"));

        // Our custom override should be present with source "override"
        var customEntry = response.Entries.FirstOrDefault(e => e.ResourceKey == "TestKey.CustomOverride");
        Assert.IsNotNull(customEntry);
        Assert.IsTrue(customEntry.Values.ContainsKey("en"));
        Assert.AreEqual("Custom English Value", customEntry.Values["en"].Value);
        Assert.AreEqual("override", customEntry.Values["en"].Source);
    }

    [TestMethod]
    public async Task UpdateLocalization_CreatesOverride_Returns204()
    {
        var (controller, db, _) = CreateController();

        var request = new UpdateLocalizationRequest("en", "Updated Value");
        var result = await controller.UpdateLocalization("SomeKey.Test", request);

        Assert.IsInstanceOfType<NoContentResult>(result);

        // Verify the override was persisted in the DB
        var override_ = await db.LocalizationOverrides
            .FirstOrDefaultAsync(o => o.ResourceKey == "SomeKey.Test" && o.LanguageCode == "en");
        Assert.IsNotNull(override_);
        Assert.AreEqual("Updated Value", override_.Value);
    }

    [TestMethod]
    public async Task CreateKey_NewKey_Returns201()
    {
        var (controller, db, _) = CreateController();

        var request = new CreateLocalizationKeyRequest(
            "Brand.NewCustomKey",
            new Dictionary<string, string>
            {
                ["en"] = "English Value",
                ["ar"] = "Arabic Value"
            });

        var result = await controller.CreateLocalizationKey(request);

        Assert.IsInstanceOfType<CreatedResult>(result);
        var created = (CreatedResult)result;
        Assert.AreEqual("/api/admin/localization/Brand.NewCustomKey", created.Location);

        // Verify both language entries were created in DB
        var overrides = await db.LocalizationOverrides
            .Where(o => o.ResourceKey == "Brand.NewCustomKey")
            .ToListAsync();
        Assert.AreEqual(2, overrides.Count);
        Assert.IsTrue(overrides.All(o => o.IsNewKey));
    }

    [TestMethod]
    public async Task CreateKey_ExistingKey_Returns409()
    {
        var (controller, _, service) = CreateController();

        // First, create the key via the service
        await service.CreateNewKeyAsync(
            "Duplicate.Key",
            new Dictionary<string, string> { ["en"] = "Value" });

        // Now try to create it again via the controller
        var request = new CreateLocalizationKeyRequest(
            "Duplicate.Key",
            new Dictionary<string, string> { ["en"] = "Another Value" });

        var result = await controller.CreateLocalizationKey(request);

        Assert.IsInstanceOfType<ConflictObjectResult>(result);
    }

    [TestMethod]
    public async Task DeleteOverride_ExistingOverride_Returns204()
    {
        var (controller, db, service) = CreateController();

        // Create an override first
        await service.UpdateOverrideAsync("KeyToDelete", "en", "Delete Me");

        // Verify it exists
        var exists = await db.LocalizationOverrides
            .AnyAsync(o => o.ResourceKey == "KeyToDelete" && o.LanguageCode == "en");
        Assert.IsTrue(exists);

        var result = await controller.DeleteLocalizationOverride("KeyToDelete", "en");

        Assert.IsInstanceOfType<NoContentResult>(result);

        // Verify it was removed from DB
        var stillExists = await db.LocalizationOverrides
            .AnyAsync(o => o.ResourceKey == "KeyToDelete" && o.LanguageCode == "en");
        Assert.IsFalse(stillExists);
    }

    [TestMethod]
    public async Task DeleteOverride_NoOverride_Returns404()
    {
        var (controller, _, _) = CreateController();

        var result = await controller.DeleteLocalizationOverride("NonExistent.Key", "en");

        Assert.IsInstanceOfType<NotFoundResult>(result);
    }
}
