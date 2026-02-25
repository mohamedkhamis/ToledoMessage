using ToledoMessage.Crypto.Hybrid;

namespace ToledoMessage.Crypto.Tests.Hybrid;

public class HybridKeyExchangeTests
{
    [Fact]
    public void GenerateKeyPair_ReturnsBothClassicalAndPqKeys()
    {
        var (classicalPublic, classicalPrivate, pqPublic, pqPrivate) = HybridKeyExchange.GenerateKeyPair();

        Assert.NotEmpty(classicalPublic);
        Assert.NotEmpty(classicalPrivate);
        Assert.NotEmpty(pqPublic);
        Assert.NotEmpty(pqPrivate);
    }

    [Fact]
    public void Encapsulate_Decapsulate_ProduceSameSharedSecret()
    {
        var (aliceClassicalPublic, aliceClassicalPrivate, alicePqPublic, alicePqPrivate) =
            HybridKeyExchange.GenerateKeyPair();
        var (bobClassicalPublic, bobClassicalPrivate, bobPqPublic, bobPqPrivate) =
            HybridKeyExchange.GenerateKeyPair();

        // Alice encapsulates toward Bob
        var (kemCiphertext, aliceSharedSecret) = HybridKeyExchange.Encapsulate(
            aliceClassicalPrivate,
            bobClassicalPublic,
            bobPqPublic);

        // Bob decapsulates
        var bobSharedSecret = HybridKeyExchange.Decapsulate(
            bobClassicalPrivateKey: bobClassicalPrivate,
            peerClassicalPublicKey: aliceClassicalPublic,
            pqPrivateKey: bobPqPrivate,
            kemCiphertext: kemCiphertext);

        Assert.Equal(32, aliceSharedSecret.Length);
        Assert.Equal(32, bobSharedSecret.Length);
        Assert.Equal(aliceSharedSecret, bobSharedSecret);
    }

    [Fact]
    public void DifferentKeyPairs_ProduceDifferentSharedSecrets()
    {
        var (aliceClassicalPublic, aliceClassicalPrivate, alicePqPublic, alicePqPrivate) =
            HybridKeyExchange.GenerateKeyPair();
        var (bobClassicalPublic, bobClassicalPrivate, bobPqPublic, bobPqPrivate) =
            HybridKeyExchange.GenerateKeyPair();
        var (charlieClassicalPublic, charlieClassicalPrivate, charliePqPublic, charliePqPrivate) =
            HybridKeyExchange.GenerateKeyPair();

        // Alice encapsulates toward Bob
        var (_, aliceBobSecret) = HybridKeyExchange.Encapsulate(
            aliceClassicalPrivate,
            bobClassicalPublic,
            bobPqPublic);

        // Alice encapsulates toward Charlie
        var (_, aliceCharlieSecret) = HybridKeyExchange.Encapsulate(
            aliceClassicalPrivate,
            charlieClassicalPublic,
            charliePqPublic);

        Assert.NotEqual(aliceBobSecret, aliceCharlieSecret);
    }
}
