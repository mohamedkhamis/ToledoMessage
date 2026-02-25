namespace ToledoMessage.Shared.DTOs;

public sealed record DeviceRegistrationRequest(
    string DeviceName,
    string IdentityPublicKeyClassical,
    string IdentityPublicKeyPostQuantum,
    string SignedPreKeyPublic,
    string SignedPreKeySignature,
    int SignedPreKeyId,
    string KyberPreKeyPublic,
    string KyberPreKeySignature,
    List<OneTimePreKeyDto> OneTimePreKeys);

public sealed record OneTimePreKeyDto(int KeyId, string PublicKey);
