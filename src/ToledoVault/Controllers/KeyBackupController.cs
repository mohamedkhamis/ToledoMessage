using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Toledo.SharedKernel.Helpers;
using ToledoVault.Data;
using ToledoVault.Models;
using ToledoVault.Shared.DTOs;
// ReSharper disable InvertIf

namespace ToledoVault.Controllers;

[ApiController]
[Route("api/keys/backup")]
[Authorize]
public class KeyBackupController(ApplicationDbContext db) : BaseApiController
{
    private const int MaxBlobSizeBytes = 50 * 1024; // 50KB

    [HttpPost]
    public async Task<IActionResult> UploadBackup([FromBody] UploadKeyBackupRequest request)
    {
        byte[] blob;
        byte[] salt;
        byte[] nonce;

        try
        {
            blob = Convert.FromBase64String(request.EncryptedBlob);
            salt = Convert.FromBase64String(request.Salt);
            nonce = Convert.FromBase64String(request.Nonce);
        }
        catch (FormatException)
        {
            return BadRequest("Invalid base64 encoding.");
        }

        if (blob.Length > MaxBlobSizeBytes)
            return BadRequest("Encrypted blob exceeds maximum size of 50KB.");

        if (salt.Length != 16)
            return BadRequest("Salt must be 16 bytes.");

        if (nonce.Length != 12)
            return BadRequest("Nonce must be 12 bytes.");

        var userId = GetUserId();
        var existing = await db.EncryptedKeyBackups.FirstOrDefaultAsync(b => b.UserId == userId);

        if (existing is not null)
        {
            existing.EncryptedBlob = blob;
            existing.Salt = salt;
            existing.Nonce = nonce;
            existing.Version++;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
        }
        else
        {
            db.EncryptedKeyBackups.Add(new EncryptedKeyBackup
            {
                Id = IdGenerator.GetNewId(),
                UserId = userId,
                EncryptedBlob = blob,
                Salt = salt,
                Nonce = nonce,
                Version = 1,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        }

        await db.SaveChangesAsync();
        return Ok();
    }

    [HttpGet]
    public async Task<IActionResult> GetBackup()
    {
        var userId = GetUserId();
        var backup = await db.EncryptedKeyBackups
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.UserId == userId);

        if (backup is null)
            return NotFound();

        return Ok(new KeyBackupResponse(
            Convert.ToBase64String(backup.EncryptedBlob),
            Convert.ToBase64String(backup.Salt),
            Convert.ToBase64String(backup.Nonce),
            backup.Version,
            backup.UpdatedAt));
    }

    [HttpDelete]
    public async Task<IActionResult> DeleteBackup()
    {
        var userId = GetUserId();
        var backup = await db.EncryptedKeyBackups.FirstOrDefaultAsync(b => b.UserId == userId);

        if (backup is not null)
        {
            db.EncryptedKeyBackups.Remove(backup);
            await db.SaveChangesAsync();
        }

        return NoContent();
    }
}
