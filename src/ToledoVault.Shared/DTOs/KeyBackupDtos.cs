namespace ToledoVault.Shared.DTOs;

public sealed record UploadKeyBackupRequest(
    string EncryptedBlob,
    string Salt,
    string Nonce);

public sealed record KeyBackupResponse(
    string EncryptedBlob,
    string Salt,
    string Nonce,
    int Version,
    DateTimeOffset? UpdatedAt = null);
