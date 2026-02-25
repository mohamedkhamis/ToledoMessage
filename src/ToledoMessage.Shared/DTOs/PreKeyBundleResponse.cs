namespace ToledoMessage.Shared.DTOs;

public sealed record PreKeyBundleResponse(
    decimal DeviceId,
    string IdentityPublicKeyClassical,
    string IdentityPublicKeyPostQuantum,
    string SignedPreKeyPublic,
    string SignedPreKeySignature,
    int SignedPreKeyId,
    string KyberPreKeyPublic,
    string KyberPreKeySignature,
    OneTimePreKeyDto? OneTimePreKey);
