using ToledoMessage.Crypto.Hybrid;

namespace ToledoMessage.Crypto.Tests.Hybrid;

public class HybridSignerTests
{
    [Fact]
    public void GenerateKeyPair_ReturnsBothClassicalAndPqKeys()
    {
        var (classicalPublic, classicalPrivate, pqPublic, pqPrivate) = HybridSigner.GenerateKeyPair();

        Assert.NotEmpty(classicalPublic);
        Assert.NotEmpty(classicalPrivate);
        Assert.NotEmpty(pqPublic);
        Assert.NotEmpty(pqPrivate);
    }

    [Fact]
    public void Sign_Verify_RoundTrip()
    {
        var (classicalPublic, classicalPrivate, pqPublic, pqPrivate) = HybridSigner.GenerateKeyPair();
        var message = "Hello, Toledo!"u8.ToArray();

        var signature = HybridSigner.Sign(classicalPrivate, pqPrivate, message);

        Assert.True(HybridSigner.Verify(classicalPublic, pqPublic, message, signature));
    }

    [Fact]
    public void Verify_RejectsTamperedMessage()
    {
        var (classicalPublic, classicalPrivate, pqPublic, pqPrivate) = HybridSigner.GenerateKeyPair();
        var message = "Hello, Toledo!"u8.ToArray();

        var signature = HybridSigner.Sign(classicalPrivate, pqPrivate, message);

        var tamperedMessage = "Hello, Tampered!"u8.ToArray();
        Assert.False(HybridSigner.Verify(classicalPublic, pqPublic, tamperedMessage, signature));
    }

    [Fact]
    public void Verify_RejectsTamperedSignature()
    {
        var (classicalPublic, classicalPrivate, pqPublic, pqPrivate) = HybridSigner.GenerateKeyPair();
        var message = "Hello, Toledo!"u8.ToArray();

        var signature = HybridSigner.Sign(classicalPrivate, pqPrivate, message);

        // Tamper with a byte in the middle of the signature (past the length prefix)
        signature[signature.Length / 2] ^= 0xFF;
        Assert.False(HybridSigner.Verify(classicalPublic, pqPublic, message, signature));
    }
}
