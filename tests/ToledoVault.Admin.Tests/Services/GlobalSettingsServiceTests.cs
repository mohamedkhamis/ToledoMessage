using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Toledo.SharedKernel.Helpers;
using ToledoVault.Data;
using ToledoVault.Models;
using ToledoVault.Services;

namespace ToledoVault.Admin.Tests.Services;

[TestClass]
public class GlobalSettingsServiceTests
{
    private static (GlobalSettingsService service, ApplicationDbContext db, IMemoryCache cache) CreateService()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new ApplicationDbContext(options);

        SeedSettings(db);

        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new GlobalSettingsService(db, cache, NullLogger<GlobalSettingsService>.Instance);

        return (service, db, cache);
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
    public async Task GetAllGroupedAsync_ReturnsCategorizedSettings()
    {
        var (service, _, _) = CreateService();

        var result = await service.GetAllGroupedAsync();

        Assert.AreEqual(3, result.Count);

        var categoryNames = result.Select(c => c.Category).OrderBy(c => c).ToList();
        CollectionAssert.AreEqual(
            new List<string> { "Appearance", "Features", "Security" },
            categoryNames);

        // Security category should have 2 settings
        var security = result.First(c => c.Category == "Security");
        Assert.AreEqual(2, security.Settings.Count);

        // Features category should have 1 setting
        var features = result.First(c => c.Category == "Features");
        Assert.AreEqual(1, features.Settings.Count);
        Assert.AreEqual("features.readReceipts", features.Settings[0].Key);
    }

    [TestMethod]
    public async Task UpdateValueAsync_ValidatesIntegerRange()
    {
        var (service, _, _) = CreateService();

        // Below minimum (100000)
        var (successBelow, errorBelow) = await service.UpdateValueAsync("security.pbkdf2Iterations", "50000");
        Assert.IsFalse(successBelow);
        Assert.IsNotNull(errorBelow);

        // Above maximum (1000000)
        var (successAbove, errorAbove) = await service.UpdateValueAsync("security.pbkdf2Iterations", "2000000");
        Assert.IsFalse(successAbove);
        Assert.IsNotNull(errorAbove);

        // Valid value within range
        var (successValid, errorValid) = await service.UpdateValueAsync("security.pbkdf2Iterations", "700000");
        Assert.IsTrue(successValid);
        Assert.IsNull(errorValid);
    }

    [TestMethod]
    public async Task UpdateValueAsync_ValidatesSelectionOptions()
    {
        var (service, _, _) = CreateService();

        // Invalid option
        var (successInvalid, errorInvalid) = await service.UpdateValueAsync("security.encryptionKeyLength", "512");
        Assert.IsFalse(successInvalid);
        Assert.IsNotNull(errorInvalid);

        // Valid option
        var (successValid, errorValid) = await service.UpdateValueAsync("security.encryptionKeyLength", "128");
        Assert.IsTrue(successValid);
        Assert.IsNull(errorValid);
    }

    [TestMethod]
    public async Task UpdateValueAsync_ValidatesBoolean()
    {
        var (service, _, _) = CreateService();

        // Invalid boolean value
        var (successInvalid, errorInvalid) = await service.UpdateValueAsync("features.readReceipts", "yes");
        Assert.IsFalse(successInvalid);
        Assert.IsNotNull(errorInvalid);

        // Valid boolean values
        var (successFalse, errorFalse) = await service.UpdateValueAsync("features.readReceipts", "false");
        Assert.IsTrue(successFalse);
        Assert.IsNull(errorFalse);

        var (successTrue, errorTrue) = await service.UpdateValueAsync("features.readReceipts", "true");
        Assert.IsTrue(successTrue);
        Assert.IsNull(errorTrue);
    }

    [TestMethod]
    public async Task ResetToDefaultAsync_SetsCurrentValueToDefault()
    {
        var (service, db, _) = CreateService();

        // First change the value
        await service.UpdateValueAsync("security.encryptionKeyLength", "128");
        var changed = await db.GlobalSettings.FirstAsync(s => s.Key == "security.encryptionKeyLength");
        Assert.AreEqual("128", changed.CurrentValue);

        // Reset to default
        var success = await service.ResetToDefaultAsync("security.encryptionKeyLength");
        Assert.IsTrue(success);

        // Reload and verify
        await db.Entry(changed).ReloadAsync();
        Assert.AreEqual("256", changed.CurrentValue);
    }

    [TestMethod]
    public async Task GetValueAsync_ReturnsCachedValue()
    {
        var (service, db, cache) = CreateService();

        // First call populates the cache
        var value1 = await service.GetValueAsync("security.encryptionKeyLength");
        Assert.AreEqual("256", value1);

        // Directly modify the DB behind the cache's back
        var setting = await db.GlobalSettings.FirstAsync(s => s.Key == "security.encryptionKeyLength");
        setting.CurrentValue = "128";
        await db.SaveChangesAsync();

        // Second call should return the cached value, not the DB value
        var value2 = await service.GetValueAsync("security.encryptionKeyLength");
        Assert.AreEqual("256", value2);

        // After removing from cache, it should return the fresh DB value
        cache.Remove("admin:setting:security.encryptionKeyLength");
        var value3 = await service.GetValueAsync("security.encryptionKeyLength");
        Assert.AreEqual("128", value3);
    }

    [TestMethod]
    public async Task ExtensibleArchitecture_NewSettingAppearsInGroupedResults()
    {
        // T057: Proves FR-016 — adding a new setting with a new category
        // requires no structural code changes; it appears automatically.
        var (service, db, _) = CreateService();

        // Add a new setting with a brand-new category (simulating a seed data entry)
        db.GlobalSettings.Add(new GlobalSetting
        {
            Id = IdGenerator.GetNewId(),
            Key = "notifications.maxRetries",
            DisplayName = "Max Push Notification Retries",
            Description = "Number of times to retry failed push notifications",
            Category = "Notifications",
            ValueType = "integer",
            CurrentValue = "3",
            DefaultValue = "3",
            ValidationRules = """{"min": 1, "max": 10}""",
            SortOrder = 0,
            LastModifiedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var result = await service.GetAllGroupedAsync();

        // The new "Notifications" category should appear automatically
        Assert.AreEqual(4, result.Count); // Was 3 categories, now 4

        var notifications = result.FirstOrDefault(c => c.Category == "Notifications");
        Assert.IsNotNull(notifications);
        Assert.AreEqual(1, notifications.Settings.Count);
        Assert.AreEqual("notifications.maxRetries", notifications.Settings[0].Key);
        Assert.AreEqual("3", notifications.Settings[0].CurrentValue);
        Assert.AreEqual("integer", notifications.Settings[0].ValueType);

        // Verify the setting can be updated with validation — no UI/controller changes needed
        var (success, error) = await service.UpdateValueAsync("notifications.maxRetries", "5");
        Assert.IsTrue(success);
        Assert.IsNull(error);

        // Out-of-range should be rejected
        var (failSuccess, failError) = await service.UpdateValueAsync("notifications.maxRetries", "15");
        Assert.IsFalse(failSuccess);
        Assert.IsNotNull(failError);
    }
}
