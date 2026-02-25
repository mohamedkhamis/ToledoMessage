using ToledoMessage.Crypto.PostQuantum;

namespace ToledoMessage.Crypto.Tests.PostQuantum;

public class MlKemKeyExchangeTests
{
    [Fact]
    public void GenerateKeyPair_ReturnsValidKeys()
    {
        var (publicKey, privateKey) = MlKemKeyExchange.GenerateKeyPair();

        Assert.NotEmpty(publicKey);
        Assert.NotEmpty(privateKey);
    }

    [Fact]
    public void Encapsulate_Decapsulate_ProducesSameSharedSecret()
    {
        var (publicKey, privateKey) = MlKemKeyExchange.GenerateKeyPair();

        var (ciphertext, sharedSecretSender) = MlKemKeyExchange.Encapsulate(publicKey);
        var sharedSecretRecipient = MlKemKeyExchange.Decapsulate(privateKey, ciphertext);

        Assert.Equal(32, sharedSecretSender.Length);
        Assert.Equal(32, sharedSecretRecipient.Length);
        Assert.Equal(sharedSecretSender, sharedSecretRecipient);
    }

    [Fact]
    public void DifferentKeyPairs_ProduceDifferentKeys()
    {
        var (publicKey1, _) = MlKemKeyExchange.GenerateKeyPair();
        var (publicKey2, _) = MlKemKeyExchange.GenerateKeyPair();

        Assert.NotEqual(publicKey1, publicKey2);
    }

    [Fact]
    public void Encapsulate_ProducesNonEmptyCiphertext()
    {
        var (publicKey, _) = MlKemKeyExchange.GenerateKeyPair();

        var (ciphertext, _) = MlKemKeyExchange.Encapsulate(publicKey);

        Assert.NotEmpty(ciphertext);
    }
}
