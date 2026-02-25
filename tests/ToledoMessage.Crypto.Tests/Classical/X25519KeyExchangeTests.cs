using ToledoMessage.Crypto.Classical;

namespace ToledoMessage.Crypto.Tests.Classical;

public class X25519KeyExchangeTests
{
    [Fact]
    public void GenerateKeyPair_ReturnsValidKeys()
    {
        var (publicKey, privateKey) = X25519KeyExchange.GenerateKeyPair();

        Assert.Equal(32, publicKey.Length);
        Assert.Equal(32, privateKey.Length);
    }

    [Fact]
    public void ComputeSharedSecret_BothSidesAgree()
    {
        var (alicePublic, alicePrivate) = X25519KeyExchange.GenerateKeyPair();
        var (bobPublic, bobPrivate) = X25519KeyExchange.GenerateKeyPair();

        var aliceShared = X25519KeyExchange.ComputeSharedSecret(alicePrivate, bobPublic);
        var bobShared = X25519KeyExchange.ComputeSharedSecret(bobPrivate, alicePublic);

        Assert.Equal(32, aliceShared.Length);
        Assert.Equal(aliceShared, bobShared);
    }

    [Fact]
    public void DifferentKeyPairs_ProduceDifferentPublicKeys()
    {
        var (publicKey1, _) = X25519KeyExchange.GenerateKeyPair();
        var (publicKey2, _) = X25519KeyExchange.GenerateKeyPair();

        Assert.NotEqual(publicKey1, publicKey2);
    }
}
