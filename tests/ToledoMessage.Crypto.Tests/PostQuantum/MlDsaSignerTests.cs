using System.Text;
using ToledoMessage.Crypto.PostQuantum;

namespace ToledoMessage.Crypto.Tests.PostQuantum;

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
        var message = Encoding.UTF8.GetBytes("Hello, post-quantum world!");

        var signature = MlDsaSigner.Sign(privateKey, message);

        Assert.IsTrue(MlDsaSigner.Verify(publicKey, message, signature));
    }

    [TestMethod]
    public void Verify_RejectsTamperedMessage()
    {
        var (publicKey, privateKey) = MlDsaSigner.GenerateKeyPair();
        var message = Encoding.UTF8.GetBytes("Original message");

        var signature = MlDsaSigner.Sign(privateKey, message);
        var tamperedMessage = Encoding.UTF8.GetBytes("Tampered message");

        Assert.IsFalse(MlDsaSigner.Verify(publicKey, tamperedMessage, signature));
    }

    [TestMethod]
    public void Verify_RejectsTamperedSignature()
    {
        var (publicKey, privateKey) = MlDsaSigner.GenerateKeyPair();
        var message = Encoding.UTF8.GetBytes("Test message");

        var signature = MlDsaSigner.Sign(privateKey, message);
        signature[0] ^= 0xFF;

        Assert.IsFalse(MlDsaSigner.Verify(publicKey, message, signature));
    }
}
