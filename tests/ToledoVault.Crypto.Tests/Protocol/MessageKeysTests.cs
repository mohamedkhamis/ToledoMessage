using ToledoVault.Crypto.Protocol;

namespace ToledoVault.Crypto.Tests.Protocol;

[TestClass]
public class MessageKeysTests
{
    [TestMethod]
    public void DeriveMessageKey_Returns32Bytes()
    {
        var chainKey = new byte[32];
        new Random(42).NextBytes(chainKey);

        var messageKey = MessageKeys.DeriveMessageKey(chainKey);

        Assert.AreEqual(32, messageKey.Length);
    }

    [TestMethod]
    public void AdvanceChainKey_Returns32Bytes()
    {
        var chainKey = new byte[32];
        new Random(42).NextBytes(chainKey);

        var nextChainKey = MessageKeys.AdvanceChainKey(chainKey);

        Assert.AreEqual(32, nextChainKey.Length);
    }

    [TestMethod]
    public void DeriveMessageKey_IsDeterministic()
    {
        var chainKey = new byte[32];
        new Random(42).NextBytes(chainKey);

        var key1 = MessageKeys.DeriveMessageKey(chainKey);
        var key2 = MessageKeys.DeriveMessageKey(chainKey);

        CollectionAssert.AreEqual(key1, key2);
    }

    [TestMethod]
    public void MessageKey_DifferentFromChainKey()
    {
        var chainKey = new byte[32];
        new Random(42).NextBytes(chainKey);

        var messageKey = MessageKeys.DeriveMessageKey(chainKey);
        var nextChainKey = MessageKeys.AdvanceChainKey(chainKey);

        CollectionAssert.AreNotEqual(messageKey, nextChainKey);
    }

    [TestMethod]
    public void ChainKeyProgression_ProducesDifferentMessageKeys()
    {
        var chainKey = new byte[32];
        new Random(42).NextBytes(chainKey);

        var (msg1, chain1) = MessageKeys.DeriveKeys(chainKey);
        var (msg2, _) = MessageKeys.DeriveKeys(chain1);

        CollectionAssert.AreNotEqual(msg1, msg2);
    }

    [TestMethod]
    public void DeriveKeys_ReturnsConsistentResults()
    {
        var chainKey = new byte[32];
        new Random(42).NextBytes(chainKey);

        var (messageKey, nextChainKey) = MessageKeys.DeriveKeys(chainKey);

        CollectionAssert.AreEqual(messageKey, MessageKeys.DeriveMessageKey(chainKey));
        CollectionAssert.AreEqual(nextChainKey, MessageKeys.AdvanceChainKey(chainKey));
    }
}
