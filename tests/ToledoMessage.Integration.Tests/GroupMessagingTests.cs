using System.Text;
using ToledoMessage.Crypto.KeyManagement;
using ToledoMessage.Crypto.Protocol;

namespace ToledoMessage.Integration.Tests;

[TestClass]
public class GroupMessagingTests
{
    [TestMethod]
    public void GroupMessage_ThreeUsers_AllDecryptCorrectly()
    {
        // ================================================================
        // 1. Alice, Bob, and Charlie each generate identity + pre-keys
        // ================================================================
        var aliceIdentity = IdentityKeyGenerator.Generate();

        var bobIdentity = IdentityKeyGenerator.Generate();
        var bobSignedPreKey = PreKeyGenerator.GenerateSignedPreKey(
            1, bobIdentity.ClassicalPrivateKey, bobIdentity.PostQuantumPrivateKey);
        var bobKyberPreKey = PreKeyGenerator.GenerateKyberPreKey(
            bobIdentity.ClassicalPrivateKey, bobIdentity.PostQuantumPrivateKey);
        var bobOneTimePreKeys = PreKeyGenerator.GenerateOneTimePreKeys(1, 5);

        var charlieIdentity = IdentityKeyGenerator.Generate();
        var charlieSignedPreKey = PreKeyGenerator.GenerateSignedPreKey(
            1, charlieIdentity.ClassicalPrivateKey, charlieIdentity.PostQuantumPrivateKey);
        var charlieKyberPreKey = PreKeyGenerator.GenerateKyberPreKey(
            charlieIdentity.ClassicalPrivateKey, charlieIdentity.PostQuantumPrivateKey);
        var charlieOneTimePreKeys = PreKeyGenerator.GenerateOneTimePreKeys(1, 5);

        // ================================================================
        // 2. Alice performs X3DH with Bob -> session_ab
        // ================================================================
        var bobBundle = new PreKeyBundle
        {
            IdentityKeyClassical = bobIdentity.ClassicalPublicKey,
            IdentityKeyPostQuantum = bobIdentity.PostQuantumPublicKey,
            SignedPreKeyPublic = bobSignedPreKey.PublicKey,
            SignedPreKeySignature = bobSignedPreKey.Signature,
            SignedPreKeyId = bobSignedPreKey.KeyId,
            KyberPreKeyPublic = bobKyberPreKey.PublicKey,
            KyberPreKeySignature = bobKyberPreKey.Signature,
            OneTimePreKeyPublic = bobOneTimePreKeys[0].PublicKey,
            OneTimePreKeyId = bobOneTimePreKeys[0].KeyId
        };

        var initBob = X3dhInitiator.Initiate(bobBundle);
        var (bobRootKey, _) = X3dhResponder.Respond(
            bobSignedPreKey.PrivateKey, bobKyberPreKey.PrivateKey,
            bobOneTimePreKeys[0].PrivateKey, initBob.EphemeralPublicKey, initBob.KemCiphertext);

        CollectionAssert.AreEqual(initBob.RootKey, bobRootKey);

        // ================================================================
        // 3. Alice performs X3DH with Charlie -> session_ac
        // ================================================================
        var charlieBundle = new PreKeyBundle
        {
            IdentityKeyClassical = charlieIdentity.ClassicalPublicKey,
            IdentityKeyPostQuantum = charlieIdentity.PostQuantumPublicKey,
            SignedPreKeyPublic = charlieSignedPreKey.PublicKey,
            SignedPreKeySignature = charlieSignedPreKey.Signature,
            SignedPreKeyId = charlieSignedPreKey.KeyId,
            KyberPreKeyPublic = charlieKyberPreKey.PublicKey,
            KyberPreKeySignature = charlieKyberPreKey.Signature,
            OneTimePreKeyPublic = charlieOneTimePreKeys[0].PublicKey,
            OneTimePreKeyId = charlieOneTimePreKeys[0].KeyId
        };

        var initCharlie = X3dhInitiator.Initiate(charlieBundle);
        var (charlieRootKey, _) = X3dhResponder.Respond(
            charlieSignedPreKey.PrivateKey, charlieKyberPreKey.PrivateKey,
            charlieOneTimePreKeys[0].PrivateKey, initCharlie.EphemeralPublicKey, initCharlie.KemCiphertext);

        CollectionAssert.AreEqual(initCharlie.RootKey, charlieRootKey);

        // ================================================================
        // Initialize Double Ratchet sessions
        // ================================================================
        var aliceToBobRatchet = DoubleRatchet.InitializeAsInitiator(
            initBob.RootKey, bobSignedPreKey.PublicKey);
        var bobFromAliceRatchet = DoubleRatchet.InitializeAsResponder(
            bobRootKey, bobSignedPreKey.PrivateKey, bobSignedPreKey.PublicKey);

        var aliceToCharlieRatchet = DoubleRatchet.InitializeAsInitiator(
            initCharlie.RootKey, charlieSignedPreKey.PublicKey);
        var charlieFromAliceRatchet = DoubleRatchet.InitializeAsResponder(
            charlieRootKey, charlieSignedPreKey.PrivateKey, charlieSignedPreKey.PublicKey);

        // ================================================================
        // 4. Alice sends a group message by encrypting independently for Bob and Charlie
        // ================================================================
        var groupMessage = "Hello group! This is Alice.";
        var plaintext = Encoding.UTF8.GetBytes(groupMessage);

        var (ciphertextForBob, headerForBob) = aliceToBobRatchet.Encrypt(plaintext);
        var (ciphertextForCharlie, headerForCharlie) = aliceToCharlieRatchet.Encrypt(plaintext);

        // ================================================================
        // 5. Bob decrypts his copy -> verify matches
        // ================================================================
        var bobDecrypted = bobFromAliceRatchet.Decrypt(ciphertextForBob, headerForBob);
        Assert.AreEqual(groupMessage, Encoding.UTF8.GetString(bobDecrypted));

        // ================================================================
        // 6. Charlie decrypts his copy -> verify matches
        // ================================================================
        var charlieDecrypted = charlieFromAliceRatchet.Decrypt(ciphertextForCharlie, headerForCharlie);
        Assert.AreEqual(groupMessage, Encoding.UTF8.GetString(charlieDecrypted));

        // ================================================================
        // 7. Verify Bob's and Charlie's ciphertexts are different
        //    (different pairwise sessions => different keys)
        // ================================================================
        CollectionAssert.AreNotEqual(ciphertextForBob, ciphertextForCharlie);

        // ================================================================
        // 8. Bob sends a reply (must encrypt for Alice and Charlie)
        //    For Bob->Alice: use the existing session
        //    For Bob->Charlie: need a new pairwise session
        // ================================================================

        // Bob needs a session with Charlie for group replies
        // Charlie publishes a second one-time pre-key for Bob
        var charlieBundle2 = new PreKeyBundle
        {
            IdentityKeyClassical = charlieIdentity.ClassicalPublicKey,
            IdentityKeyPostQuantum = charlieIdentity.PostQuantumPublicKey,
            SignedPreKeyPublic = charlieSignedPreKey.PublicKey,
            SignedPreKeySignature = charlieSignedPreKey.Signature,
            SignedPreKeyId = charlieSignedPreKey.KeyId,
            KyberPreKeyPublic = charlieKyberPreKey.PublicKey,
            KyberPreKeySignature = charlieKyberPreKey.Signature,
            OneTimePreKeyPublic = charlieOneTimePreKeys[1].PublicKey,
            OneTimePreKeyId = charlieOneTimePreKeys[1].KeyId
        };

        var bobInitCharlie = X3dhInitiator.Initiate(charlieBundle2);
        var (charlieFromBobRootKey, _) = X3dhResponder.Respond(
            charlieSignedPreKey.PrivateKey, charlieKyberPreKey.PrivateKey,
            charlieOneTimePreKeys[1].PrivateKey, bobInitCharlie.EphemeralPublicKey, bobInitCharlie.KemCiphertext);

        CollectionAssert.AreEqual(bobInitCharlie.RootKey, charlieFromBobRootKey);

        var bobToCharlieRatchet = DoubleRatchet.InitializeAsInitiator(
            bobInitCharlie.RootKey, charlieSignedPreKey.PublicKey);
        var charlieFromBobRatchet = DoubleRatchet.InitializeAsResponder(
            charlieFromBobRootKey, charlieSignedPreKey.PrivateKey, charlieSignedPreKey.PublicKey);

        // Bob sends reply to the group
        var bobReply = "Hey everyone, Bob here!";
        var replyPlaintext = Encoding.UTF8.GetBytes(bobReply);

        // Encrypt for Alice (via existing session)
        var (replyCtForAlice, replyHdrForAlice) = bobFromAliceRatchet.Encrypt(replyPlaintext);
        // Encrypt for Charlie (via new session)
        var (replyCtForCharlie, replyHdrForCharlie) = bobToCharlieRatchet.Encrypt(replyPlaintext);

        // ================================================================
        // 9. Alice and Charlie both decrypt -> verify matches
        // ================================================================
        var aliceDecryptedReply = aliceToBobRatchet.Decrypt(replyCtForAlice, replyHdrForAlice);
        Assert.AreEqual(bobReply, Encoding.UTF8.GetString(aliceDecryptedReply));

        var charlieDecryptedReply = charlieFromBobRatchet.Decrypt(replyCtForCharlie, replyHdrForCharlie);
        Assert.AreEqual(bobReply, Encoding.UTF8.GetString(charlieDecryptedReply));

        // Verify the reply ciphertexts are also different
        CollectionAssert.AreNotEqual(replyCtForAlice, replyCtForCharlie);
    }

