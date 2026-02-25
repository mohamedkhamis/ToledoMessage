using System.Text;
using ToledoMessage.Crypto.PostQuantum;

namespace ToledoMessage.Crypto.Tests.PostQuantum;

public class MlDsaSignerTests
{
    [Fact]
    public void GenerateKeyPair_ReturnsValidKeys()
    {
        var (publicKey, privateKey) = MlDsaSigner.GenerateKeyPair();

        Assert.NotEmpty(publicKey);
        Assert.NotEmpty(privateKey);
    }

    [Fact]
    public void Sign_ProducesValidSignature()
    {
        var (publicKey, privateKey) = MlDsaSigner.GenerateKeyPair();
        var message = Encoding.UTF8.GetBytes("Hello, post-quantum world!");

        var signature = MlDsaSigner.Sign(privateKey, message);

        Assert.True(MlDsaSigner.Verify(publicKey, message, signature));
    }

    [Fact]
    public void Verify_RejectsTamperedMessage()
    {
        var (publicKey, privateKey) = MlDsaSigner.GenerateKeyPair();
        var message = Encoding.UTF8.GetBytes("Original message");

        var signature = MlDsaSigner.Sign(privateKey, message);
        var tamperedMessage = Encoding.UTF8.GetBytes("Tampered message");

        Assert.False(MlDsaSigner.Verify(publicKey, tamperedMessage, signature));
    }

    [Fact]
    public void Verify_RejectsTamperedSignature()
    {
        var (publicKey, privateKey) = MlDsaSigner.GenerateKeyPair();
        var message = Encoding.UTF8.GetBytes("Test message");

        var signature = MlDsaSigner.Sign(privateKey, message);
        signature[0] ^= 0xFF;

        Assert.False(MlDsaSigner.Verify(publicKey, message, signature));
    }
}
