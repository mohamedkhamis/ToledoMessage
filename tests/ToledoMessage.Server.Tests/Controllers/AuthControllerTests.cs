using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using ToledoMessage.Controllers;
using ToledoMessage.Models;
using ToledoMessage.Services;
using ToledoMessage.Shared.DTOs;

namespace ToledoMessage.Server.Tests.Controllers;

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

    [Fact]
    public async Task Register_ValidRequest_ReturnsCreated()
    {
        var (controller, _) = CreateController();
        var request = new RegisterRequest("testuser01", "MySecurePass12");

        var result = await controller.Register(request);

        var created = Assert.IsType<CreatedResult>(result);
        var response = Assert.IsType<AuthResponse>(created.Value);
        Assert.Equal("testuser01", response.DisplayName);
        Assert.NotNull(response.Token);
        Assert.NotNull(response.RefreshToken);
    }

    [Fact]
    public async Task Register_EmptyDisplayName_ReturnsBadRequest()
    {
        var (controller, _) = CreateController();
        var result = await controller.Register(new RegisterRequest("", "MySecurePass12"));
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Register_ShortDisplayName_ReturnsBadRequest()
    {
        var (controller, _) = CreateController();
        var result = await controller.Register(new RegisterRequest("ab", "MySecurePass12"));
        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("3 and 32", bad.Value!.ToString());
    }

    [Fact]
    public async Task Register_InvalidDisplayNameChars_ReturnsBadRequest()
    {
        var (controller, _) = CreateController();
        var result = await controller.Register(new RegisterRequest("test user!", "MySecurePass12"));
        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("letters, digits", bad.Value!.ToString());
    }

    [Fact]
    public async Task Register_ShortPassword_ReturnsBadRequest()
    {
        var (controller, _) = CreateController();
        var result = await controller.Register(new RegisterRequest("validuser", "short"));
        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("12 characters", bad.Value!.ToString());
    }

    [Fact]
    public async Task Register_DuplicateDisplayName_ReturnsConflict()
    {
        var (controller, db) = CreateController();
        await TestDbContextFactory.SeedUser(db, 1m, "existing");

        var result = await controller.Register(new RegisterRequest("existing", "MySecurePass12"));
        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsOk()
    {
        var (controller, _) = CreateController();

        // Register first
        await controller.Register(new RegisterRequest("loginuser", "MySecurePass12"));

        var result = await controller.Login(new LoginRequest("loginuser", "MySecurePass12"));

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<AuthResponse>(ok.Value);
        Assert.Equal("loginuser", response.DisplayName);
        Assert.NotNull(response.Token);
        Assert.NotNull(response.RefreshToken);
    }

    [Fact]
    public async Task Login_WrongPassword_ReturnsUnauthorized()
    {
        var (controller, _) = CreateController();
        await controller.Register(new RegisterRequest("wrongpw", "MySecurePass12"));

        var result = await controller.Login(new LoginRequest("wrongpw", "WrongPassword1"));
        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Login_NonExistentUser_ReturnsUnauthorized()
    {
        var (controller, _) = CreateController();
        var result = await controller.Login(new LoginRequest("nonexistent", "MySecurePass12"));
        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Login_DeactivatedUser_ReturnsUnauthorized_WithGenericError()
    {
        var (controller, db) = CreateController();

        // Register and then deactivate
        await controller.Register(new RegisterRequest("deactuser", "MySecurePass12"));
        var user = db.Users.First(u => u.DisplayName == "deactuser");
        user.IsActive = false;
        await db.SaveChangesAsync();

        var result = await controller.Login(new LoginRequest("deactuser", "MySecurePass12"));
        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        // Should return the same generic error to prevent user enumeration
        Assert.Contains("Invalid display name or password", unauthorized.Value!.ToString());
    }

    [Fact]
    public async Task Login_CancelsPendingDeletion()
    {
        var (controller, db) = CreateController();

        await controller.Register(new RegisterRequest("deluser", "MySecurePass12"));
        var user = db.Users.First(u => u.DisplayName == "deluser");
        user.DeletionRequestedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        await controller.Login(new LoginRequest("deluser", "MySecurePass12"));

        var refreshedUser = db.Users.First(u => u.DisplayName == "deluser");
        Assert.Null(refreshedUser.DeletionRequestedAt);
    }

    [Fact]
    public async Task Refresh_ValidTokens_ReturnsNewTokenPair()
    {
        var (controller, db) = CreateController();

        // Register to get tokens
        var registerResult = await controller.Register(new RegisterRequest("refreshuser", "MySecurePass12"));
        var created = Assert.IsType<CreatedResult>(registerResult);
        var authResponse = Assert.IsType<AuthResponse>(created.Value);

        var refreshResult = await controller.Refresh(
            new RefreshTokenRequest(authResponse.Token, authResponse.RefreshToken!));

        var ok = Assert.IsType<OkObjectResult>(refreshResult);
        var response = Assert.IsType<RefreshTokenResponse>(ok.Value);
        Assert.NotNull(response.Token);
        Assert.NotNull(response.RefreshToken);
        Assert.NotEqual(authResponse.RefreshToken, response.RefreshToken); // Rotated
    }

    [Fact]
    public async Task Refresh_InvalidAccessToken_ReturnsUnauthorized()
    {
        var (controller, _) = CreateController();

        var result = await controller.Refresh(
            new RefreshTokenRequest("invalid-jwt", "some-refresh-token"));

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Refresh_InvalidRefreshToken_ReturnsUnauthorized()
    {
        var (controller, _) = CreateController();

        var registerResult = await controller.Register(new RegisterRequest("rftestuser", "MySecurePass12"));
        var created = Assert.IsType<CreatedResult>(registerResult);
        var authResponse = Assert.IsType<AuthResponse>(created.Value);

        var result = await controller.Refresh(
            new RefreshTokenRequest(authResponse.Token, "wrong-refresh-token"));

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Refresh_RevokedRefreshToken_ReturnsUnauthorized()
    {
        var (controller, db) = CreateController();

        var registerResult = await controller.Register(new RegisterRequest("revoketest", "MySecurePass12"));
        var created = Assert.IsType<CreatedResult>(registerResult);
        var authResponse = Assert.IsType<AuthResponse>(created.Value);

        // Revoke all tokens
        foreach (var token in db.RefreshTokens) token.IsRevoked = true;
        await db.SaveChangesAsync();

        var result = await controller.Refresh(
            new RefreshTokenRequest(authResponse.Token, authResponse.RefreshToken!));

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task DeleteAccount_Authenticated_ReturnsOk()
    {
        var (controller, db) = CreateController();
        await TestDbContextFactory.SeedUser(db, 42m, "deleteuser");
        TestDbContextFactory.SetUser(controller, 42m, "deleteuser");

        var result = await controller.DeleteAccount();

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<AccountDeletionResponse>(ok.Value);
        Assert.True(response.GracePeriodEndsAt > response.DeletionScheduledAt);

        var user = await db.Users.FindAsync(42m);
        Assert.NotNull(user!.DeletionRequestedAt);
    }

    [Fact]
    public async Task DeleteAccount_NoUserClaim_ReturnsUnauthorized()
    {
        var (controller, _) = CreateController();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext()
        };

        var result = await controller.DeleteAccount();
        Assert.IsType<UnauthorizedResult>(result);
    }

    // --- User Enumeration Prevention ---

    [Fact]
    public async Task Login_NonExistentUser_ReturnsSameErrorAsWrongPassword()
    {
        var (controller, _) = CreateController();
        await controller.Register(new RegisterRequest("realuser", "MySecurePass12"));

        var nonExistentResult = await controller.Login(new LoginRequest("fakeuser", "MySecurePass12"));
        var wrongPasswordResult = await controller.Login(new LoginRequest("realuser", "WrongPassword1"));

        var nonExistentError = Assert.IsType<UnauthorizedObjectResult>(nonExistentResult);
        var wrongPasswordError = Assert.IsType<UnauthorizedObjectResult>(wrongPasswordResult);

        // Both should return the exact same generic error message
        Assert.Equal(nonExistentError.Value!.ToString(), wrongPasswordError.Value!.ToString());
    }

    // --- Logout ---

    [Fact]
    public async Task Logout_ValidRefreshToken_RevokesIt()
    {
        var (controller, db) = CreateController();

        var registerResult = await controller.Register(new RegisterRequest("logoutuser", "MySecurePass12"));
        var created = Assert.IsType<CreatedResult>(registerResult);
        var authResponse = Assert.IsType<AuthResponse>(created.Value);

        TestDbContextFactory.SetUser(controller, authResponse.UserId, "logoutuser");

        var result = await controller.Logout(new ToledoMessage.Shared.DTOs.LogoutRequest(authResponse.RefreshToken!));
        Assert.IsType<NoContentResult>(result);

        // Verify the token is revoked
        var token = db.RefreshTokens.First(rt => rt.Token == authResponse.RefreshToken);
        Assert.True(token.IsRevoked);
    }

    [Fact]
    public async Task LogoutAll_RevokesAllTokens()
    {
        var (controller, db) = CreateController();

        var registerResult = await controller.Register(new RegisterRequest("logoutalluser", "MySecurePass12"));
        var created = Assert.IsType<CreatedResult>(registerResult);
        var authResponse = Assert.IsType<AuthResponse>(created.Value);

        // Login again to create a second token
        await controller.Login(new LoginRequest("logoutalluser", "MySecurePass12"));

        TestDbContextFactory.SetUser(controller, authResponse.UserId, "logoutalluser");

        var result = await controller.LogoutAll();
        Assert.IsType<NoContentResult>(result);

        // All refresh tokens should be revoked
        var activeTokens = db.RefreshTokens
            .Where(rt => rt.UserId == authResponse.UserId && !rt.IsRevoked)
            .Count();
        Assert.Equal(0, activeTokens);
    }

    // --- Refresh Token Cleanup on Rotation ---

    [Fact]
    public async Task Refresh_CleansUpExpiredTokens()
    {
        var (controller, db) = CreateController();

        // Register to get tokens
        var registerResult = await controller.Register(new RegisterRequest("cleanuprfuser", "MySecurePass12"));
        var created = Assert.IsType<CreatedResult>(registerResult);
        var authResponse = Assert.IsType<AuthResponse>(created.Value);

        // Manually add an expired token for this user
        db.RefreshTokens.Add(new RefreshToken
        {
            Id = 999m,
            UserId = authResponse.UserId,
            Token = "expired-token",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(-1), // Already expired
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-31),
            IsRevoked = false
        });
        await db.SaveChangesAsync();

        // Refresh should clean up the expired token
        await controller.Refresh(new RefreshTokenRequest(authResponse.Token, authResponse.RefreshToken!));

        var expiredToken = db.RefreshTokens.First(rt => rt.Token == "expired-token");
        Assert.True(expiredToken.IsRevoked);
    }
}
