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
using ToledoVault.Data;
using ToledoVault.Models;
using ToledoVault.Services;
using ToledoVault.Shared.DTOs;

namespace ToledoVault.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(
    ApplicationDbContext db,
    IPasswordHasher<User> passwordHasher,
    IConfiguration configuration,
    AccountDeletionService accountDeletionService,
    PreKeyService preKeyService,
    TokenBlacklistService tokenBlacklist,
    ILogger<AuthController> logger)
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

        if (request.DisplayNameSecondary is { Length: > 50 })
            return BadRequest("Secondary display name must not exceed 50 characters.");

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 12)
            return BadRequest("Password must be at least 12 characters.");

        if (request.Password.Length > Shared.Constants.ProtocolConstants.MaxPasswordLength)
            return BadRequest($"Password must not exceed {Shared.Constants.ProtocolConstants.MaxPasswordLength} characters.");

        var exists = await db.Users.AnyAsync(u => u.Username == request.Username);
        if (exists)
            return Conflict("This username is not available.");

        var user = new User
        {
            Id = IdGenerator.GetNewId(),
            Username = request.Username,
            DisplayName = request.DisplayName,
            DisplayNameSecondary = string.IsNullOrWhiteSpace(request.DisplayNameSecondary) ? null : request.DisplayNameSecondary.Trim(),
            CreatedAt = DateTimeOffset.UtcNow,
            IsActive = true
        };

        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);

        db.Users.Add(user);
        await db.SaveChangesAsync();

        logger.LogInformation("User registered. UserId={UserId}, Username={Username}", user.Id, user.Username);

        var accessToken = GenerateJwtToken(user);
        var refreshToken = await CreateRefreshTokenAsync(user.Id, request.RememberMe);

        return Created(string.Empty, new AuthResponse(user.Id, user.Username, user.DisplayName, accessToken, refreshToken, user.DisplayNameSecondary));
    }

    /// <summary>
    /// Combined registration: creates user + device in a single atomic transaction.
    /// If any step fails, the entire operation is rolled back.
    /// </summary>
    [HttpPost("register-with-device")]
    public async Task<IActionResult> RegisterWithDevice([FromBody] RegisterWithDeviceRequest request)
    {
        // --- User validation ---
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
        if (request.DisplayNameSecondary is { Length: > 50 })
            return BadRequest("Secondary display name must not exceed 50 characters.");
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 12)
            return BadRequest("Password must be at least 12 characters.");
        if (request.Password.Length > Shared.Constants.ProtocolConstants.MaxPasswordLength)
            return BadRequest($"Password must not exceed {Shared.Constants.ProtocolConstants.MaxPasswordLength} characters.");

        var exists = await db.Users.AnyAsync(u => u.Username == request.Username);
        if (exists)
            return Conflict("This username is not available.");

        // --- Device validation ---
        var dev = request.Device;
        if (string.IsNullOrWhiteSpace(dev.DeviceName) || dev.DeviceName.Length > Shared.Constants.ProtocolConstants.MaxDeviceNameLength)
            return BadRequest($"Device name must be between 1 and {Shared.Constants.ProtocolConstants.MaxDeviceNameLength} characters.");

        byte[] classicalIdentityKey, pqIdentityKey, signedPreKeyPublic, signedPreKeySig, kyberPreKeyPublic, kyberPreKeySig;
        try
        {
            classicalIdentityKey = Convert.FromBase64String(dev.IdentityPublicKeyClassical);
            pqIdentityKey = Convert.FromBase64String(dev.IdentityPublicKeyPostQuantum);
            signedPreKeyPublic = Convert.FromBase64String(dev.SignedPreKeyPublic);
            signedPreKeySig = Convert.FromBase64String(dev.SignedPreKeySignature);
            kyberPreKeyPublic = Convert.FromBase64String(dev.KyberPreKeyPublic);
            kyberPreKeySig = Convert.FromBase64String(dev.KyberPreKeySignature);
        }
        catch (FormatException)
        {
            return BadRequest("One or more key fields contain invalid Base64.");
        }

        if (classicalIdentityKey.Length != Shared.Constants.ProtocolConstants.Ed25519PublicKeySize)
            return BadRequest("Invalid identity public key (classical) size.");
        if (pqIdentityKey.Length != Shared.Constants.ProtocolConstants.MlDsa65PublicKeySize)
            return BadRequest("Invalid identity public key (post-quantum) size.");
        if (signedPreKeyPublic.Length != Shared.Constants.ProtocolConstants.X25519PublicKeySize)
            return BadRequest("Invalid signed pre-key public key size.");
        if (signedPreKeySig.Length != Shared.Constants.ProtocolConstants.HybridSignatureSize)
            return BadRequest("Invalid signed pre-key signature size.");
        if (kyberPreKeyPublic.Length != Shared.Constants.ProtocolConstants.MlKem768PublicKeySize)
            return BadRequest("Invalid Kyber pre-key public key size.");
        if (kyberPreKeySig.Length != Shared.Constants.ProtocolConstants.HybridSignatureSize)
            return BadRequest("Invalid Kyber pre-key signature size.");

        // --- Create user ---
        var user = new User
        {
            Id = IdGenerator.GetNewId(),
            Username = request.Username,
            DisplayName = request.DisplayName,
            DisplayNameSecondary = string.IsNullOrWhiteSpace(request.DisplayNameSecondary) ? null : request.DisplayNameSecondary.Trim(),
            CreatedAt = DateTimeOffset.UtcNow,
            IsActive = true
        };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        // --- Create device ---
        var device = new Device
        {
            Id = IdGenerator.GetNewId(),
            UserId = user.Id,
            DeviceName = dev.DeviceName,
            IdentityPublicKeyClassical = classicalIdentityKey,
            IdentityPublicKeyPostQuantum = pqIdentityKey,
            SignedPreKeyPublic = signedPreKeyPublic,
            SignedPreKeySignature = signedPreKeySig,
            SignedPreKeyId = dev.SignedPreKeyId,
            KyberPreKeyPublic = kyberPreKeyPublic,
            KyberPreKeySignature = kyberPreKeySig,
            CreatedAt = DateTimeOffset.UtcNow,
            LastSeenAt = DateTimeOffset.UtcNow,
            IsActive = true
        };
        db.Devices.Add(device);
        await db.SaveChangesAsync();

        // --- Store pre-keys ---
        if (dev.OneTimePreKeys is { Count: > 0 and <= Shared.Constants.ProtocolConstants.OneTimePreKeyBatchSize })
        {
            await preKeyService.StoreOneTimePreKeys(device.Id, dev.OneTimePreKeys);
        }

        // --- Generate tokens (bound to the new device) ---
        var accessToken = GenerateJwtToken(user);
        var refreshToken = await CreateRefreshTokenAsync(user.Id, request.RememberMe, device.Id);

        return Created(string.Empty, new RegisterWithDeviceResponse(
            user.Id, user.Username, user.DisplayName, accessToken, refreshToken, device.Id, user.DisplayNameSecondary));
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Username == request.Username);
        if (user == null)
        {
            logger.LogWarning("Login failed: user not found. Username={Username}", request.Username);
            return Unauthorized("USER_NOT_FOUND");
        }

        if (!user.IsActive)
        {
            logger.LogWarning("Login failed: account deactivated. UserId={UserId}, Username={Username}", user.Id, user.Username);
            return Unauthorized("ACCOUNT_DEACTIVATED");
        }

        var result = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (result == PasswordVerificationResult.Failed)
        {
            logger.LogWarning("Login failed: wrong password. UserId={UserId}, Username={Username}", user.Id, user.Username);
            return Unauthorized("WRONG_PASSWORD");
        }

        // Cancel pending deletion on successful login (FR-020 grace period)
        if (user.DeletionRequestedAt is not null)
        {
            await accountDeletionService.CancelDeletionAsync(user.Id);
        }

        logger.LogInformation("Login succeeded. UserId={UserId}, Username={Username}", user.Id, user.Username);

        var accessToken = GenerateJwtToken(user);
        var refreshToken = await CreateRefreshTokenAsync(user.Id, request.RememberMe);

        return Ok(new AuthResponse(user.Id, user.Username, user.DisplayName, accessToken, refreshToken, user.DisplayNameSecondary));
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
        if (userIdClaim == null || !long.TryParse(userIdClaim.Value, out var userId))
            return Unauthorized("Invalid access token claims.");

        var storedToken = await db.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == request.RefreshToken && rt.UserId == userId);

        if (storedToken == null || storedToken.IsRevoked || storedToken.ExpiresAt <= DateTimeOffset.UtcNow)
            return Unauthorized("Invalid or expired refresh token.");

        // S-06 Fix: Device-bound tokens MUST include a matching device ID
        if (storedToken.DeviceId is not null)
        {
            if (request.DeviceId is null || storedToken.DeviceId != request.DeviceId)
                return Unauthorized("Refresh token is bound to a specific device.");
        }

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
        var newRefreshToken = await CreateRefreshTokenAsync(user.Id, storedToken.IsPersistent, storedToken.DeviceId);

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

        // S-11: Blacklist the current access token so it's rejected immediately
        BlacklistCurrentAccessToken();

        var token = await db.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == request.RefreshToken && rt.UserId == userId && !rt.IsRevoked);

        // ReSharper disable  InvertIf
        if (token is not null)
        {
            token.IsRevoked = true;
            await db.SaveChangesAsync();
        }

        logger.LogInformation("User logged out. UserId={UserId}", userId);

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

        // S-11: Blacklist the current access token so it's rejected immediately
        BlacklistCurrentAccessToken();

        var tokens = await db.RefreshTokens
            .Where(rt => rt.UserId == userId && !rt.IsRevoked)
            .ToListAsync();

        foreach (var token in tokens)
        {
            token.IsRevoked = true;
        }

        await db.SaveChangesAsync();

        logger.LogInformation("User logged out from all devices. UserId={UserId}, RevokedTokens={Count}", userId, tokens.Count);

        return NoContent();
    }

    [Authorize]
    [HttpDelete("account")]
    public async Task<IActionResult> DeleteAccount()
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var deletionRequestedAt = await accountDeletionService.InitiateDeletionAsync(userId);

        logger.LogWarning("Account deletion requested. UserId={UserId}, DeletionAt={DeletionAt}", userId, deletionRequestedAt);

        return Ok(new AccountDeletionResponse(
            deletionRequestedAt,
            deletionRequestedAt.AddDays(Shared.Constants.ProtocolConstants.AccountDeletionGracePeriodDays)));
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CurrentPassword))
            return BadRequest("Current password is required.");

        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 12)
            return BadRequest("New password must be at least 12 characters.");

        if (request.NewPassword.Length > Shared.Constants.ProtocolConstants.MaxPasswordLength)
            return BadRequest($"New password must not exceed {Shared.Constants.ProtocolConstants.MaxPasswordLength} characters.");

        var userId = GetUserId();
        var user = await db.Users.FindAsync(userId);
        if (user is not { IsActive: true })
            return Unauthorized("User account not found or deactivated.");

        var result = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.CurrentPassword);
        if (result == PasswordVerificationResult.Failed)
        {
            logger.LogWarning("Password change failed: wrong current password. UserId={UserId}", userId);
            return Unauthorized("Current password is incorrect.");
        }

        user.PasswordHash = passwordHasher.HashPassword(user, request.NewPassword);

        // Revoke all refresh tokens — compromised password means all sessions are suspect
        var tokens = await db.RefreshTokens
            .Where(rt => rt.UserId == userId && !rt.IsRevoked)
            .ToListAsync();
        foreach (var token in tokens)
        {
            token.IsRevoked = true;
        }

        await db.SaveChangesAsync();

        // Blacklist the current access token so the caller must re-authenticate
        BlacklistCurrentAccessToken();

        logger.LogInformation("Password changed successfully, {Count} sessions revoked. UserId={UserId}", tokens.Count, userId);

        return NoContent();
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

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString(CultureInfo.InvariantCulture)),
            new(JwtRegisteredClaimNames.UniqueName, user.Username),
            new(JwtRegisteredClaimNames.Name, user.DisplayName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        if (!string.IsNullOrEmpty(user.DisplayNameSecondary))
            claims.Add(new Claim("name2", user.DisplayNameSecondary));

        var token = new JwtSecurityToken(
            issuer,
            audience,
            claims,
            expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private async Task<string> CreateRefreshTokenAsync(long userId, bool isPersistent = true, long? deviceId = null)
    {
        var tokenValue = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var expiryDays = isPersistent ? 90 : 1; // 90 days for "remember me", 24 hours for session-only

        var refreshToken = new RefreshToken
        {
            Id = IdGenerator.GetNewId(),
            UserId = userId,
            Token = tokenValue,
            DeviceId = deviceId,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(expiryDays),
            CreatedAt = DateTimeOffset.UtcNow,
            IsPersistent = isPersistent
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

    /// <summary>
    /// Extracts the JTI and expiry from the current bearer token and adds it to the blacklist.
    /// </summary>
    private void BlacklistCurrentAccessToken()
    {
        var jti = User.FindFirstValue(JwtRegisteredClaimNames.Jti);
        if (jti is null) return;

        var expClaim = User.FindFirstValue(JwtRegisteredClaimNames.Exp);
        var expiresAt = expClaim is not null && long.TryParse(expClaim, out var exp)
            ? DateTimeOffset.FromUnixTimeSeconds(exp)
            : DateTimeOffset.UtcNow.AddMinutes(15); // Fallback to max token lifetime

        tokenBlacklist.RevokeToken(jti, expiresAt);
    }
}
