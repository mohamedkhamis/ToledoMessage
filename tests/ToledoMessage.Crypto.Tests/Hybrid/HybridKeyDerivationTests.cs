using ToledoMessage.Crypto.Hybrid;

namespace ToledoMessage.Crypto.Tests.Hybrid;

public class HybridKeyDerivationTests
{
    [Fact]
    public void DeriveKey_ProducesRequestedLength()
    {
        var ikm = new byte[32];
        Random.Shared.NextBytes(ikm);
        var info = "test-info"u8.ToArray();

        var result = HybridKeyDerivation.DeriveKey(ikm, info, 64);

        Assert.Equal(64, result.Length);
    }

    [Fact]
    public void DeriveKey_SameInputProducesSameOutput()
    {
        var ikm = new byte[32];
        Random.Shared.NextBytes(ikm);
        var info = "test-info"u8.ToArray();

        var result1 = HybridKeyDerivation.DeriveKey(ikm, info, 32);
        var result2 = HybridKeyDerivation.DeriveKey(ikm, info, 32);

        Assert.Equal(result1, result2);
    }

    [Fact]
    public void DeriveKey_DifferentInfoProducesDifferentOutput()
    {
        var ikm = new byte[32];
        Random.Shared.NextBytes(ikm);
        var info1 = "info-one"u8.ToArray();
        var info2 = "info-two"u8.ToArray();

        var result1 = HybridKeyDerivation.DeriveKey(ikm, info1, 32);
        var result2 = HybridKeyDerivation.DeriveKey(ikm, info2, 32);

        Assert.NotEqual(result1, result2);
    }

    [Fact]
    public void DeriveKey_DifferentIkmProducesDifferentOutput()
    {
        var ikm1 = new byte[32];
        var ikm2 = new byte[32];
        Random.Shared.NextBytes(ikm1);
        Random.Shared.NextBytes(ikm2);
        var info = "test-info"u8.ToArray();

        var result1 = HybridKeyDerivation.DeriveKey(ikm1, info, 32);
        var result2 = HybridKeyDerivation.DeriveKey(ikm2, info, 32);

        Assert.NotEqual(result1, result2);
    }

    [Fact]
    public void DeriveKey_WithAndWithoutSalt_ProduceDifferentOutput()
    {
        var ikm = new byte[32];
        Random.Shared.NextBytes(ikm);
        var salt = new byte[16];
        Random.Shared.NextBytes(salt);
        var info = "test-info"u8.ToArray();

        var withSalt = HybridKeyDerivation.DeriveKey(ikm, salt, info, 32);
        var withoutSalt = HybridKeyDerivation.DeriveKey(ikm, info, 32);

        Assert.NotEqual(withSalt, withoutSalt);
    }
}