    [TestMethod]
    public void GroupMessage_MultipleRoundsOfConversation()
    {
        // Tests multiple rounds of group messaging to verify
        // the Double Ratchet advances correctly in a pairwise group context.

        // Setup three users
        var aliceIdentity = IdentityKeyGenerator.Generate();
        var bobIdentity = IdentityKeyGenerator.Generate();
        var charlieIdentity = IdentityKeyGenerator.Generate();

        var bobSpk = PreKeyGenerator.GenerateSignedPreKey(1, bobIdentity.ClassicalPrivateKey, bobIdentity.PostQuantumPrivateKey);
        var bobKpk = PreKeyGenerator.GenerateKyberPreKey(bobIdentity.ClassicalPrivateKey, bobIdentity.PostQuantumPrivateKey);
        var bobOtpks = PreKeyGenerator.GenerateOneTimePreKeys(1, 5);

        var charlieSpk = PreKeyGenerator.GenerateSignedPreKey(1, charlieIdentity.ClassicalPrivateKey, charlieIdentity.PostQuantumPrivateKey);
        var charlieKpk = PreKeyGenerator.GenerateKyberPreKey(charlieIdentity.ClassicalPrivateKey, charlieIdentity.PostQuantumPrivateKey);
        var charlieOtpks = PreKeyGenerator.GenerateOneTimePreKeys(1, 5);

        // Alice -> Bob session
        var bobBundle = new PreKeyBundle
        {
            IdentityKeyClassical = bobIdentity.ClassicalPublicKey,
            IdentityKeyPostQuantum = bobIdentity.PostQuantumPublicKey,
            SignedPreKeyPublic = bobSpk.PublicKey,
            SignedPreKeySignature = bobSpk.Signature,
            SignedPreKeyId = bobSpk.KeyId,
            KyberPreKeyPublic = bobKpk.PublicKey,
            KyberPreKeySignature = bobKpk.Signature,
            OneTimePreKeyPublic = bobOtpks[0].PublicKey,
            OneTimePreKeyId = bobOtpks[0].KeyId
        };
        var initBob = X3dhInitiator.Initiate(bobBundle);
        var (bobRk, _) = X3dhResponder.Respond(
            bobSpk.PrivateKey, bobKpk.PrivateKey, bobOtpks[0].PrivateKey,
            initBob.EphemeralPublicKey, initBob.KemCiphertext);

        // Alice -> Charlie session
        var charlieBundle = new PreKeyBundle
        {
            IdentityKeyClassical = charlieIdentity.ClassicalPublicKey,
            IdentityKeyPostQuantum = charlieIdentity.PostQuantumPublicKey,
            SignedPreKeyPublic = charlieSpk.PublicKey,
            SignedPreKeySignature = charlieSpk.Signature,
            SignedPreKeyId = charlieSpk.KeyId,
            KyberPreKeyPublic = charlieKpk.PublicKey,
            KyberPreKeySignature = charlieKpk.Signature,
            OneTimePreKeyPublic = charlieOtpks[0].PublicKey,
            OneTimePreKeyId = charlieOtpks[0].KeyId
        };
        var initCharlie = X3dhInitiator.Initiate(charlieBundle);
        var (charlieRk, _) = X3dhResponder.Respond(
            charlieSpk.PrivateKey, charlieKpk.PrivateKey, charlieOtpks[0].PrivateKey,
            initCharlie.EphemeralPublicKey, initCharlie.KemCiphertext);

        // Double Ratchet sessions for Alice <-> Bob, Alice <-> Charlie
        var aliceToBob = DoubleRatchet.InitializeAsInitiator(initBob.RootKey, bobSpk.PublicKey);
        var bobFromAlice = DoubleRatchet.InitializeAsResponder(bobRk, bobSpk.PrivateKey, bobSpk.PublicKey);

        var aliceToCharlie = DoubleRatchet.InitializeAsInitiator(initCharlie.RootKey, charlieSpk.PublicKey);
        var charlieFromAlice = DoubleRatchet.InitializeAsResponder(charlieRk, charlieSpk.PrivateKey, charlieSpk.PublicKey);

        // Send 5 group messages from Alice, verify all decrypt correctly
        for (int i = 0; i < 5; i++)
        {
            var msg = $"Alice group message #{i + 1}";
            var plaintext = Encoding.UTF8.GetBytes(msg);

            var (ctBob, hdrBob) = aliceToBob.Encrypt(plaintext);
            var (ctCharlie, hdrCharlie) = aliceToCharlie.Encrypt(plaintext);

            var bobDecrypted = bobFromAlice.Decrypt(ctBob, hdrBob);
            Assert.AreEqual(msg, Encoding.UTF8.GetString(bobDecrypted));

            var charlieDecrypted = charlieFromAlice.Decrypt(ctCharlie, hdrCharlie);
            Assert.AreEqual(msg, Encoding.UTF8.GetString(charlieDecrypted));

            // Each round produces different ciphertexts
            CollectionAssert.AreNotEqual(ctBob, ctCharlie);
        }

        // Bob replies to Alice (via existing session)
        for (int i = 0; i < 3; i++)
        {
            var reply = $"Bob reply #{i + 1}";
            var replyBytes = Encoding.UTF8.GetBytes(reply);

            var (ct, hdr) = bobFromAlice.Encrypt(replyBytes);
            var decrypted = aliceToBob.Decrypt(ct, hdr);
            Assert.AreEqual(reply, Encoding.UTF8.GetString(decrypted));
        }
    }
}
