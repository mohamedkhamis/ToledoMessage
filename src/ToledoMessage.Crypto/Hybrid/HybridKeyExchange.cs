using ToledoMessage.Crypto.Classical;
using ToledoMessage.Crypto.PostQuantum;

namespace ToledoMessage.Crypto.Hybrid;

public static class HybridKeyExchange
{
    private static readonly byte[] HkdfInfo = "ToledoMessage-HybridKEM-v1"u8.ToArray();

    public static (byte[] classicalPublic, byte[] classicalPrivate, byte[] pqPublic, byte[] pqPrivate) GenerateKeyPair()
    {
        var (classicalPublic, classicalPrivate) = X25519KeyExchange.GenerateKeyPair();
        var (pqPublic, pqPrivate) = MlKemKeyExchange.GenerateKeyPair();

        return (classicalPublic, classicalPrivate, pqPublic, pqPrivate);
    }

    public static (byte[] kemCiphertext, byte[] sharedSecret) Encapsulate(
        byte[] classicalPrivateKey,
        byte[] peerClassicalPublicKey,
        byte[] peerPqPublicKey)
    {
        var classicalSharedSecret = X25519KeyExchange.ComputeSharedSecret(classicalPrivateKey, peerClassicalPublicKey);
        var (kemCiphertext, pqSharedSecret) = MlKemKeyExchange.Encapsulate(peerPqPublicKey);

        var combinedIkm = new byte[classicalSharedSecret.Length + pqSharedSecret.Length];
        Buffer.BlockCopy(classicalSharedSecret, 0, combinedIkm, 0, classicalSharedSecret.Length);
        Buffer.BlockCopy(pqSharedSecret, 0, combinedIkm, classicalSharedSecret.Length, pqSharedSecret.Length);

        var finalSharedSecret = HybridKeyDerivation.DeriveKey(combinedIkm, HkdfInfo, 32);

        return (kemCiphertext, finalSharedSecret);
    }

    public static byte[] Decapsulate(
        byte[] classicalPrivateKey,
        byte[] peerClassicalPublicKey,
        byte[] pqPrivateKey,
        byte[] kemCiphertext)
    {
        var classicalSharedSecret = X25519KeyExchange.ComputeSharedSecret(classicalPrivateKey, peerClassicalPublicKey);
        var pqSharedSecret = MlKemKeyExchange.Decapsulate(pqPrivateKey, kemCiphertext);

        var combinedIkm = new byte[classicalSharedSecret.Length + pqSharedSecret.Length];
        Buffer.BlockCopy(classicalSharedSecret, 0, combinedIkm, 0, classicalSharedSecret.Length);
        Buffer.BlockCopy(pqSharedSecret, 0, combinedIkm, classicalSharedSecret.Length, pqSharedSecret.Length);

        var finalSharedSecret = HybridKeyDerivation.DeriveKey(combinedIkm, HkdfInfo, 32);

        return finalSharedSecret;
    }
}
