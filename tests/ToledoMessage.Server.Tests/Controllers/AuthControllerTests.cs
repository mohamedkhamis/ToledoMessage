using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using ToledoMessage.Controllers;
using ToledoMessage.Models;
using ToledoMessage.Services;
using ToledoMessage.Shared.DTOs;

namespace ToledoMessage.Server.Tests.Controllers;

[TestClass]
[SuppressMessage("ReSharper", "RemoveRedundantBraces")]
[SuppressMessage("ReSharper", "RedundantNameQualifier")]
public class AuthControllerTests
{
    private static (AuthController controller, Data.ApplicationDbContext db) CreateController()
    {
        var db = TestDbContextFactory.Create();
        var passwordHasher = new PasswordHasher<User>();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SecretKey"] = "ThisIsAVeryLongTestSecretKeyForHmacSha256!@#$%",
                ["Jwt:Issuer"] = "TestIssuer",
                ["Jwt:Audience"] = "TestAudience",
                ["Jwt:ExpiryMinutes"] = "15"
            })
            .Build();
        var deletionService = new AccountDeletionService(db, NullLogger<AccountDeletionService>.Instance);
        var controller = new AuthController(db, passwordHasher, config, deletionService);
        return (controller, db);
    }

    [TestMethod]
    public async Task Register_ValidRequest_ReturnsCreated()
    {
        var (controller, _) = CreateController();
        var request = new RegisterRequest("testuser01", "Test User 01", "MySecurePass12");

        var result = await controller.Register(request);

        Assert.IsInstanceOfType<CreatedResult>(result);
        var created = (CreatedResult)result;
        Assert.IsInstanceOfType<AuthResponse>(created.Value);
        var response = (AuthResponse)created.Value;
        Assert.AreEqual("testuser01", response.Username);
        Assert.IsNotNull(response.Token);
        Assert.IsNotNull(response.RefreshToken);
    }

    [TestMethod]
    public async Task Register_EmptyUsername_ReturnsBadRequest()
    {
        var (controller, _) = CreateController();
        var result = await controller.Register(new RegisterRequest("", "Empty User", "MySecurePass12"));
        Assert.IsInstanceOfType<BadRequestObjectResult>(result);
    }

    [Microsoft.VisualStudio.TestTools.UnitTesting.TestMethod]
    public async System.Threading.Tasks.Task Register_ShortUsername_ReturnsBadRequest()
    {
        var (controller, _) = CreateController();
        var result = await controller.Register(new ToledoMessage.Shared.DTOs.RegisterRequest("ab", "AB User", "MySecurePass12"));
        Microsoft.VisualStudio.TestTools.UnitTesting.Assert.IsInstanceOfType<Microsoft.AspNetCore.Mvc.BadRequestObjectResult>(result);
        var bad = (Microsoft.AspNetCore.Mvc.BadRequestObjectResult)result;
#pragma warning disable MSTEST0046
        Microsoft.VisualStudio.TestTools.UnitTesting.StringAssert.Contains(bad.Value?.ToString(), "3 and 32");
#pragma warning restore MSTEST0046
    }

    [TestMethod]
    public async Task Register_InvalidUsernameChars_ReturnsBadRequest()
    {
        var (controller, _) = CreateController();
        var result = await controller.Register(new RegisterRequest("test user!", "Test User", "MySecurePass12"));
        Assert.IsInstanceOfType<BadRequestObjectResult>(result);
        var bad = (BadRequestObjectResult)result;
#pragma warning disable MSTEST0046
        StringAssert.Contains(bad.Value?.ToString(), "letters, digits");
#pragma warning restore MSTEST0046
    }

    [TestMethod]
    public async Task Register_ShortPassword_ReturnsBadRequest()
    {
        var (controller, _) = CreateController();
        var result = await controller.Register(new RegisterRequest("validuser", "Valid User", "short"));
        Assert.IsInstanceOfType<BadRequestObjectResult>(result);
        var bad = (BadRequestObjectResult)result;
#pragma warning disable MSTEST0046
        StringAssert.Contains(bad.Value?.ToString(), "12 characters");
#pragma warning restore MSTEST0046
    }

    [TestMethod]
    public async Task Register_DuplicateUsername_ReturnsConflict()
    {
        var (controller, db) = CreateController();
        await TestDbContextFactory.SeedUser(db, 1L, "existing");

        var result = await controller.Register(new RegisterRequest("existing", "Existing User", "MySecurePass12"));
        Assert.IsInstanceOfType<ConflictObjectResult>(result);
    }

    [TestMethod]
    public async Task Login_ValidCredentials_ReturnsOk()
    {
        var (controller, _) = CreateController();

        // Register first
        await controller.Register(new RegisterRequest("loginuser", "Login User", "MySecurePass12"));

        var result = await controller.Login(new LoginRequest("loginuser", "MySecurePass12"));

        Assert.IsInstanceOfType<OkObjectResult>(result);
        var ok = (OkObjectResult)result;
        Assert.IsInstanceOfType<AuthResponse>(ok.Value);
        var response = (AuthResponse)ok.Value;
        Assert.AreEqual("loginuser", response.Username);
        Assert.IsNotNull(response.Token);
        Assert.IsNotNull(response.RefreshToken);
    }

    [TestMethod]
    public async Task Login_WrongPassword_ReturnsUnauthorized()
    {
        var (controller, _) = CreateController();
        await controller.Register(new RegisterRequest("wrongpw", "Wrong PW", "MySecurePass12"));

        var result = await controller.Login(new LoginRequest("wrongpw", "WrongPassword1"));
        Assert.IsInstanceOfType<UnauthorizedObjectResult>(result);
    }

    [TestMethod]
    public async Task Login_NonExistentUser_ReturnsUnauthorized()
    {
        var (controller, _) = CreateController();
        var result = await controller.Login(new LoginRequest("nonexistent", "MySecurePass12"));
        Assert.IsInstanceOfType<UnauthorizedObjectResult>(result);
    }

    [TestMethod]
    public async Task Login_DeactivatedUser_ReturnsUnauthorized_WithGenericError()
    {
        var (controller, db) = CreateController();

        // Register and then deactivate
        await controller.Register(new RegisterRequest("deactuser", "Deact User", "MySecurePass12"));
        var user = db.Users.First(static u => u.Username == "deactuser");
        user.IsActive = false;
#pragma warning disable MSTEST0049
        await db.SaveChangesAsync();
#pragma warning restore MSTEST0049

        var result = await controller.Login(new LoginRequest("deactuser", "MySecurePass12"));
        Assert.IsInstanceOfType<UnauthorizedObjectResult>(result);
        var unauthorized = (UnauthorizedObjectResult)result;
        // Should return the same generic error to prevent user enumeration
#pragma warning disable MSTEST0046
        StringAssert.Contains(unauthorized.Value?.ToString(), "Invalid username or password");
#pragma warning restore MSTEST0046
    }

    [TestMethod]
    public async Task Login_CancelsPendingDeletion()
    {
        var (controller, db) = CreateController();

        await controller.Register(new RegisterRequest("deluser", "Del User", "MySecurePass12"));
        var user = db.Users.First(static u => u.Username == "deluser");
        user.DeletionRequestedAt = DateTimeOffset.UtcNow;
#pragma warning disable MSTEST0049
        await db.SaveChangesAsync();
#pragma warning restore MSTEST0049

        await controller.Login(new LoginRequest("deluser", "MySecurePass12"));

        var refreshedUser = db.Users.First(static u => u.Username == "deluser");
        Assert.IsNull(refreshedUser.DeletionRequestedAt);
    }

    [TestMethod]
    public async Task Refresh_ValidTokens_ReturnsNewTokenPair()
    {
        // ReSharper disable once UnusedVariable
        var (controller, _) = CreateController();

        // Register to get tokens
        var registerResult = await controller.Register(new RegisterRequest("refreshuser", "Refresh User", "MySecurePass12"));
        Assert.IsInstanceOfType<CreatedResult>(registerResult);
        var created = (CreatedResult)registerResult;
        Assert.IsInstanceOfType<AuthResponse>(created.Value);
        var authResponse = (AuthResponse)created.Value;

        var refreshResult = await controller.Refresh(
            new RefreshTokenRequest(authResponse.Token, authResponse.RefreshToken));

        Assert.IsInstanceOfType<OkObjectResult>(refreshResult);
        var ok = (OkObjectResult)refreshResult;
        Assert.IsInstanceOfType<RefreshTokenResponse>(ok.Value);
        var response = (RefreshTokenResponse)ok.Value;
        Assert.IsNotNull(response.Token);
        Assert.IsNotNull(response.RefreshToken);
        Assert.AreNotEqual(authResponse.RefreshToken, response.RefreshToken); // Rotated
    }

    [TestMethod]
    public async Task Refresh_InvalidAccessToken_ReturnsUnauthorized()
    {
        var (controller, _) = CreateController();

        var result = await controller.Refresh(
            new RefreshTokenRequest("invalid-jwt", "some-refresh-token"));

        Assert.IsInstanceOfType<UnauthorizedObjectResult>(result);
    }

    [TestMethod]
    public async Task Refresh_InvalidRefreshToken_ReturnsUnauthorized()
    {
        var (controller, _) = CreateController();

        var registerResult = await controller.Register(new RegisterRequest("rftestuser", "RF Test User", "MySecurePass12"));
        Assert.IsInstanceOfType<CreatedResult>(registerResult);
        var created = (CreatedResult)registerResult;
        Assert.IsInstanceOfType<AuthResponse>(created.Value);
        var authResponse = (AuthResponse)created.Value;

        var result = await controller.Refresh(
            new RefreshTokenRequest(authResponse.Token, "wrong-refresh-token"));

        Assert.IsInstanceOfType<UnauthorizedObjectResult>(result);
    }

    [TestMethod]
    public async Task Refresh_RevokedRefreshToken_ReturnsUnauthorized()
    {
        var (controller, db) = CreateController();

        var registerResult = await controller.Register(new RegisterRequest("revoketest", "Revoke Test", "MySecurePass12"));
        Assert.IsInstanceOfType<CreatedResult>(registerResult);
        var created = (CreatedResult)registerResult;
        Assert.IsInstanceOfType<AuthResponse>(created.Value);
        var authResponse = (AuthResponse)created.Value;

        // Revoke all tokens
        foreach (var token in db.RefreshTokens)
        {
            token.IsRevoked = true;
        }

#pragma warning disable MSTEST0049
        await db.SaveChangesAsync();
#pragma warning restore MSTEST0049

        var result = await controller.Refresh(
            new RefreshTokenRequest(authResponse.Token, authResponse.RefreshToken));

        Assert.IsInstanceOfType<UnauthorizedObjectResult>(result);
    }

    [TestMethod]
    public async Task DeleteAccount_Authenticated_ReturnsOk()
    {
        var (controller, db) = CreateController();
        await TestDbContextFactory.SeedUser(db, 42L, "deleteuser");
        TestDbContextFactory.SetUser(controller, 42L, "deleteuser");

        var result = await controller.DeleteAccount();

        Assert.IsInstanceOfType<OkObjectResult>(result);
        var ok = (OkObjectResult)result;
        Assert.IsInstanceOfType<AccountDeletionResponse>(ok.Value);
        var response = (AccountDeletionResponse)ok.Value;
#pragma warning disable MSTEST0037
        Assert.IsTrue(response.GracePeriodEndsAt > response.DeletionScheduledAt);
#pragma warning restore MSTEST0037

#pragma warning disable MSTEST0049
        var user = await db.Users.FindAsync(42L);
#pragma warning restore MSTEST0049
        if (user != null) Assert.IsNotNull(user.DeletionRequestedAt);
    }

    [TestMethod]
    public async Task DeleteAccount_NoUserClaim_ReturnsUnauthorized()
    {
        var (controller, _) = CreateController();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext()
        };

        var result = await controller.DeleteAccount();
        Assert.IsInstanceOfType<UnauthorizedResult>(result);
    }

    // --- User Enumeration Prevention ---

    [TestMethod]
    public async Task Login_NonExistentUser_ReturnsSameErrorAsWrongPassword()
    {
        var (controller, _) = CreateController();
        await controller.Register(new RegisterRequest("realuser", "Real User", "MySecurePass12"));

        var nonExistentResult = await controller.Login(new LoginRequest("fakeuser", "MySecurePass12"));
        var wrongPasswordResult = await controller.Login(new LoginRequest("realuser", "WrongPassword1"));

        Assert.IsInstanceOfType<UnauthorizedObjectResult>(nonExistentResult);
        var nonExistentError = (UnauthorizedObjectResult)nonExistentResult;
        Assert.IsInstanceOfType<UnauthorizedObjectResult>(wrongPasswordResult);
        var wrongPasswordError = (UnauthorizedObjectResult)wrongPasswordResult;

        // Both should return the exact same generic error message
        Assert.AreEqual(nonExistentError.Value?.ToString(), wrongPasswordError.Value?.ToString());
    }

    // --- Logout ---

    [TestMethod]
    public async Task Logout_ValidRefreshToken_RevokesIt()
    {
        var (controller, db) = CreateController();

        var registerResult = await controller.Register(new RegisterRequest("logoutuser", "Logout User", "MySecurePass12"));
        Assert.IsInstanceOfType<CreatedResult>(registerResult);
        var created = (CreatedResult)registerResult;
        Assert.IsInstanceOfType<AuthResponse>(created.Value);
        var authResponse = (AuthResponse)created.Value;

        TestDbContextFactory.SetUser(controller, authResponse.UserId, "logoutuser");

        if (authResponse.RefreshToken != null)
        {
            var result = await controller.Logout(new LogoutRequest(authResponse.RefreshToken));
            Assert.IsInstanceOfType<NoContentResult>(result);
        }

        // Verify the token is revoked
        var token = db.RefreshTokens.First(rt => rt.Token == authResponse.RefreshToken);
        Assert.IsTrue(token.IsRevoked);
    }

    [TestMethod]
    public async Task LogoutAll_RevokesAllTokens()
    {
        var (controller, db) = CreateController();

        var registerResult = await controller.Register(new RegisterRequest("logoutalluser", "Logout All User", "MySecurePass12"));
        Assert.IsInstanceOfType<CreatedResult>(registerResult);
        var created = (CreatedResult)registerResult;
        Assert.IsInstanceOfType<AuthResponse>(created.Value);
        var authResponse = (AuthResponse)created.Value;

        // Login again to create a second token
        await controller.Login(new LoginRequest("logoutalluser", "MySecurePass12"));

        TestDbContextFactory.SetUser(controller, authResponse.UserId, "logoutalluser");

        var result = await controller.LogoutAll();
        Assert.IsInstanceOfType<NoContentResult>(result);

        // All refresh tokens should be revoked
        var activeTokens = db.RefreshTokens
            .Count(rt => rt.UserId == authResponse.UserId && !rt.IsRevoked);
        Assert.AreEqual(0, activeTokens);
    }

    // --- Refresh Token Cleanup on Rotation ---

    [TestMethod]
    public async Task Refresh_CleansUpExpiredTokens()
    {
        var (controller, db) = CreateController();

        // Register to get tokens
        var registerResult = await controller.Register(new RegisterRequest("cleanuprfuser", "Cleanup RF User", "MySecurePass12"));
        Assert.IsInstanceOfType<CreatedResult>(registerResult);
        var created = (CreatedResult)registerResult;
        Assert.IsInstanceOfType<AuthResponse>(created.Value);
        var authResponse = (AuthResponse)created.Value;

        // Manually add an expired token for this user
        db.RefreshTokens.Add(new RefreshToken
        {
            Id = 999L,
            UserId = authResponse.UserId,
            Token = "expired-token",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(-1), // Already expired
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-31),
            IsRevoked = false
        });
#pragma warning disable MSTEST0049
        await db.SaveChangesAsync();
#pragma warning restore MSTEST0049

        // Refresh should clean up the expired token
        await controller.Refresh(new RefreshTokenRequest(authResponse.Token, authResponse.RefreshToken));

        var expiredToken = db.RefreshTokens.First(static rt => rt.Token == "expired-token");
        Assert.IsTrue(expiredToken.IsRevoked);
    }
}
