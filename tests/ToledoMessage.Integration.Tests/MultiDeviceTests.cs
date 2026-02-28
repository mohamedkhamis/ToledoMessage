using System.Text;
using ToledoMessage.Crypto.KeyManagement;
using ToledoMessage.Crypto.Protocol;

namespace ToledoMessage.Integration.Tests;

[TestClass]
public class MultiDeviceTests
{
    [TestMethod]
    public void FanOutEncryption_MultipleDevices_AllDecryptCorrectly()
    {
        // ================================================================
        // 1. Alice generates identity + pre-keys
        // ================================================================
        var aliceIdentity = IdentityKeyGenerator.Generate();

        // ================================================================
        // 2. Bob generates identity + pre-keys for Device1
        // ================================================================
        var bobDevice1Identity = IdentityKeyGenerator.Generate();
        var bobDevice1SignedPreKey = PreKeyGenerator.GenerateSignedPreKey(
            keyId: 1,
            identityClassicalPrivate: bobDevice1Identity.ClassicalPrivateKey,
            identityPqPrivate: bobDevice1Identity.PostQuantumPrivateKey);
        var bobDevice1KyberPreKey = PreKeyGenerator.GenerateKyberPreKey(
            bobDevice1Identity.ClassicalPrivateKey,
            bobDevice1Identity.PostQuantumPrivateKey);
        var bobDevice1OneTimePreKeys = PreKeyGenerator.GenerateOneTimePreKeys(startKeyId: 1, count: 5);

        // ================================================================
        // 3. Bob generates SEPARATE identity + pre-keys for Device2
        //    (different key material — each device has its own identity)
        // ================================================================
        var bobDevice2Identity = IdentityKeyGenerator.Generate();
        var bobDevice2SignedPreKey = PreKeyGenerator.GenerateSignedPreKey(
            keyId: 1,
            identityClassicalPrivate: bobDevice2Identity.ClassicalPrivateKey,
            identityPqPrivate: bobDevice2Identity.PostQuantumPrivateKey);
        var bobDevice2KyberPreKey = PreKeyGenerator.GenerateKyberPreKey(
            bobDevice2Identity.ClassicalPrivateKey,
            bobDevice2Identity.PostQuantumPrivateKey);
        var bobDevice2OneTimePreKeys = PreKeyGenerator.GenerateOneTimePreKeys(startKeyId: 10, count: 5);

        // ================================================================
        // 4. Alice performs X3DH with Bob's Device1 -> session1
        // ================================================================
        var bobDevice1Bundle = new PreKeyBundle
        {
            IdentityKeyClassical = bobDevice1Identity.ClassicalPublicKey,
            IdentityKeyPostQuantum = bobDevice1Identity.PostQuantumPublicKey,
            SignedPreKeyPublic = bobDevice1SignedPreKey.PublicKey,
            SignedPreKeySignature = bobDevice1SignedPreKey.Signature,
            SignedPreKeyId = bobDevice1SignedPreKey.KeyId,
            KyberPreKeyPublic = bobDevice1KyberPreKey.PublicKey,
            KyberPreKeySignature = bobDevice1KyberPreKey.Signature,
            OneTimePreKeyPublic = bobDevice1OneTimePreKeys[0].PublicKey,
            OneTimePreKeyId = bobDevice1OneTimePreKeys[0].KeyId
        };

        var initResultDevice1 = X3dhInitiator.Initiate(bobDevice1Bundle);
        Assert.IsNotNull(initResultDevice1.RootKey);
        Assert.IsNotNull(initResultDevice1.ChainKey);

        // ================================================================
        // 5. Alice performs X3DH with Bob's Device2 -> session2
        // ================================================================
        var bobDevice2Bundle = new PreKeyBundle
        {
            IdentityKeyClassical = bobDevice2Identity.ClassicalPublicKey,
            IdentityKeyPostQuantum = bobDevice2Identity.PostQuantumPublicKey,
            SignedPreKeyPublic = bobDevice2SignedPreKey.PublicKey,
            SignedPreKeySignature = bobDevice2SignedPreKey.Signature,
            SignedPreKeyId = bobDevice2SignedPreKey.KeyId,
            KyberPreKeyPublic = bobDevice2KyberPreKey.PublicKey,
            KyberPreKeySignature = bobDevice2KyberPreKey.Signature,
            OneTimePreKeyPublic = bobDevice2OneTimePreKeys[0].PublicKey,
            OneTimePreKeyId = bobDevice2OneTimePreKeys[0].KeyId
        };

        var initResultDevice2 = X3dhInitiator.Initiate(bobDevice2Bundle);
        Assert.IsNotNull(initResultDevice2.RootKey);
        Assert.IsNotNull(initResultDevice2.ChainKey);

        // Verify the two sessions have different root keys (different key material)
        CollectionAssert.AreNotEqual(initResultDevice1.RootKey, initResultDevice2.RootKey);

        // ================================================================
        // Bob's Device1 responds to X3DH
        // ================================================================
        var (bobD1RootKey, bobD1ChainKey) = X3dhResponder.Respond(
            signedPreKeyPrivate: bobDevice1SignedPreKey.PrivateKey,
            kyberPreKeyPrivate: bobDevice1KyberPreKey.PrivateKey,
            oneTimePreKeyPrivate: bobDevice1OneTimePreKeys[0].PrivateKey,
            aliceEphemeralPublicKey: initResultDevice1.EphemeralPublicKey,
            kemCiphertext: initResultDevice1.KemCiphertext);

        CollectionAssert.AreEqual(initResultDevice1.RootKey, bobD1RootKey);
        CollectionAssert.AreEqual(initResultDevice1.ChainKey, bobD1ChainKey);

        // ================================================================
        // Bob's Device2 responds to X3DH
        // ================================================================
        var (bobD2RootKey, bobD2ChainKey) = X3dhResponder.Respond(
            signedPreKeyPrivate: bobDevice2SignedPreKey.PrivateKey,
            kyberPreKeyPrivate: bobDevice2KyberPreKey.PrivateKey,
            oneTimePreKeyPrivate: bobDevice2OneTimePreKeys[0].PrivateKey,
            aliceEphemeralPublicKey: initResultDevice2.EphemeralPublicKey,
            kemCiphertext: initResultDevice2.KemCiphertext);

        CollectionAssert.AreEqual(initResultDevice2.RootKey, bobD2RootKey);
        CollectionAssert.AreEqual(initResultDevice2.ChainKey, bobD2ChainKey);

        // ================================================================
        // 6. Initialize Double Ratchet sessions for both devices
        // ================================================================
        var bobD1Ratchet = DoubleRatchet.InitializeAsResponder(
            bobD1RootKey, bobDevice1SignedPreKey.PrivateKey, bobDevice1SignedPreKey.PublicKey);
        var aliceToD1Ratchet = DoubleRatchet.InitializeAsInitiator(
            initResultDevice1.RootKey, bobDevice1SignedPreKey.PublicKey);

        var bobD2Ratchet = DoubleRatchet.InitializeAsResponder(
            bobD2RootKey, bobDevice2SignedPreKey.PrivateKey, bobDevice2SignedPreKey.PublicKey);
        var aliceToD2Ratchet = DoubleRatchet.InitializeAsInitiator(
            initResultDevice2.RootKey, bobDevice2SignedPreKey.PublicKey);

        // ================================================================
        // 7. Alice encrypts the same message for both sessions (fan-out)
        // ================================================================
        var messageText = "Hello Bob, this is a fan-out message!";
        var plaintext = Encoding.UTF8.GetBytes(messageText);

        var (ciphertextD1, headerD1) = aliceToD1Ratchet.Encrypt(plaintext);
        var (ciphertextD2, headerD2) = aliceToD2Ratchet.Encrypt(plaintext);

        // ================================================================
        // 8. Bob's Device1 decrypts its ciphertext -> verify matches
        // ================================================================
        var decryptedD1 = bobD1Ratchet.Decrypt(ciphertextD1, headerD1);
        Assert.AreEqual(messageText, Encoding.UTF8.GetString(decryptedD1));

        // ================================================================
        // 9. Bob's Device2 decrypts its ciphertext -> verify matches
        // ================================================================
        var decryptedD2 = bobD2Ratchet.Decrypt(ciphertextD2, headerD2);
        Assert.AreEqual(messageText, Encoding.UTF8.GetString(decryptedD2));

        // ================================================================
        // 10. Verify the two ciphertexts are DIFFERENT
        //     (different sessions, different keys)
        // ================================================================
        CollectionAssert.AreNotEqual(ciphertextD1, ciphertextD2);
    }

