using System.Diagnostics.CodeAnalysis;
using BenchmarkDotNet.Attributes;
using ToledoVault.Crypto.Classical;
using ToledoVault.Crypto.Hybrid;
using ToledoVault.Crypto.KeyManagement;
using ToledoVault.Crypto.PostQuantum;
using ToledoVault.Crypto.Protocol;

namespace ToledoVault.Benchmarks;

[MemoryDiagnoser]
[SuppressMessage("ReSharper", "NotAccessedField.Local")]
[SuppressMessage("ReSharper", "NullableWarningSuppressionIsUsed")]
[SuppressMessage("ReSharper", "UnusedVariable")]
public class CryptoBenchmarks
{
    // Pre-generated key material for benchmarks
    private byte[] _x25519PublicKey = null!;
    private byte[] _x25519PrivateKey = null!;
    private byte[] _x25519PeerPublicKey = null!;
    private byte[] _x25519PeerPrivateKey = null!;

    private byte[] _ed25519PublicKey = null!;
    private byte[] _ed25519PrivateKey = null!;
    private byte[] _ed25519Signature = null!;
    private byte[] _testMessage = null!;

    private byte[] _aesKey = null!;
    private byte[] _aesNonce = null!;
    private byte[] _aesPlaintext = null!;
    private byte[] _aesCiphertext = null!;

    private byte[] _mlKemPublicKey = null!;
    private byte[] _mlKemPrivateKey = null!;
    private byte[] _mlKemCiphertext = null!;

    private byte[] _mlDsaPublicKey = null!;
    private byte[] _mlDsaPrivateKey = null!;
    private byte[] _mlDsaSignature = null!;

    private byte[] _hybridClassicalPublic = null!;
    private byte[] _hybridClassicalPrivate = null!;
    private byte[] _hybridPqPublic = null!;
    private byte[] _hybridPqPrivate = null!;
    private byte[] _hybridPeerClassicalPublic = null!;
    private byte[] _hybridPeerPqPublic = null!;

    private byte[] _chainKey = null!;

    private PreKeyBundle _bobBundle = null!;
    private IdentityKeyGenerator.IdentityKeyPair _aliceIdentity = null!;

    // Double Ratchet sessions
    private DoubleRatchet _aliceRatchet = null!;
    private DoubleRatchet _bobRatchet = null!;

    [GlobalSetup]
    public void Setup()
    {
        // X25519
        (_x25519PublicKey, _x25519PrivateKey) = X25519KeyExchange.GenerateKeyPair();
        (_x25519PeerPublicKey, _x25519PeerPrivateKey) = X25519KeyExchange.GenerateKeyPair();

        // Ed25519
        (_ed25519PublicKey, _ed25519PrivateKey) = Ed25519Signer.GenerateKeyPair();
        _testMessage = "Hello, this is a test message for benchmarking cryptographic operations."u8.ToArray();
        _ed25519Signature = Ed25519Signer.Sign(_ed25519PrivateKey, _testMessage);

        // AES-GCM
        _aesKey = new byte[32];
        _aesNonce = new byte[12];
        _aesPlaintext = "This is a plaintext message for AES-GCM benchmarking. It should be at least a few bytes long."u8.ToArray();
        new Random(42).NextBytes(_aesKey);
        new Random(43).NextBytes(_aesNonce);
        _aesCiphertext = AesGcmCipher.Encrypt(_aesKey, _aesNonce, _aesPlaintext);

        // ML-KEM-768
        (_mlKemPublicKey, _mlKemPrivateKey) = MlKemKeyExchange.GenerateKeyPair();
        var (ct, _) = MlKemKeyExchange.Encapsulate(_mlKemPublicKey);
        _mlKemCiphertext = ct;

        // ML-DSA-65
        (_mlDsaPublicKey, _mlDsaPrivateKey) = MlDsaSigner.GenerateKeyPair();
        _mlDsaSignature = MlDsaSigner.Sign(_mlDsaPrivateKey, _testMessage);

        // Hybrid key exchange
        (_hybridClassicalPublic, _hybridClassicalPrivate, _hybridPqPublic, _hybridPqPrivate) =
            HybridKeyExchange.GenerateKeyPair();
        var peerHybrid = HybridKeyExchange.GenerateKeyPair();
        _hybridPeerClassicalPublic = peerHybrid.classicalPublic;
        _hybridPeerPqPublic = peerHybrid.pqPublic;

        // MessageKeys chain key
        _chainKey = new byte[32];
        new Random(44).NextBytes(_chainKey);

        // X3DH setup: generate Alice's identity, Bob's identity + pre-keys, and build Bob's bundle
        _aliceIdentity = IdentityKeyGenerator.Generate();
        var bobIdentity = IdentityKeyGenerator.Generate();

        var signedPreKey = PreKeyGenerator.GenerateSignedPreKey(
            1, bobIdentity.ClassicalPrivateKey, bobIdentity.PostQuantumPrivateKey);
        var kyberPreKey = PreKeyGenerator.GenerateKyberPreKey(
            bobIdentity.ClassicalPrivateKey, bobIdentity.PostQuantumPrivateKey);
        var oneTimePreKeys = PreKeyGenerator.GenerateOneTimePreKeys(1, 1);

        _bobBundle = new PreKeyBundle
        {
            IdentityKeyClassical = bobIdentity.ClassicalPublicKey,
            IdentityKeyPostQuantum = bobIdentity.PostQuantumPublicKey,
            SignedPreKeyPublic = signedPreKey.PublicKey,
            SignedPreKeySignature = signedPreKey.Signature,
            SignedPreKeyId = signedPreKey.KeyId,
            KyberPreKeyPublic = kyberPreKey.PublicKey,
            KyberPreKeySignature = kyberPreKey.Signature,
            OneTimePreKeyPublic = oneTimePreKeys[0].PublicKey,
            OneTimePreKeyId = oneTimePreKeys[0].KeyId
        };

        // Double Ratchet setup: perform X3DH then initialize both sessions
        var initResult = X3dhInitiator.Initiate(_bobBundle);
        var (bobRootKey, bobChainKey) = X3dhResponder.Respond(
            signedPreKey.PrivateKey,
            kyberPreKey.PrivateKey,
            oneTimePreKeys[0].PrivateKey,
            initResult.EphemeralPublicKey,
            initResult.KemCiphertext);

        _bobRatchet = DoubleRatchet.InitializeAsResponder(
            bobRootKey, signedPreKey.PrivateKey, signedPreKey.PublicKey);

        _aliceRatchet = DoubleRatchet.InitializeAsInitiator(
            initResult.RootKey, signedPreKey.PublicKey);
    }

