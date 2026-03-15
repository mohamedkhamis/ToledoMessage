using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ToledoVault.Services;
using ToledoVault.Shared.DTOs;

namespace ToledoVault.Controllers.Admin;

[ApiController]
[Route("api/admin/auth")]
public class AdminAuthController(
    AdminAuthService adminAuthService,
    ILogger<AdminAuthController> logger) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] AdminLoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest("Username and password are required.");

        var (success, mustChangePassword) = await adminAuthService.ValidateCredentialsAsync(request.Username, request.Password);
        if (!success)
        {
            logger.LogWarning("Admin login failed for {Username}", request.Username);
            return Unauthorized("Invalid credentials.");
        }

        var token = adminAuthService.GenerateAdminJwt(request.Username);
        logger.LogInformation("Admin login successful: {Username}", request.Username);

        return Ok(new AdminLoginResponse(token, mustChangePassword));
    }

    [HttpPost("change-password")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> ChangePassword([FromBody] AdminChangePasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 12)
            return BadRequest("New password must be at least 12 characters.");

        if (request.NewPassword.Length > 128)
            return BadRequest("New password must be at most 128 characters.");

        var username = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (username is null)
            return Unauthorized();

        var success = await adminAuthService.ChangePasswordAsync(username, request.CurrentPassword, request.NewPassword);
        if (!success)
            return Unauthorized("Current password is incorrect.");

        logger.LogInformation("Admin password changed: {Username}", username);
        return NoContent();
    }
}