    [TestMethod]
    public void FanOutEncryption_DevicesCanReplyIndependently()
    {
        // Tests that each device can independently reply to Alice
        // after receiving a fan-out message.

        // Setup: Alice + Bob with 2 devices
        var aliceIdentity = IdentityKeyGenerator.Generate();

        var bobD1Identity = IdentityKeyGenerator.Generate();
        var bobD1Spk = PreKeyGenerator.GenerateSignedPreKey(1, bobD1Identity.ClassicalPrivateKey, bobD1Identity.PostQuantumPrivateKey);
        var bobD1Kpk = PreKeyGenerator.GenerateKyberPreKey(bobD1Identity.ClassicalPrivateKey, bobD1Identity.PostQuantumPrivateKey);
        var bobD1Otpks = PreKeyGenerator.GenerateOneTimePreKeys(1, 3);

        var bobD2Identity = IdentityKeyGenerator.Generate();
        var bobD2Spk = PreKeyGenerator.GenerateSignedPreKey(1, bobD2Identity.ClassicalPrivateKey, bobD2Identity.PostQuantumPrivateKey);
        var bobD2Kpk = PreKeyGenerator.GenerateKyberPreKey(bobD2Identity.ClassicalPrivateKey, bobD2Identity.PostQuantumPrivateKey);
        var bobD2Otpks = PreKeyGenerator.GenerateOneTimePreKeys(10, 3);

        // X3DH for both devices
        var bundle1 = new PreKeyBundle
        {
            IdentityKeyClassical = bobD1Identity.ClassicalPublicKey,
            IdentityKeyPostQuantum = bobD1Identity.PostQuantumPublicKey,
            SignedPreKeyPublic = bobD1Spk.PublicKey,
            SignedPreKeySignature = bobD1Spk.Signature,
            SignedPreKeyId = bobD1Spk.KeyId,
            KyberPreKeyPublic = bobD1Kpk.PublicKey,
            KyberPreKeySignature = bobD1Kpk.Signature,
            OneTimePreKeyPublic = bobD1Otpks[0].PublicKey,
            OneTimePreKeyId = bobD1Otpks[0].KeyId
        };
        var init1 = X3dhInitiator.Initiate(bundle1);
        var (rk1, _) = X3dhResponder.Respond(bobD1Spk.PrivateKey, bobD1Kpk.PrivateKey, bobD1Otpks[0].PrivateKey, init1.EphemeralPublicKey, init1.KemCiphertext);

        var bundle2 = new PreKeyBundle
        {
            IdentityKeyClassical = bobD2Identity.ClassicalPublicKey,
            IdentityKeyPostQuantum = bobD2Identity.PostQuantumPublicKey,
            SignedPreKeyPublic = bobD2Spk.PublicKey,
            SignedPreKeySignature = bobD2Spk.Signature,
            SignedPreKeyId = bobD2Spk.KeyId,
            KyberPreKeyPublic = bobD2Kpk.PublicKey,
            KyberPreKeySignature = bobD2Kpk.Signature,
            OneTimePreKeyPublic = bobD2Otpks[0].PublicKey,
            OneTimePreKeyId = bobD2Otpks[0].KeyId
        };
        var init2 = X3dhInitiator.Initiate(bundle2);
        var (rk2, _) = X3dhResponder.Respond(bobD2Spk.PrivateKey, bobD2Kpk.PrivateKey, bobD2Otpks[0].PrivateKey, init2.EphemeralPublicKey, init2.KemCiphertext);

        // Initialize Double Ratchet sessions
        var aliceToD1 = DoubleRatchet.InitializeAsInitiator(init1.RootKey, bobD1Spk.PublicKey);
        var bobD1Ratchet = DoubleRatchet.InitializeAsResponder(rk1, bobD1Spk.PrivateKey, bobD1Spk.PublicKey);

        var aliceToD2 = DoubleRatchet.InitializeAsInitiator(init2.RootKey, bobD2Spk.PublicKey);
        var bobD2Ratchet = DoubleRatchet.InitializeAsResponder(rk2, bobD2Spk.PrivateKey, bobD2Spk.PublicKey);

        // Alice sends fan-out message
        var msg = Encoding.UTF8.GetBytes("Fan-out hello");
        var (ct1, h1) = aliceToD1.Encrypt(msg);
        var (ct2, h2) = aliceToD2.Encrypt(msg);

        Assert.AreEqual("Fan-out hello", Encoding.UTF8.GetString(bobD1Ratchet.Decrypt(ct1, h1)));
        Assert.AreEqual("Fan-out hello", Encoding.UTF8.GetString(bobD2Ratchet.Decrypt(ct2, h2)));

        // Device1 replies to Alice
        var d1Reply = Encoding.UTF8.GetBytes("Reply from Device 1");
        var (ctReply1, hReply1) = bobD1Ratchet.Encrypt(d1Reply);
        var decryptedReply1 = aliceToD1.Decrypt(ctReply1, hReply1);
        Assert.AreEqual("Reply from Device 1", Encoding.UTF8.GetString(decryptedReply1));

        // Device2 replies to Alice independently
        var d2Reply = Encoding.UTF8.GetBytes("Reply from Device 2");
        var (ctReply2, hReply2) = bobD2Ratchet.Encrypt(d2Reply);
        var decryptedReply2 = aliceToD2.Decrypt(ctReply2, hReply2);
        Assert.AreEqual("Reply from Device 2", Encoding.UTF8.GetString(decryptedReply2));

        // Verify the replies are different ciphertexts
        CollectionAssert.AreNotEqual(ctReply1, ctReply2);
    }
}
