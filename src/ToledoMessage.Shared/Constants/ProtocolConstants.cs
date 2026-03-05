using System.Diagnostics.CodeAnalysis;

namespace ToledoMessage.Shared.Constants;

[SuppressMessage("ReSharper", "UnusedMember.Global")]
public static class ProtocolConstants
{
    // Key sizes (bytes)
    public const int X25519PublicKeySize = 32;
    public const int X25519PrivateKeySize = 32;
    public const int Ed25519PublicKeySize = 32;
    public const int Ed25519PrivateKeySize = 64;
    public const int Ed25519SignatureSize = 64;
    public const int MlKem768PublicKeySize = 1184;
    public const int MlKem768CiphertextSize = 1088;
    public const int MlKem768SharedSecretSize = 32;
    public const int MlDsa65PublicKeySize = 1952;
    public const int MlDsa65SignatureSize = 3309; // NIST FIPS 204 ML-DSA-65 signature size
    public const int AesKeySize = 32; // AES-256
    public const int AesNonceSize = 12; // GCM nonce
    public const int AesTagSize = 16; // GCM tag

    // Hybrid signature = 4-byte length prefix + Ed25519 signature + ML-DSA-65 signature
    public const int HybridSignatureSize = 4 + Ed25519SignatureSize + MlDsa65SignatureSize;

    // Pre-key management
    public const int OneTimePreKeyBatchSize = 100;
    public const int OneTimePreKeyLowThreshold = 10;
    public const int MaxDevicesPerUser = 10;
    public const int MaxGroupParticipants = 100;

    // Rate limits
    public const int MessageRateLimitPerMinute = 60;
    public const int SearchRateLimitPerMinute = 10;

    // Message limits
    public const int MaxMessageSizeBytes = 65_536; // 64 KB plaintext limit
    public const int MaxCiphertextSizeBytes = 67_584; // ~66 KB (64 KB plaintext + encryption overhead)
    public const int MaxMediaFileSizeBytes = 16_777_216; // 16 MB raw file limit (matches WhatsApp)
    public const int MaxMediaCiphertextSizeBytes = 25_165_824; // ~24 MB ciphertext (16 MB file → ~22 MB base64 + encryption overhead)

    // Input length limits
    public const int MaxDeviceNameLength = 64;
    public const int MaxGroupNameLength = 100;
    public const int MaxSearchQueryLength = 32;
    public const int MaxPasswordLength = 128;
    public const int MaxTimerSeconds = 31_536_000; // 1 year in seconds

    // Account lifecycle
    public const int AccountDeletionGracePeriodDays = 7;

    // Message retention
    public const int UndeliveredMessageRetentionDays = 90;

    // HKDF info strings for domain separation
    public const string HkdfInfoRootKey = "ToledoMessage_RootKey";
    public const string HkdfInfoChainKey = "ToledoMessage_ChainKey";
    public const string HkdfInfoMessageKey = "ToledoMessage_MessageKey";
}
