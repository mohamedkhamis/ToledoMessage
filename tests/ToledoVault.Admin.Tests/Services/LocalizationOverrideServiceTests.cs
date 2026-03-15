using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using ToledoVault.Data;
using ToledoVault.Models;
using ToledoVault.Services;
using ToledoVault.Shared.DTOs;

namespace ToledoVault.Admin.Tests.Services;

[TestClass]
public class LocalizationOverrideServiceTests
{
    private static (LocalizationOverrideService service, ApplicationDbContext db, IMemoryCache cache) CreateService()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new ApplicationDbContext(options);

        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new LocalizationOverrideService(db, cache, NullLogger<LocalizationOverrideService>.Instance);

        return (service, db, cache);
    }

    [TestMethod]
    public async Task GetAllMergedAsync_MergesResxAndDbOverrides()
    {
        var (service, _, _) = CreateService();

        // Add a DB override for a custom key
        await service.UpdateOverrideAsync("Custom.TestKey", "en", "Custom English");

        var result = await service.GetAllMergedAsync(null, null, false);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.TotalKeys > 0);
        Assert.IsTrue(result.Languages.Contains("en"));
        Assert.IsTrue(result.Languages.Contains("ar"));

        // The custom override should be present with "override" source
        var customEntry = result.Entries.FirstOrDefault(e => e.ResourceKey == "Custom.TestKey");
        Assert.IsNotNull(customEntry);
        Assert.AreEqual("Custom English", customEntry.Values["en"].Value);
        Assert.AreEqual("override", customEntry.Values["en"].Source);

        // There should be .resx baseline entries with "resx" source as well
        var resxEntries = result.Entries.Where(e =>
            e.Values.Any(v => v.Value.Source == "resx")).ToList();
        Assert.IsTrue(resxEntries.Count > 0, "Expected at least one .resx baseline entry");
    }

    [TestMethod]
    public async Task GetAllMergedAsync_SearchFilter_MatchesKeyOrValue()
    {
        var (service, _, _) = CreateService();

        // Add a known override with unique text
        await service.UpdateOverrideAsync("Search.UniqueAlphaKey", "en", "UniqueAlphaValue");

        // Search by key fragment
        var resultByKey = await service.GetAllMergedAsync(null, "UniqueAlphaKey", false);
        Assert.IsTrue(resultByKey.Entries.Any(e => e.ResourceKey == "Search.UniqueAlphaKey"),
            "Search by key should find the override");

        // Search by value fragment
        var resultByValue = await service.GetAllMergedAsync(null, "UniqueAlphaValue", false);
        Assert.IsTrue(resultByValue.Entries.Any(e => e.ResourceKey == "Search.UniqueAlphaKey"),
            "Search by value should find the override");

        // Search for something that does not exist
        var resultNone = await service.GetAllMergedAsync(null, "ZzzNonExistentXyzTerm999", false);
        Assert.AreEqual(0, resultNone.Entries.Count,
            "Search for non-existent term should return no results");
    }

    [TestMethod]
    public async Task GetAllMergedAsync_MissingOnly_ReturnsIncompleteKeys()
    {
        var (service, _, _) = CreateService();

        // Create a new key with only one language (missing "ar")
        await service.CreateNewKeyAsync(
            "Incomplete.OnlyEnglish",
            new Dictionary<string, string> { ["en"] = "English Only" });

        var result = await service.GetAllMergedAsync(null, null, missingOnly: true);

        // Our incomplete key should appear because it's missing "ar"
        var incompleteEntry = result.Entries.FirstOrDefault(e => e.ResourceKey == "Incomplete.OnlyEnglish");
        Assert.IsNotNull(incompleteEntry, "Incomplete key should appear in missingOnly results");
        Assert.IsTrue(incompleteEntry.Values.ContainsKey("en"));
        Assert.IsFalse(incompleteEntry.Values.ContainsKey("ar"),
            "The key should be missing the 'ar' language");
    }

    [TestMethod]
    public async Task UpdateOverrideAsync_CreatesNewOverrideRow()
    {
        var (service, db, _) = CreateService();

        var success = await service.UpdateOverrideAsync("NewOverride.Key", "en", "New Value");

        Assert.IsTrue(success);

        var override_ = await db.LocalizationOverrides
            .FirstOrDefaultAsync(o => o.ResourceKey == "NewOverride.Key" && o.LanguageCode == "en");
        Assert.IsNotNull(override_);
        Assert.AreEqual("New Value", override_.Value);
        Assert.IsFalse(override_.IsNewKey);
        Assert.IsTrue(override_.LastModifiedAt > DateTimeOffset.MinValue);
    }

    [TestMethod]
    public async Task UpdateOverrideAsync_UpdatesExistingOverride()
    {
        var (service, db, _) = CreateService();

        // Create the initial override
        await service.UpdateOverrideAsync("Existing.Key", "en", "Original Value");
        var original = await db.LocalizationOverrides
            .FirstAsync(o => o.ResourceKey == "Existing.Key" && o.LanguageCode == "en");
        var originalTimestamp = original.LastModifiedAt;

        // Small delay to ensure timestamp difference
        await Task.Delay(10);

        // Update the same key
        var success = await service.UpdateOverrideAsync("Existing.Key", "en", "Updated Value");

        Assert.IsTrue(success);

        // Reload and verify
        await db.Entry(original).ReloadAsync();
        Assert.AreEqual("Updated Value", original.Value);
        Assert.IsTrue(original.LastModifiedAt >= originalTimestamp);

        // Should still be only one row for this key/lang
        var count = await db.LocalizationOverrides
            .CountAsync(o => o.ResourceKey == "Existing.Key" && o.LanguageCode == "en");
        Assert.AreEqual(1, count);
    }

    [TestMethod]
    public async Task CreateNewKeyAsync_DuplicateKey_ReturnsError()
    {
        var (service, _, _) = CreateService();

        // Create the key first
        var (success1, error1) = await service.CreateNewKeyAsync(
            "Duplicate.TestKey",
            new Dictionary<string, string> { ["en"] = "First" });
        Assert.IsTrue(success1);
        Assert.IsNull(error1);

        // Try to create the same key again
        var (success2, error2) = await service.CreateNewKeyAsync(
            "Duplicate.TestKey",
            new Dictionary<string, string> { ["en"] = "Second" });
        Assert.IsFalse(success2);
        Assert.IsNotNull(error2);
        Assert.IsTrue(error2.Contains("already exists"), $"Error should indicate key already exists, got: {error2}");
    }

    [TestMethod]
    public async Task DeleteOverrideAsync_RevertsToBaseline()
    {
        var (service, db, _) = CreateService();

        // Create an override
        await service.UpdateOverrideAsync("Revert.Key", "en", "Override Value");

        // Verify it exists
        var exists = await db.LocalizationOverrides
            .AnyAsync(o => o.ResourceKey == "Revert.Key" && o.LanguageCode == "en");
        Assert.IsTrue(exists);

        // Delete the override
        var success = await service.DeleteOverrideAsync("Revert.Key", "en");
        Assert.IsTrue(success);

        // Verify it was removed from DB
        var stillExists = await db.LocalizationOverrides
            .AnyAsync(o => o.ResourceKey == "Revert.Key" && o.LanguageCode == "en");
        Assert.IsFalse(stillExists);

        // Deleting again should return false (not found)
        var secondDelete = await service.DeleteOverrideAsync("Revert.Key", "en");
        Assert.IsFalse(secondDelete);
    }
}
