using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Toledo.SharedKernel.Helpers;
using ToledoVault.Data;
using ToledoVault.Models;

namespace ToledoVault.Services;

public class AdminAuthService(
    ApplicationDbContext db,
    IConfiguration config,
    ILogger<AdminAuthService> logger)
{
    private readonly PasswordHasher<AdminCredential> _hasher = new();

    public async Task<(bool success, bool mustChangePassword)> ValidateCredentialsAsync(string username, string password)
    {
        var credential = await db.AdminCredentials.FirstOrDefaultAsync(c => c.Username == username);

        if (credential is not null)
        {
            var result = _hasher.VerifyHashedPassword(credential, credential.PasswordHash, password);
            if (result is PasswordVerificationResult.Failed)
            {
                logger.LogWarning("Admin login failed: invalid password for {Username}", username);
                return (false, false);
            }

            credential.LastLoginAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();

            return (true, credential.MustChangePassword);
        }

        // Check config-based default credentials
        var configUsername = config["Admin:Username"];
        var configPassword = config["Admin:DefaultPassword"];

        if (username != configUsername || password != configPassword)
        {
            logger.LogWarning("Admin login failed: unknown username {Username}", username);
            return (false, false);
        }

        // First login — create DB credential with MustChangePassword = true
        var newCredential = new AdminCredential
        {
            Id = IdGenerator.GetNewId(),
            Username = username,
            PasswordHash = _hasher.HashPassword(null!, password),
            MustChangePassword = true,
            CreatedAt = DateTimeOffset.UtcNow,
            LastLoginAt = DateTimeOffset.UtcNow
        };
        db.AdminCredentials.Add(newCredential);
        await db.SaveChangesAsync();

        logger.LogInformation("Admin credential created for first login: {Username}", username);
        return (true, true);
    }

    public async Task<bool> ChangePasswordAsync(string username, string currentPassword, string newPassword)
    {
        var credential = await db.AdminCredentials.FirstOrDefaultAsync(c => c.Username == username);
        if (credential is null)
            return false;

        var result = _hasher.VerifyHashedPassword(credential, credential.PasswordHash, currentPassword);
        if (result is PasswordVerificationResult.Failed)
        {
            logger.LogWarning("Admin password change failed: invalid current password for {Username}", username);
            return false;
        }

        credential.PasswordHash = _hasher.HashPassword(credential, newPassword);
        credential.MustChangePassword = false;
        await db.SaveChangesAsync();

        logger.LogInformation("Admin password changed successfully for {Username}", username);
        return true;
    }

    public string GenerateAdminJwt(string username)
    {
        var secretKey = config["Jwt:SecretKey"]
                        ?? throw new InvalidOperationException("Jwt:SecretKey is not configured.");
        var issuer = config["Jwt:Issuer"];
        var audience = config["Jwt:Audience"];

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, username),
            new Claim(JwtRegisteredClaimNames.UniqueName, username),
            new Claim(ClaimTypes.Role, "admin"),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(60),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
