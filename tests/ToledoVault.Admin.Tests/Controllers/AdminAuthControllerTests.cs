using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using ToledoVault.Controllers.Admin;
using ToledoVault.Data;
using ToledoVault.Services;
using ToledoVault.Shared.DTOs;

namespace ToledoVault.Admin.Tests.Controllers;

[TestClass]
public class AdminAuthControllerTests
{
    private static readonly Dictionary<string, string?> ConfigValues = new()
    {
        ["Admin:Username"] = "admin",
        ["Admin:DefaultPassword"] = "P@$$w0rd",
        ["Jwt:SecretKey"] = "test-secret-key-that-is-at-least-32-chars",
        ["Jwt:Issuer"] = "test",
        ["Jwt:Audience"] = "test"
    };

    private static (AdminAuthController controller, ApplicationDbContext db, AdminAuthService authService) CreateController()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new ApplicationDbContext(options);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(ConfigValues)
            .Build();

        var authService = new AdminAuthService(db, config, NullLogger<AdminAuthService>.Instance);
        var controller = new AdminAuthController(authService, NullLogger<AdminAuthController>.Instance);

        return (controller, db, authService);
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
    public async Task Login_WithValidDefaultCredentials_ReturnsTokenAndMustChangePassword()
    {
        var (controller, _, _) = CreateController();

        var result = await controller.Login(new AdminLoginRequest("admin", "Admin123!Admin123!"));

        Assert.IsInstanceOfType<OkObjectResult>(result);
        var ok = (OkObjectResult)result;
        Assert.IsInstanceOfType<AdminLoginResponse>(ok.Value);
        var response = (AdminLoginResponse)ok.Value;
        Assert.IsNotNull(response.Token);
        Assert.IsTrue(response.MustChangePassword);
    }

    [TestMethod]
    public async Task Login_WithInvalidCredentials_Returns401()
    {
        var (controller, _, _) = CreateController();

        var result = await controller.Login(new AdminLoginRequest("admin", "WrongPassword123!"));

        Assert.IsInstanceOfType<UnauthorizedObjectResult>(result);
    }

    [TestMethod]
    public async Task ChangePassword_WithValidCurrentPassword_Returns204()
    {
        var (controller, _, authService) = CreateController();

        // First login to create the credential in DB
        await authService.ValidateCredentialsAsync("admin", "Admin123!Admin123!");

        // Set the admin user claims on the controller
        SetAdminUser(controller, "admin");

        var result = await controller.ChangePassword(
            new AdminChangePasswordRequest("Admin123!Admin123!", "NewSecurePass12!"));

        Assert.IsInstanceOfType<NoContentResult>(result);
    }

    [TestMethod]
    public async Task ChangePassword_WithWrongCurrentPassword_Returns401()
    {
        var (controller, _, authService) = CreateController();

        // First login to create the credential in DB
        await authService.ValidateCredentialsAsync("admin", "Admin123!Admin123!");

        SetAdminUser(controller, "admin");

        var result = await controller.ChangePassword(
            new AdminChangePasswordRequest("WrongCurrentPw!!", "NewSecurePass12!"));

        Assert.IsInstanceOfType<UnauthorizedObjectResult>(result);
    }

    [TestMethod]
    public async Task ChangePassword_WithShortNewPassword_Returns400()
    {
        var (controller, _, authService) = CreateController();

        // First login to create the credential in DB
        await authService.ValidateCredentialsAsync("admin", "Admin123!Admin123!");

        SetAdminUser(controller, "admin");

        var result = await controller.ChangePassword(
            new AdminChangePasswordRequest("Admin123!Admin123!", "short"));

        Assert.IsInstanceOfType<BadRequestObjectResult>(result);
    }
}
