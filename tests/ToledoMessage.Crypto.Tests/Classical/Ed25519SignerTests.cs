using ToledoMessage.Crypto.Classical;

namespace ToledoMessage.Crypto.Tests.Classical;

public class Ed25519SignerTests
{
    [Fact]
    public void GenerateKeyPair_ReturnsValidKeys()
    {
        var (publicKey, privateKey) = Ed25519Signer.GenerateKeyPair();

        Assert.Equal(32, publicKey.Length);
        Assert.Equal(32, privateKey.Length);
    }

    [Fact]
    public void Sign_ProducesValidSignature()
    {
        var (publicKey, privateKey) = Ed25519Signer.GenerateKeyPair();
        var message = "Hello, Toledo!"u8.ToArray();

        var signature = Ed25519Signer.Sign(privateKey, message);

        Assert.Equal(64, signature.Length);
        Assert.True(Ed25519Signer.Verify(publicKey, message, signature));
    }

    [Fact]
    public void Verify_RejectsTamperedMessage()
    {
        var (publicKey, privateKey) = Ed25519Signer.GenerateKeyPair();
        var message = "Hello, Toledo!"u8.ToArray();

        var signature = Ed25519Signer.Sign(privateKey, message);

        var tamperedMessage = "Hello, Tampered!"u8.ToArray();
        Assert.False(Ed25519Signer.Verify(publicKey, tamperedMessage, signature));
    }

    [Fact]
    public void Verify_RejectsTamperedSignature()
    {
        var (publicKey, privateKey) = Ed25519Signer.GenerateKeyPair();
        var message = "Hello, Toledo!"u8.ToArray();

        var signature = Ed25519Signer.Sign(privateKey, message);

        signature[0] ^= 0xFF;
        Assert.False(Ed25519Signer.Verify(publicKey, message, signature));
    }
}
