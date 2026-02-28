using ToledoMessage.Crypto.PostQuantum;

namespace ToledoMessage.Crypto.Tests.PostQuantum;

[TestClass]
public class MlKemKeyExchangeTests
{
    [TestMethod]
    public void GenerateKeyPair_ReturnsValidKeys()
    {
        var (publicKey, privateKey) = MlKemKeyExchange.GenerateKeyPair();

        Assert.IsTrue(publicKey.Length > 0);
        Assert.IsTrue(privateKey.Length > 0);
    }

    [TestMethod]
    public void Encapsulate_Decapsulate_ProducesSameSharedSecret()
    {
        var (publicKey, privateKey) = MlKemKeyExchange.GenerateKeyPair();

        var (ciphertext, sharedSecretSender) = MlKemKeyExchange.Encapsulate(publicKey);
        var sharedSecretRecipient = MlKemKeyExchange.Decapsulate(privateKey, ciphertext);

        Assert.AreEqual(32, sharedSecretSender.Length);
        Assert.AreEqual(32, sharedSecretRecipient.Length);
        CollectionAssert.AreEqual(sharedSecretSender, sharedSecretRecipient);
    }

    [TestMethod]
    public void DifferentKeyPairs_ProduceDifferentKeys()
    {
        var (publicKey1, _) = MlKemKeyExchange.GenerateKeyPair();
        var (publicKey2, _) = MlKemKeyExchange.GenerateKeyPair();

        CollectionAssert.AreNotEqual(publicKey1, publicKey2);
    }

    [TestMethod]
    public void Encapsulate_ProducesNonEmptyCiphertext()
    {
        var (publicKey, _) = MlKemKeyExchange.GenerateKeyPair();

        var (ciphertext, _) = MlKemKeyExchange.Encapsulate(publicKey);

        Assert.IsTrue(ciphertext.Length > 0);
    }
}
