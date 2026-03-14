using ToledoVault.Crypto.Hybrid;

namespace ToledoVault.Crypto.Tests.Hybrid;

[TestClass]
public class HybridSignerTests
{
    [TestMethod]
    public void GenerateKeyPair_ReturnsBothClassicalAndPqKeys()
    {
        var (classicalPublic, classicalPrivate, pqPublic, pqPrivate) = HybridSigner.GenerateKeyPair();

        Assert.IsTrue(classicalPublic.Length > 0);
        Assert.IsTrue(classicalPrivate.Length > 0);
        Assert.IsTrue(pqPublic.Length > 0);
        Assert.IsTrue(pqPrivate.Length > 0);
    }

    [TestMethod]
    public void Sign_Verify_RoundTrip()
    {
        var (classicalPublic, classicalPrivate, pqPublic, pqPrivate) = HybridSigner.GenerateKeyPair();
        var message = "Hello, Toledo!"u8.ToArray();

        var signature = HybridSigner.Sign(classicalPrivate, pqPrivate, message);

        Assert.IsTrue(HybridSigner.Verify(classicalPublic, pqPublic, message, signature));
    }

    [TestMethod]
    public void Verify_RejectsTamperedMessage()
    {
        var (classicalPublic, classicalPrivate, pqPublic, pqPrivate) = HybridSigner.GenerateKeyPair();
        var message = "Hello, Toledo!"u8.ToArray();

        var signature = HybridSigner.Sign(classicalPrivate, pqPrivate, message);

        var tamperedMessage = "Hello, Tampered!"u8.ToArray();
        Assert.IsFalse(HybridSigner.Verify(classicalPublic, pqPublic, tamperedMessage, signature));
    }

    [TestMethod]
    public void Verify_RejectsTamperedSignature()
    {
        var (classicalPublic, classicalPrivate, pqPublic, pqPrivate) = HybridSigner.GenerateKeyPair();
        var message = "Hello, Toledo!"u8.ToArray();

        var signature = HybridSigner.Sign(classicalPrivate, pqPrivate, message);

        // Tamper with a byte in the middle of the signature (past the version byte and length prefix)
        signature[signature.Length / 2] ^= 0xFF;
        Assert.IsFalse(HybridSigner.Verify(classicalPublic, pqPublic, message, signature));
    }

    [TestMethod]
    public void Sign_ProducesV1Format_WithVersionByte()
    {
        var (_, classicalPrivate, _, pqPrivate) = HybridSigner.GenerateKeyPair();
        var message = "version test"u8.ToArray();

        var signature = HybridSigner.Sign(classicalPrivate, pqPrivate, message);

        // v1 signatures start with 0x01
        Assert.AreEqual(0x01, signature[0]);
    }

    [TestMethod]
    public void Verify_AcceptsV0LegacyFormat()
    {
        // Simulate a v0 signature (no version byte prefix)
        var (classicalPublic, classicalPrivate, pqPublic, pqPrivate) = HybridSigner.GenerateKeyPair();
        var message = "legacy test"u8.ToArray();

        // Build a v0 signature manually: [4-byte length][ed25519 sig][ml-dsa sig]
        var ed25519Sig = ToledoVault.Crypto.Classical.Ed25519Signer.Sign(classicalPrivate, message);
        var mlDsaSig = ToledoVault.Crypto.PostQuantum.MlDsaSigner.Sign(pqPrivate, message);

        var lengthPrefix = BitConverter.GetBytes(ed25519Sig.Length);
        var v0Signature = new byte[lengthPrefix.Length + ed25519Sig.Length + mlDsaSig.Length];
        Buffer.BlockCopy(lengthPrefix, 0, v0Signature, 0, lengthPrefix.Length);
        Buffer.BlockCopy(ed25519Sig, 0, v0Signature, lengthPrefix.Length, ed25519Sig.Length);
        Buffer.BlockCopy(mlDsaSig, 0, v0Signature, lengthPrefix.Length + ed25519Sig.Length, mlDsaSig.Length);

        // v0 should still verify
        Assert.IsTrue(HybridSigner.Verify(classicalPublic, pqPublic, message, v0Signature));
    }

    [TestMethod]
    public void Verify_RejectsTooShortSignature()
    {
        var (classicalPublic, _, pqPublic, _) = HybridSigner.GenerateKeyPair();
        var message = "short"u8.ToArray();

        Assert.IsFalse(HybridSigner.Verify(classicalPublic, pqPublic, message, [0x01, 0x02]));
        Assert.IsFalse(HybridSigner.Verify(classicalPublic, pqPublic, message, []));
    }
}
