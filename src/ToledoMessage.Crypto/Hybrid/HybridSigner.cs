using ToledoMessage.Crypto.Classical;
using ToledoMessage.Crypto.PostQuantum;

namespace ToledoMessage.Crypto.Hybrid;

public static class HybridSigner
{
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
        var result = new byte[lengthPrefix.Length + ed25519Sig.Length + mlDsaSig.Length];

        Buffer.BlockCopy(lengthPrefix, 0, result, 0, lengthPrefix.Length);
        Buffer.BlockCopy(ed25519Sig, 0, result, lengthPrefix.Length, ed25519Sig.Length);
        Buffer.BlockCopy(mlDsaSig, 0, result, lengthPrefix.Length + ed25519Sig.Length, mlDsaSig.Length);

        return result;
    }

    public static bool Verify(byte[] classicalPublicKey, byte[] pqPublicKey, byte[] message, byte[] signature)
    {
        if (signature.Length < 4)
            return false;

        var ed25519SigLength = BitConverter.ToInt32(signature, 0);

        if (signature.Length < 4 + ed25519SigLength)
            return false;

        var ed25519Sig = new byte[ed25519SigLength];
        Buffer.BlockCopy(signature, 4, ed25519Sig, 0, ed25519SigLength);

        var mlDsaSigLength = signature.Length - 4 - ed25519SigLength;
        var mlDsaSig = new byte[mlDsaSigLength];
        Buffer.BlockCopy(signature, 4 + ed25519SigLength, mlDsaSig, 0, mlDsaSigLength);

        var classicalValid = Ed25519Signer.Verify(classicalPublicKey, message, ed25519Sig);
        var pqValid = MlDsaSigner.Verify(pqPublicKey, message, mlDsaSig);

        return classicalValid && pqValid;
    }
}
