using ToledoMessage.Crypto.Classical;
using ToledoMessage.Crypto.Hybrid;
using ToledoMessage.Crypto.KeyManagement;
using ToledoMessage.Crypto.PostQuantum;

namespace ToledoMessage.Crypto.Tests.KeyManagement;

public class IdentityKeyGeneratorTests
{
    [Fact]
    public void Generate_ReturnsValidKeyPair()
    {
        var identity = IdentityKeyGenerator.Generate();

        Assert.NotNull(identity.ClassicalPublicKey);
        Assert.NotNull(identity.ClassicalPrivateKey);
        Assert.NotNull(identity.PostQuantumPublicKey);
        Assert.NotNull(identity.PostQuantumPrivateKey);

        Assert.Equal(32, identity.ClassicalPublicKey.Length);
        Assert.Equal(32, identity.ClassicalPrivateKey.Length);
        Assert.NotEmpty(identity.PostQuantumPublicKey);
        Assert.NotEmpty(identity.PostQuantumPrivateKey);
    }

    [Fact]
    public void Generate_CanSignAndVerify()
    {
        var identity = IdentityKeyGenerator.Generate();
        var message = System.Text.Encoding.UTF8.GetBytes("test message");

        var signature = HybridSigner.Sign(
            identity.ClassicalPrivateKey,
            identity.PostQuantumPrivateKey,
            message);

        var isValid = HybridSigner.Verify(
            identity.ClassicalPublicKey,
            identity.PostQuantumPublicKey,
            message,
            signature);

        Assert.True(isValid);
    }

    [Fact]
    public void Generate_TwoCallsProduceDifferentKeys()
    {
        var id1 = IdentityKeyGenerator.Generate();
        var id2 = IdentityKeyGenerator.Generate();

        Assert.NotEqual(id1.ClassicalPublicKey, id2.ClassicalPublicKey);
        Assert.NotEqual(id1.ClassicalPrivateKey, id2.ClassicalPrivateKey);
    }
}

public class PreKeyGeneratorTests
{
    [Fact]
    public void GenerateSignedPreKey_ReturnsValidKey()
    {
        var identity = IdentityKeyGenerator.Generate();

        var spk = PreKeyGenerator.GenerateSignedPreKey(
            1,
            identity.ClassicalPrivateKey,
            identity.PostQuantumPrivateKey);

        Assert.Equal(1, spk.KeyId);
        Assert.Equal(32, spk.PublicKey.Length);
        Assert.Equal(32, spk.PrivateKey.Length);
        Assert.NotEmpty(spk.Signature);
    }

    [Fact]
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

        Assert.True(isValid);
    }

    [Fact]
    public void GenerateKyberPreKey_ReturnsValidKey()
    {
        var identity = IdentityKeyGenerator.Generate();

        var kpk = PreKeyGenerator.GenerateKyberPreKey(
            identity.ClassicalPrivateKey,
            identity.PostQuantumPrivateKey);

        Assert.NotEmpty(kpk.PublicKey);
        Assert.NotEmpty(kpk.PrivateKey);
        Assert.NotEmpty(kpk.Signature);
    }

    [Fact]
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

        Assert.True(isValid);
    }

    [Fact]
    public void GenerateKyberPreKey_CanEncapsulateAndDecapsulate()
    {
        var identity = IdentityKeyGenerator.Generate();

        var kpk = PreKeyGenerator.GenerateKyberPreKey(
            identity.ClassicalPrivateKey,
            identity.PostQuantumPrivateKey);

        var (ciphertext, sharedSecret1) = MlKemKeyExchange.Encapsulate(kpk.PublicKey);
        var sharedSecret2 = MlKemKeyExchange.Decapsulate(kpk.PrivateKey, ciphertext);

        Assert.Equal(sharedSecret1, sharedSecret2);
    }

    [Fact]
    public void GenerateOneTimePreKeys_ReturnsCorrectCount()
    {
        var keys = PreKeyGenerator.GenerateOneTimePreKeys(10, 5);

        Assert.Equal(5, keys.Count);
    }

    [Fact]
    public void GenerateOneTimePreKeys_HasSequentialIds()
    {
        var keys = PreKeyGenerator.GenerateOneTimePreKeys(10, 5);

        for (int i = 0; i < 5; i++)
        {
            Assert.Equal(10 + i, keys[i].KeyId);
        }
    }

    [Fact]
    public void GenerateOneTimePreKeys_AllKeysAreUnique()
    {
        var keys = PreKeyGenerator.GenerateOneTimePreKeys(1, 10);

        var publicKeys = keys.Select(k => Convert.ToBase64String(k.PublicKey)).ToList();
        Assert.Equal(publicKeys.Count, publicKeys.Distinct().Count());
    }

    [Fact]
    public void GenerateOneTimePreKeys_KeysAreValidX25519()
    {
        var keys = PreKeyGenerator.GenerateOneTimePreKeys(1, 3);

        foreach (var key in keys)
        {
            Assert.Equal(32, key.PublicKey.Length);
            Assert.Equal(32, key.PrivateKey.Length);

            // Verify that DH works with these keys
            var (otherPub, otherPriv) = X25519KeyExchange.GenerateKeyPair();
            var ss1 = X25519KeyExchange.ComputeSharedSecret(key.PrivateKey, otherPub);
            var ss2 = X25519KeyExchange.ComputeSharedSecret(otherPriv, key.PublicKey);
            Assert.Equal(ss1, ss2);
        }
    }

    [Fact]
    public void GenerateOneTimePreKeys_ZeroCount_ReturnsEmptyList()
    {
        var keys = PreKeyGenerator.GenerateOneTimePreKeys(1, 0);

        Assert.Empty(keys);
    }
}

public class FingerprintGeneratorTests
{
    [Fact]
    public void GenerateFingerprint_ReturnsCorrectFormat()
    {
        var key1 = new byte[32];
        var key2 = new byte[32];
        new Random(42).NextBytes(key1);
        new Random(99).NextBytes(key2);

        var fingerprint = FingerprintGenerator.GenerateFingerprint(key1, key2);

        // 30 digits in 6 groups of 5, separated by spaces
        var groups = fingerprint.Split(' ');
        Assert.Equal(6, groups.Length);
        foreach (var group in groups)
        {
            Assert.Equal(5, group.Length);
            Assert.True(group.All(char.IsDigit), $"Group '{group}' contains non-digit characters");
        }
    }

    [Fact]
    public void GenerateFingerprint_SameResultRegardlessOfOrder()
    {
        var key1 = new byte[32];
        var key2 = new byte[32];
        new Random(42).NextBytes(key1);
        new Random(99).NextBytes(key2);

        var fp1 = FingerprintGenerator.GenerateFingerprint(key1, key2);
        var fp2 = FingerprintGenerator.GenerateFingerprint(key2, key1);

        Assert.Equal(fp1, fp2);
    }

    [Fact]
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

        Assert.NotEqual(fp12, fp13);
    }

    [Fact]
    public void GenerateFingerprint_IsDeterministic()
    {
        var key1 = new byte[32];
        var key2 = new byte[32];
        new Random(42).NextBytes(key1);
        new Random(99).NextBytes(key2);

        var fp1 = FingerprintGenerator.GenerateFingerprint(key1, key2);
        var fp2 = FingerprintGenerator.GenerateFingerprint(key1, key2);

        Assert.Equal(fp1, fp2);
    }
}
