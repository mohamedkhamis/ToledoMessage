using ToledoVault.Crypto.Classical;
using ToledoVault.Crypto.Hybrid;
using ToledoVault.Crypto.KeyManagement;
using ToledoVault.Crypto.PostQuantum;

namespace ToledoVault.Crypto.Tests.KeyManagement;

[TestClass]
public class IdentityKeyGeneratorTests
{
    [TestMethod]
    public void Generate_ReturnsValidKeyPair()
    {
        var identity = IdentityKeyGenerator.Generate();

        Assert.IsNotNull(identity.ClassicalPublicKey);
        Assert.IsNotNull(identity.ClassicalPrivateKey);
        Assert.IsNotNull(identity.PostQuantumPublicKey);
        Assert.IsNotNull(identity.PostQuantumPrivateKey);

        Assert.AreEqual(32, identity.ClassicalPublicKey.Length);
        Assert.AreEqual(32, identity.ClassicalPrivateKey.Length);
        Assert.IsTrue(identity.PostQuantumPublicKey.Length > 0);
        Assert.IsTrue(identity.PostQuantumPrivateKey.Length > 0);
    }

    [TestMethod]
    public void Generate_CanSignAndVerify()
    {
        var identity = IdentityKeyGenerator.Generate();
        var message = "test message"u8.ToArray();

        var signature = HybridSigner.Sign(
            identity.ClassicalPrivateKey,
            identity.PostQuantumPrivateKey,
            message);

        var isValid = HybridSigner.Verify(
            identity.ClassicalPublicKey,
            identity.PostQuantumPublicKey,
            message,
            signature);

        Assert.IsTrue(isValid);
    }

    [TestMethod]
    public void Generate_TwoCallsProduceDifferentKeys()
    {
        var id1 = IdentityKeyGenerator.Generate();
        var id2 = IdentityKeyGenerator.Generate();

        CollectionAssert.AreNotEqual(id1.ClassicalPublicKey, id2.ClassicalPublicKey);
        CollectionAssert.AreNotEqual(id1.ClassicalPrivateKey, id2.ClassicalPrivateKey);
    }
}

[TestClass]
public class PreKeyGeneratorTests
{
    [TestMethod]
    public void GenerateSignedPreKey_ReturnsValidKey()
    {
        var identity = IdentityKeyGenerator.Generate();

        var spk = PreKeyGenerator.GenerateSignedPreKey(
            1,
            identity.ClassicalPrivateKey,
            identity.PostQuantumPrivateKey);

        Assert.AreEqual(1, spk.KeyId);
        Assert.AreEqual(32, spk.PublicKey.Length);
        Assert.AreEqual(32, spk.PrivateKey.Length);
        Assert.IsTrue(spk.Signature.Length > 0);
    }

    [TestMethod]
    public void GenerateSignedPreKey_SignatureIsValid()
    {
        var identity = IdentityKeyGenerator.Generate();

        var spk = PreKeyGenerator.GenerateSignedPreKey(
            1,
            identity.ClassicalPrivateKey,
            identity.PostQuantumPrivateKey);

        var isValid = HybridSigner.Verify(
            identity.ClassicalPublicKey,
            identity.PostQuantumPublicKey,
            spk.PublicKey,
            spk.Signature);

        Assert.IsTrue(isValid);
    }

    [TestMethod]
    public void GenerateKyberPreKey_ReturnsValidKey()
    {
        var identity = IdentityKeyGenerator.Generate();

        var kpk = PreKeyGenerator.GenerateKyberPreKey(
            identity.ClassicalPrivateKey,
            identity.PostQuantumPrivateKey);

        Assert.IsTrue(kpk.PublicKey.Length > 0);
        Assert.IsTrue(kpk.PrivateKey.Length > 0);
        Assert.IsTrue(kpk.Signature.Length > 0);
    }

    [TestMethod]
    public void GenerateKyberPreKey_SignatureIsValid()
    {
        var identity = IdentityKeyGenerator.Generate();

        var kpk = PreKeyGenerator.GenerateKyberPreKey(
            identity.ClassicalPrivateKey,
            identity.PostQuantumPrivateKey);

        var isValid = HybridSigner.Verify(
            identity.ClassicalPublicKey,
            identity.PostQuantumPublicKey,
            kpk.PublicKey,
            kpk.Signature);

        Assert.IsTrue(isValid);
    }

