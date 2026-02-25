namespace ToledoMessage.Shared.Constants;

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
    public const int AesKeySize = 32; // AES-256
    public const int AesNonceSize = 12; // GCM nonce
    public const int AesTagSize = 16; // GCM tag

    // Pre-key management
    public const int OneTimePreKeyBatchSize = 100;
    public const int OneTimePreKeyLowThreshold = 10;
    public const int MaxDevicesPerUser = 10;
    public const int MaxGroupParticipants = 100;

    // Rate limits
    public const int MessageRateLimitPerMinute = 60;
    public const int SearchRateLimitPerMinute = 10;

    // HKDF info strings for domain separation
    public const string HkdfInfoRootKey = "ToledoMessage_RootKey";
    public const string HkdfInfoChainKey = "ToledoMessage_ChainKey";
    public const string HkdfInfoMessageKey = "ToledoMessage_MessageKey";
}
