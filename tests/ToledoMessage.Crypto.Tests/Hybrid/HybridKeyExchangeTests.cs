using ToledoMessage.Crypto.Hybrid;

namespace ToledoMessage.Crypto.Tests.Hybrid;

[TestClass]
public class HybridKeyExchangeTests
{
    [TestMethod]
    public void GenerateKeyPair_ReturnsBothClassicalAndPqKeys()
    {
        var (classicalPublic, classicalPrivate, pqPublic, pqPrivate) = HybridKeyExchange.GenerateKeyPair();

        Assert.IsTrue(classicalPublic.Length > 0);
        Assert.IsTrue(classicalPrivate.Length > 0);
        Assert.IsTrue(pqPublic.Length > 0);
        Assert.IsTrue(pqPrivate.Length > 0);
    }

    [TestMethod]
    public void Encapsulate_Decapsulate_ProduceSameSharedSecret()
    {
        // ReSharper disable  UnusedVariable
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
        // ReSharper disable ArgumentsStyleNamedExpression
        var bobSharedSecret = HybridKeyExchange.Decapsulate(
            classicalPrivateKey: bobClassicalPrivate,
            peerClassicalPublicKey: aliceClassicalPublic,
            pqPrivateKey: bobPqPrivate,
            kemCiphertext: kemCiphertext);

        Assert.AreEqual(32, aliceSharedSecret.Length);
        Assert.AreEqual(32, bobSharedSecret.Length);
        CollectionAssert.AreEqual(aliceSharedSecret, bobSharedSecret);
    }

    [TestMethod]
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

        CollectionAssert.AreNotEqual(aliceBobSecret, aliceCharlieSecret);
    }
}