    [TestMethod]
    public void GenerateKyberPreKey_CanEncapsulateAndDecapsulate()
    {
        var identity = IdentityKeyGenerator.Generate();

        var kpk = PreKeyGenerator.GenerateKyberPreKey(
            identity.ClassicalPrivateKey,
            identity.PostQuantumPrivateKey);

        var (ciphertext, sharedSecret1) = MlKemKeyExchange.Encapsulate(kpk.PublicKey);
        var sharedSecret2 = MlKemKeyExchange.Decapsulate(kpk.PrivateKey, ciphertext);

        CollectionAssert.AreEqual(sharedSecret1, sharedSecret2);
    }

    [TestMethod]
    public void GenerateOneTimePreKeys_ReturnsCorrectCount()
    {
        var keys = PreKeyGenerator.GenerateOneTimePreKeys(10, 5);

        Assert.AreEqual(5, keys.Count);
    }

    [TestMethod]
    public void GenerateOneTimePreKeys_HasSequentialIds()
    {
        var keys = PreKeyGenerator.GenerateOneTimePreKeys(10, 5);

        for (int i = 0; i < 5; i++) Assert.AreEqual(10 + i, keys[i].KeyId);
    }

    [TestMethod]
    public void GenerateOneTimePreKeys_AllKeysAreUnique()
    {
        var keys = PreKeyGenerator.GenerateOneTimePreKeys(1, 10);

        var publicKeys = keys.Select(k => Convert.ToBase64String(k.PublicKey)).ToList();
        Assert.AreEqual(publicKeys.Count, publicKeys.Distinct().Count());
    }

    [TestMethod]
    public void GenerateOneTimePreKeys_KeysAreValidX25519()
    {
        var keys = PreKeyGenerator.GenerateOneTimePreKeys(1, 3);

        foreach (var key in keys)
        {
            Assert.AreEqual(32, key.PublicKey.Length);
            Assert.AreEqual(32, key.PrivateKey.Length);

            // Verify that DH works with these keys
            var (otherPub, otherPriv) = X25519KeyExchange.GenerateKeyPair();
            var ss1 = X25519KeyExchange.ComputeSharedSecret(key.PrivateKey, otherPub);
            var ss2 = X25519KeyExchange.ComputeSharedSecret(otherPriv, key.PublicKey);
            CollectionAssert.AreEqual(ss1, ss2);
        }
    }

    [TestMethod]
    public void GenerateOneTimePreKeys_ZeroCount_ReturnsEmptyList()
    {
        var keys = PreKeyGenerator.GenerateOneTimePreKeys(1, 0);

        Assert.IsFalse(keys.Any());
    }
}

[TestClass]
public class FingerprintGeneratorTests
{
    [TestMethod]
    public void GenerateFingerprint_ReturnsCorrectFormat()
    {
        var key1 = new byte[32];
        var key2 = new byte[32];
        new Random(42).NextBytes(key1);
        new Random(99).NextBytes(key2);

        var fingerprint = FingerprintGenerator.GenerateFingerprint(key1, key2);

        // 30 digits in 6 groups of 5, separated by spaces
        var groups = fingerprint.Split(' ');
        Assert.AreEqual(6, groups.Length);
        foreach (var group in groups)
        {
            Assert.AreEqual(5, group.Length);
            Assert.IsTrue(group.All(char.IsDigit), $"Group '{group}' contains non-digit characters");
        }
    }

    [TestMethod]
    public void GenerateFingerprint_SameResultRegardlessOfOrder()
    {
        var key1 = new byte[32];
        var key2 = new byte[32];
        new Random(42).NextBytes(key1);
        new Random(99).NextBytes(key2);

        var fp1 = FingerprintGenerator.GenerateFingerprint(key1, key2);
        var fp2 = FingerprintGenerator.GenerateFingerprint(key2, key1);

        Assert.AreEqual(fp1, fp2);
    }

    [TestMethod]
    public void GenerateFingerprint_DifferentKeysProduceDifferentFingerprints()
    {
        var key1 = new byte[32];
        var key2 = new byte[32];
        var key3 = new byte[32];
        new Random(42).NextBytes(key1);
        new Random(99).NextBytes(key2);
        new Random(123).NextBytes(key3);

        var fp12 = FingerprintGenerator.GenerateFingerprint(key1, key2);
        var fp13 = FingerprintGenerator.GenerateFingerprint(key1, key3);

        Assert.AreNotEqual(fp12, fp13);
    }

    [TestMethod]
    public void GenerateFingerprint_IsDeterministic()
    {
        var key1 = new byte[32];
        var key2 = new byte[32];
        new Random(42).NextBytes(key1);
        new Random(99).NextBytes(key2);

        var fp1 = FingerprintGenerator.GenerateFingerprint(key1, key2);
        var fp2 = FingerprintGenerator.GenerateFingerprint(key1, key2);

        Assert.AreEqual(fp1, fp2);
    }
}
