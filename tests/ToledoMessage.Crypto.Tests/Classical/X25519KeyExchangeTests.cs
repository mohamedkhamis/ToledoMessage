using ToledoMessage.Crypto.Classical;

namespace ToledoMessage.Crypto.Tests.Classical;

[TestClass]
public class X25519KeyExchangeTests
{
    [TestMethod]
    public void GenerateKeyPair_ReturnsValidKeys()
    {
        var (publicKey, privateKey) = X25519KeyExchange.GenerateKeyPair();

        Assert.AreEqual(32, publicKey.Length);
        Assert.AreEqual(32, privateKey.Length);
    }

    [TestMethod]
    public void ComputeSharedSecret_BothSidesAgree()
    {
        var (alicePublic, alicePrivate) = X25519KeyExchange.GenerateKeyPair();
        var (bobPublic, bobPrivate) = X25519KeyExchange.GenerateKeyPair();

        var aliceShared = X25519KeyExchange.ComputeSharedSecret(alicePrivate, bobPublic);
        var bobShared = X25519KeyExchange.ComputeSharedSecret(bobPrivate, alicePublic);

        Assert.AreEqual(32, aliceShared.Length);
        CollectionAssert.AreEqual(aliceShared, bobShared);
    }

    [TestMethod]
    public void DifferentKeyPairs_ProduceDifferentPublicKeys()
    {
        var (publicKey1, _) = X25519KeyExchange.GenerateKeyPair();
        var (publicKey2, _) = X25519KeyExchange.GenerateKeyPair();

        CollectionAssert.AreNotEqual(publicKey1, publicKey2);
    }
}
