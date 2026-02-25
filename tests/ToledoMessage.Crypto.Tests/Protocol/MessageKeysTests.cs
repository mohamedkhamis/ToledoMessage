using ToledoMessage.Crypto.Protocol;

namespace ToledoMessage.Crypto.Tests.Protocol;

public class MessageKeysTests
{
    [Fact]
    public void DeriveMessageKey_Returns32Bytes()
    {
        var chainKey = new byte[32];
        new Random(42).NextBytes(chainKey);

        var messageKey = MessageKeys.DeriveMessageKey(chainKey);

        Assert.Equal(32, messageKey.Length);
    }

    [Fact]
    public void AdvanceChainKey_Returns32Bytes()
    {
        var chainKey = new byte[32];
        new Random(42).NextBytes(chainKey);

        var nextChainKey = MessageKeys.AdvanceChainKey(chainKey);

        Assert.Equal(32, nextChainKey.Length);
    }

    [Fact]
    public void DeriveMessageKey_IsDeterministic()
    {
        var chainKey = new byte[32];
        new Random(42).NextBytes(chainKey);

        var key1 = MessageKeys.DeriveMessageKey(chainKey);
        var key2 = MessageKeys.DeriveMessageKey(chainKey);

        Assert.Equal(key1, key2);
    }

    [Fact]
    public void MessageKey_DifferentFromChainKey()
    {
        var chainKey = new byte[32];
        new Random(42).NextBytes(chainKey);

        var messageKey = MessageKeys.DeriveMessageKey(chainKey);
        var nextChainKey = MessageKeys.AdvanceChainKey(chainKey);

        Assert.NotEqual(messageKey, nextChainKey);
    }

    [Fact]
    public void ChainKeyProgression_ProducesDifferentMessageKeys()
    {
        var chainKey = new byte[32];
        new Random(42).NextBytes(chainKey);

        var (msg1, chain1) = MessageKeys.DeriveKeys(chainKey);
        var (msg2, _) = MessageKeys.DeriveKeys(chain1);

        Assert.NotEqual(msg1, msg2);
    }

    [Fact]
    public void DeriveKeys_ReturnsConsistentResults()
    {
        var chainKey = new byte[32];
        new Random(42).NextBytes(chainKey);

        var (messageKey, nextChainKey) = MessageKeys.DeriveKeys(chainKey);

        Assert.Equal(messageKey, MessageKeys.DeriveMessageKey(chainKey));
        Assert.Equal(nextChainKey, MessageKeys.AdvanceChainKey(chainKey));
    }
}
