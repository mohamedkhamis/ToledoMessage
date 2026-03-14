using ToledoVault.Crypto.Classical;

namespace ToledoVault.Crypto.Tests.Classical;

[TestClass]
public class Ed25519SignerTests
{
    [TestMethod]
    public void GenerateKeyPair_ReturnsValidKeys()
    {
        var (publicKey, privateKey) = Ed25519Signer.GenerateKeyPair();

        Assert.AreEqual(32, publicKey.Length);
        Assert.AreEqual(32, privateKey.Length);
    }

    [TestMethod]
    public void Sign_ProducesValidSignature()
    {
        var (publicKey, privateKey) = Ed25519Signer.GenerateKeyPair();
        var message = "Hello, Toledo!"u8.ToArray();

        var signature = Ed25519Signer.Sign(privateKey, message);

        Assert.AreEqual(64, signature.Length);
        Assert.IsTrue(Ed25519Signer.Verify(publicKey, message, signature));
    }

    [TestMethod]
    public void Verify_RejectsTamperedMessage()
    {
        var (publicKey, privateKey) = Ed25519Signer.GenerateKeyPair();
        var message = "Hello, Toledo!"u8.ToArray();

        var signature = Ed25519Signer.Sign(privateKey, message);

        var tamperedMessage = "Hello, Tampered!"u8.ToArray();
        Assert.IsFalse(Ed25519Signer.Verify(publicKey, tamperedMessage, signature));
    }

    [TestMethod]
    public void Verify_RejectsTamperedSignature()
    {
        var (publicKey, privateKey) = Ed25519Signer.GenerateKeyPair();
        var message = "Hello, Toledo!"u8.ToArray();

        var signature = Ed25519Signer.Sign(privateKey, message);

        signature[0] ^= 0xFF;
        Assert.IsFalse(Ed25519Signer.Verify(publicKey, message, signature));
    }
}
