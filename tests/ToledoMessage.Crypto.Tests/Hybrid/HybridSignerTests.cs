using ToledoMessage.Crypto.Hybrid;

namespace ToledoMessage.Crypto.Tests.Hybrid;

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

        // Tamper with a byte in the middle of the signature (past the length prefix)
        signature[signature.Length / 2] ^= 0xFF;
        Assert.IsFalse(HybridSigner.Verify(classicalPublic, pqPublic, message, signature));
    }
}
