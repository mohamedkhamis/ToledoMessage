using ToledoVault.Crypto.PostQuantum;

namespace ToledoVault.Crypto.Tests.PostQuantum;

[TestClass]
public class MlDsaSignerTests
{
    [TestMethod]
    public void GenerateKeyPair_ReturnsValidKeys()
    {
        var (publicKey, privateKey) = MlDsaSigner.GenerateKeyPair();

        Assert.IsTrue(publicKey.Length > 0);
        Assert.IsTrue(privateKey.Length > 0);
    }

    [TestMethod]
    public void Sign_ProducesValidSignature()
    {
        var (publicKey, privateKey) = MlDsaSigner.GenerateKeyPair();
        var message = "Hello, post-quantum world!"u8.ToArray();

        var signature = MlDsaSigner.Sign(privateKey, message);

        Assert.IsTrue(MlDsaSigner.Verify(publicKey, message, signature));
    }

    [TestMethod]
    public void Verify_RejectsTamperedMessage()
    {
        var (publicKey, privateKey) = MlDsaSigner.GenerateKeyPair();
        var message = "Original message"u8.ToArray();

        var signature = MlDsaSigner.Sign(privateKey, message);
        var tamperedMessage = "Tampered message"u8.ToArray();

        Assert.IsFalse(MlDsaSigner.Verify(publicKey, tamperedMessage, signature));
    }

    [TestMethod]
    public void Verify_RejectsTamperedSignature()
    {
        var (publicKey, privateKey) = MlDsaSigner.GenerateKeyPair();
        var message = "Test message"u8.ToArray();

        var signature = MlDsaSigner.Sign(privateKey, message);
        signature[0] ^= 0xFF;

        Assert.IsFalse(MlDsaSigner.Verify(publicKey, message, signature));
    }
}