    [Benchmark]
    public void X25519_KeyExchange()
    {
        X25519KeyExchange.ComputeSharedSecret(_x25519PrivateKey, _x25519PeerPublicKey);
    }

    [Benchmark]
    public void Ed25519_Sign()
    {
        Ed25519Signer.Sign(_ed25519PrivateKey, _testMessage);
    }

    [Benchmark]
    public void Ed25519_Verify()
    {
        Ed25519Signer.Verify(_ed25519PublicKey, _testMessage, _ed25519Signature);
    }

    [Benchmark]
    public void AesGcm_Encrypt()
    {
        AesGcmCipher.Encrypt(_aesKey, _aesNonce, _aesPlaintext);
    }

    [Benchmark]
    public void AesGcm_Decrypt()
    {
        AesGcmCipher.Decrypt(_aesKey, _aesNonce, _aesCiphertext);
    }

    [Benchmark]
    public void MlKem768_Encapsulate()
    {
        MlKemKeyExchange.Encapsulate(_mlKemPublicKey);
    }

    [Benchmark]
    public void MlKem768_Decapsulate()
    {
        MlKemKeyExchange.Decapsulate(_mlKemPrivateKey, _mlKemCiphertext);
    }

    [Benchmark]
    public void MlDsa65_Sign()
    {
        MlDsaSigner.Sign(_mlDsaPrivateKey, _testMessage);
    }

    [Benchmark]
    public void MlDsa65_Verify()
    {
        MlDsaSigner.Verify(_mlDsaPublicKey, _testMessage, _mlDsaSignature);
    }

    [Benchmark]
    public void HybridKeyExchange_Encapsulate()
    {
        HybridKeyExchange.Encapsulate(
            _hybridClassicalPrivate, _hybridPeerClassicalPublic, _hybridPeerPqPublic);
    }

    [Benchmark]
    public void DoubleRatchet_EncryptDecrypt()
    {
        var plaintext = "Benchmark message"u8.ToArray();
        var (ciphertext, header) = _aliceRatchet.Encrypt(plaintext);
        _bobRatchet.Decrypt(ciphertext, header);
    }

    [Benchmark]
    public void X3dh_FullHandshake()
    {
        X3dhInitiator.Initiate(_bobBundle);
    }

    [Benchmark]
    public void MessageKeys_DeriveKeys()
    {
        MessageKeys.DeriveKeys(_chainKey);
    }
}
