using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Toledo.SharedKernel.Helpers;
using ToledoMessage.Data;
using ToledoMessage.Models;
using ToledoMessage.Services;
using ToledoMessage.Shared.DTOs;

namespace ToledoMessage.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(
    ApplicationDbContext db,
    IPasswordHasher<User> passwordHasher,
    IConfiguration configuration,
    AccountDeletionService accountDeletionService)
    : BaseApiController
{
    private static readonly Regex UsernameRegex = new("^[a-zA-Z0-9_-]+$", RegexOptions.Compiled);
    // ReSharper disable  RemoveRedundantBraces

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username))
            return BadRequest("Username is required.");

        if (request.Username.Length is < 3 or > 32)
            return BadRequest("Username must be between 3 and 32 characters.");

        if (!UsernameRegex.IsMatch(request.Username))
            return BadRequest("Username may only contain letters, digits, hyphens, and underscores.");

        if (string.IsNullOrWhiteSpace(request.DisplayName))
            return BadRequest("Display name is required.");

        if (request.DisplayName.Length is < 1 or > 50)
            return BadRequest("Display name must be between 1 and 50 characters.");

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 12)
            return BadRequest("Password must be at least 12 characters.");

        if (request.Password.Length > Shared.Constants.ProtocolConstants.MaxPasswordLength)
            return BadRequest($"Password must not exceed {Shared.Constants.ProtocolConstants.MaxPasswordLength} characters.");

        var exists = await db.Users.AnyAsync(u => u.Username == request.Username);
        if (exists)
            return Conflict("A user with this username already exists.");

        var user = new User
        {
            Id = DecimalTools.GetNewId(),
            Username = request.Username,
            DisplayName = request.DisplayName,
            CreatedAt = DateTimeOffset.UtcNow,
            IsActive = true
        };

        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);

        db.Users.Add(user);
        await db.SaveChangesAsync();

        var accessToken = GenerateJwtToken(user);
        var refreshToken = await CreateRefreshTokenAsync(user.Id);

        return Created(string.Empty, new AuthResponse(user.Id, user.Username, user.DisplayName, accessToken, refreshToken));
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        const string genericError = "Invalid username or password.";

        var user = await db.Users.FirstOrDefaultAsync(u => u.Username == request.Username);
        if (user == null)
            return Unauthorized(genericError);

        if (!user.IsActive)
            return Unauthorized(genericError);

        var result = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (result == PasswordVerificationResult.Failed)
            return Unauthorized(genericError);

        // Cancel pending deletion on successful login (FR-020 grace period)
        if (user.DeletionRequestedAt is not null)
        {
            await accountDeletionService.CancelDeletionAsync(user.Id);
        }

        var accessToken = GenerateJwtToken(user);
        var refreshToken = await CreateRefreshTokenAsync(user.Id);

        return Ok(new AuthResponse(user.Id, user.Username, user.DisplayName, accessToken, refreshToken));
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request)
    {
        // Validate the expired access token to extract claims (skip lifetime check)
        var principal = GetPrincipalFromExpiredToken(request.AccessToken);
        if (principal == null)
            return Unauthorized("Invalid access token.");

        var userIdClaim = principal.FindFirst(JwtRegisteredClaimNames.Sub)
                          ?? principal.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !decimal.TryParse(userIdClaim.Value, out var userId))
            return Unauthorized("Invalid access token claims.");

        var storedToken = await db.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == request.RefreshToken && rt.UserId == userId);

        if (storedToken == null || storedToken.IsRevoked || storedToken.ExpiresAt <= DateTimeOffset.UtcNow)
            return Unauthorized("Invalid or expired refresh token.");

        // Revoke the old refresh token (rotation)
        storedToken.IsRevoked = true;

        // Clean up expired tokens for this user to prevent accumulation
        var expiredTokens = await db.RefreshTokens
            .Where(rt => rt.UserId == userId && rt.ExpiresAt <= DateTimeOffset.UtcNow && !rt.IsRevoked)
            .ToListAsync();
        foreach (var expired in expiredTokens)
        {
            expired.IsRevoked = true;
        }

        var user = await db.Users.FindAsync(userId);
        if (user is not { IsActive: true })
            return Unauthorized("User account not found or deactivated.");

        var newAccessToken = GenerateJwtToken(user);
        var newRefreshToken = await CreateRefreshTokenAsync(user.Id);

        return Ok(new RefreshTokenResponse(newAccessToken, newRefreshToken));
    }

    /// <summary>
    /// Logout: revokes the provided refresh token.
    /// </summary>
    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest request)
    {
        var userId = GetUserId();

        var token = await db.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == request.RefreshToken && rt.UserId == userId && !rt.IsRevoked);

        // ReSharper disable  InvertIf
        if (token is not null)
        {
            token.IsRevoked = true;
            await db.SaveChangesAsync();
        }

        return NoContent();
    }

    /// <summary>
    /// Logout from all devices: revokes all refresh tokens for the user.
    /// </summary>
    [Authorize]
    [HttpPost("logout-all")]
    public async Task<IActionResult> LogoutAll()
    {
        var userId = GetUserId();

        var tokens = await db.RefreshTokens
            .Where(rt => rt.UserId == userId && !rt.IsRevoked)
            .ToListAsync();

        foreach (var token in tokens)
        {
            token.IsRevoked = true;
        }

        await db.SaveChangesAsync();

        return NoContent();
    }

    [Authorize]
    [HttpDelete("account")]
    public async Task<IActionResult> DeleteAccount()
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var deletionRequestedAt = await accountDeletionService.InitiateDeletionAsync(userId);

        return Ok(new AccountDeletionResponse(
            deletionRequestedAt,
            deletionRequestedAt.AddDays(Shared.Constants.ProtocolConstants.AccountDeletionGracePeriodDays)));
    }

    private string GenerateJwtToken(User user)
    {
        var secretKey = configuration["Jwt:SecretKey"]
                        ?? throw new InvalidOperationException("Jwt:SecretKey is not configured.");
        var issuer = configuration["Jwt:Issuer"];
        var audience = configuration["Jwt:Audience"];
        var expiryMinutes = int.Parse(configuration["Jwt:ExpiryMinutes"] ?? "15");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString(CultureInfo.InvariantCulture)),
            new Claim(JwtRegisteredClaimNames.UniqueName, user.Username),
            new Claim(JwtRegisteredClaimNames.Name, user.DisplayName),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer,
            audience,
            claims,
            expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private async Task<string> CreateRefreshTokenAsync(decimal userId)
    {
        var tokenValue = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

        var refreshToken = new RefreshToken
        {
            Id = DecimalTools.GetNewId(),
            UserId = userId,
            Token = tokenValue,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.RefreshTokens.Add(refreshToken);
        await db.SaveChangesAsync();

        return tokenValue;
    }

    private ClaimsPrincipal? GetPrincipalFromExpiredToken(string token)
    {
        var secretKey = configuration["Jwt:SecretKey"]
                        ?? throw new InvalidOperationException("Jwt:SecretKey is not configured.");

        var tokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = false, // Allow expired tokens
            ValidateIssuerSigningKey = true,
            ValidIssuer = configuration["Jwt:Issuer"],
            ValidAudience = configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
        };

        try
        {
            var principal = new JwtSecurityTokenHandler()
                .ValidateToken(token, tokenValidationParameters, out var securityToken);

            if (securityToken is not JwtSecurityToken jwtToken ||
                !jwtToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
                return null;

            return principal;
        }
        catch
        {
            return null;
        }
    }
}
