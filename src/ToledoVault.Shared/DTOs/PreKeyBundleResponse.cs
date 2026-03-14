namespace ToledoVault.Shared.DTOs;

public sealed record PreKeyBundleResponse(
    long DeviceId,
    string IdentityPublicKeyClassical,
    string IdentityPublicKeyPostQuantum,
    string SignedPreKeyPublic,
    string SignedPreKeySignature,
    int SignedPreKeyId,
    string KyberPreKeyPublic,
    string KyberPreKeySignature,
    OneTimePreKeyDto? OneTimePreKey);
