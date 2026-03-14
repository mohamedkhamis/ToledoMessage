using ToledoVault.Crypto.Classical;
using ToledoVault.Crypto.PostQuantum;

namespace ToledoVault.Crypto.Hybrid;

/// <summary>
/// Hybrid signer combining Ed25519 (classical) and ML-DSA (post-quantum) signatures.
///
/// Wire format:
///   v0 (legacy): [4-byte Ed25519 sig length (little-endian)][Ed25519 sig][ML-DSA sig]
///   v1 (current): [0x01][4-byte Ed25519 sig length (little-endian)][Ed25519 sig][ML-DSA sig]
///
/// Sign() always produces v1. Verify() auto-detects and accepts both v0 and v1.
/// </summary>
public static class HybridSigner
{
    private const byte SignatureVersion1 = 0x01;

    public static (byte[] classicalPublic, byte[] classicalPrivate, byte[] pqPublic, byte[] pqPrivate) GenerateKeyPair()
    {
        var (classicalPublic, classicalPrivate) = Ed25519Signer.GenerateKeyPair();
        var (pqPublic, pqPrivate) = MlDsaSigner.GenerateKeyPair();

        return (classicalPublic, classicalPrivate, pqPublic, pqPrivate);
    }

    public static byte[] Sign(byte[] classicalPrivateKey, byte[] pqPrivateKey, byte[] message)
    {
        var ed25519Sig = Ed25519Signer.Sign(classicalPrivateKey, message);
        var mlDsaSig = MlDsaSigner.Sign(pqPrivateKey, message);

        var lengthPrefix = BitConverter.GetBytes(ed25519Sig.Length);
        // v1 format: [version byte][length prefix][ed25519 sig][ml-dsa sig]
        var result = new byte[1 + lengthPrefix.Length + ed25519Sig.Length + mlDsaSig.Length];

        result[0] = SignatureVersion1;
        Buffer.BlockCopy(lengthPrefix, 0, result, 1, lengthPrefix.Length);
        Buffer.BlockCopy(ed25519Sig, 0, result, 1 + lengthPrefix.Length, ed25519Sig.Length);
        Buffer.BlockCopy(mlDsaSig, 0, result, 1 + lengthPrefix.Length + ed25519Sig.Length, mlDsaSig.Length);

        return result;
    }

    public static bool Verify(byte[] classicalPublicKey, byte[] pqPublicKey, byte[] message, byte[] signature)
    {
        if (signature.Length < 5)
            return false;

        // Auto-detect version: v1 starts with 0x01, v0 starts with a 4-byte length prefix
        // v1 format: skip version byte

        var offset = signature[0] == SignatureVersion1 ? 1 : 0; // v0 format: no version byte

        if (signature.Length < offset + 4)
            return false;

        var ed25519SigLength = BitConverter.ToInt32(signature, offset);
        offset += 4;

        if (ed25519SigLength <= 0 || signature.Length < offset + ed25519SigLength)
            return false;

        var ed25519Sig = new byte[ed25519SigLength];
        Buffer.BlockCopy(signature, offset, ed25519Sig, 0, ed25519SigLength);
        offset += ed25519SigLength;

        var mlDsaSigLength = signature.Length - offset;
        if (mlDsaSigLength <= 0)
            return false;

        var mlDsaSig = new byte[mlDsaSigLength];
        Buffer.BlockCopy(signature, offset, mlDsaSig, 0, mlDsaSigLength);

        var classicalValid = Ed25519Signer.Verify(classicalPublicKey, message, ed25519Sig);
        var pqValid = MlDsaSigner.Verify(pqPublicKey, message, mlDsaSig);

        return classicalValid && pqValid;
    }
}
