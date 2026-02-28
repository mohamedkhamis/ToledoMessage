using ToledoMessage.Crypto.Classical;
using ToledoMessage.Crypto.Hybrid;
using ToledoMessage.Crypto.PostQuantum;
using ToledoMessage.Crypto.Protocol;

namespace ToledoMessage.Crypto.Tests.Protocol;

[TestClass]
public class X3dhTests
{
    /// <summary>
    /// Creates a valid PreKeyBundle along with Bob's private keys needed for the responder side.
    /// </summary>
    private static (
        PreKeyBundle bundle,
        byte[] spkPrivate,
        byte[] kyberPrivate,
        byte[]? otpkPrivate
    ) CreateBobBundle(bool includeOneTimePreKey)
    {
        // Identity key pair (Ed25519 + ML-DSA-65)
        var (classicalPub, classicalPriv, pqPub, pqPriv) = HybridSigner.GenerateKeyPair();

        // Signed pre-key (X25519)
        var (spkPub, spkPriv) = X25519KeyExchange.GenerateKeyPair();
        var spkSig = HybridSigner.Sign(classicalPriv, pqPriv, spkPub);

        // Kyber pre-key (ML-KEM-768)
        var (kyberPub, kyberPriv) = MlKemKeyExchange.GenerateKeyPair();
        var kyberSig = HybridSigner.Sign(classicalPriv, pqPriv, kyberPub);

        // One-time pre-key (X25519), optional
        byte[]? otpkPub = null;
        byte[]? otpkPriv = null;
        int? otpkId = null;

        if (includeOneTimePreKey)
        {
            var (pub, priv) = X25519KeyExchange.GenerateKeyPair();
            otpkPub = pub;
            otpkPriv = priv;
            otpkId = 42;
        }

        var bundle = new PreKeyBundle
        {
            IdentityKeyClassical = classicalPub,
            IdentityKeyPostQuantum = pqPub,
            SignedPreKeyPublic = spkPub,
            SignedPreKeySignature = spkSig,
            SignedPreKeyId = 1,
            KyberPreKeyPublic = kyberPub,
            KyberPreKeySignature = kyberSig,
            OneTimePreKeyPublic = otpkPub,
            OneTimePreKeyId = otpkId
        };

        return (bundle, spkPriv, kyberPriv, otpkPriv);
    }

    [TestMethod]
    public void Initiate_Respond_WithOtpk_ProduceSameKeys()
    {
        var (bundle, spkPriv, kyberPriv, otpkPriv) = CreateBobBundle(includeOneTimePreKey: true);

        var result = X3dhInitiator.Initiate(bundle);

        var (bobRootKey, bobChainKey) = X3dhResponder.Respond(
            spkPriv,
            kyberPriv,
            otpkPriv,
            result.EphemeralPublicKey,
            result.KemCiphertext);

        CollectionAssert.AreEqual(result.RootKey, bobRootKey);
        CollectionAssert.AreEqual(result.ChainKey, bobChainKey);
        Assert.AreEqual(42, result.UsedOneTimePreKeyId);
    }

    [TestMethod]
    public void Initiate_Respond_WithoutOtpk_ProduceSameKeys()
    {
        var (bundle, spkPriv, kyberPriv, _) = CreateBobBundle(includeOneTimePreKey: false);

        var result = X3dhInitiator.Initiate(bundle);

        var (bobRootKey, bobChainKey) = X3dhResponder.Respond(
            spkPriv,
            kyberPriv,
            null,
            result.EphemeralPublicKey,
            result.KemCiphertext);

        CollectionAssert.AreEqual(result.RootKey, bobRootKey);
        CollectionAssert.AreEqual(result.ChainKey, bobChainKey);
        Assert.IsNull(result.UsedOneTimePreKeyId);
    }

    [TestMethod]
    public void Initiate_InvalidSignedPreKeySignature_Throws()
    {
        var (bundle, _, _, _) = CreateBobBundle(includeOneTimePreKey: true);

        // Tamper with the signed pre-key signature
        var tamperedSig = (byte[])bundle.SignedPreKeySignature.Clone();
        tamperedSig[tamperedSig.Length / 2] ^= 0xFF;

        var tamperedBundle = new PreKeyBundle
        {
            IdentityKeyClassical = bundle.IdentityKeyClassical,
            IdentityKeyPostQuantum = bundle.IdentityKeyPostQuantum,
            SignedPreKeyPublic = bundle.SignedPreKeyPublic,
            SignedPreKeySignature = tamperedSig,
            SignedPreKeyId = bundle.SignedPreKeyId,
            KyberPreKeyPublic = bundle.KyberPreKeyPublic,
            KyberPreKeySignature = bundle.KyberPreKeySignature,
            OneTimePreKeyPublic = bundle.OneTimePreKeyPublic,
            OneTimePreKeyId = bundle.OneTimePreKeyId
        };

        var ex = Assert.Throws<InvalidOperationException>(() => X3dhInitiator.Initiate(tamperedBundle));
        StringAssert.Contains(ex.Message, "Signed pre-key");
    }

    [TestMethod]
    public void Initiate_InvalidKyberPreKeySignature_Throws()
    {
        var (bundle, _, _, _) = CreateBobBundle(includeOneTimePreKey: true);

        // Tamper with the Kyber pre-key signature
        var tamperedSig = (byte[])bundle.KyberPreKeySignature.Clone();
        tamperedSig[tamperedSig.Length / 2] ^= 0xFF;

        var tamperedBundle = new PreKeyBundle
        {
            IdentityKeyClassical = bundle.IdentityKeyClassical,
            IdentityKeyPostQuantum = bundle.IdentityKeyPostQuantum,
            SignedPreKeyPublic = bundle.SignedPreKeyPublic,
            SignedPreKeySignature = bundle.SignedPreKeySignature,
            SignedPreKeyId = bundle.SignedPreKeyId,
            KyberPreKeyPublic = bundle.KyberPreKeyPublic,
            KyberPreKeySignature = tamperedSig,
            OneTimePreKeyPublic = bundle.OneTimePreKeyPublic,
            OneTimePreKeyId = bundle.OneTimePreKeyId
        };

        var ex = Assert.Throws<InvalidOperationException>(() => X3dhInitiator.Initiate(tamperedBundle));
        StringAssert.Contains(ex.Message, "Kyber");
    }

    [TestMethod]
    public void Initiate_ReturnsNonEmptyResults()
    {
        var (bundle, _, _, _) = CreateBobBundle(includeOneTimePreKey: true);

        var result = X3dhInitiator.Initiate(bundle);

        Assert.IsNotNull(result.RootKey);
        Assert.AreEqual(32, result.RootKey.Length);

        Assert.IsNotNull(result.ChainKey);
        Assert.AreEqual(32, result.ChainKey.Length);

        Assert.IsNotNull(result.EphemeralPublicKey);
        Assert.AreEqual(32, result.EphemeralPublicKey.Length);

        Assert.IsNotNull(result.KemCiphertext);
        Assert.IsTrue(result.KemCiphertext.Length > 0);

        // RootKey and ChainKey should be different
        CollectionAssert.AreNotEqual(result.RootKey, result.ChainKey);
    }
}
