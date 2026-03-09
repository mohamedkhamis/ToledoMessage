using System.Diagnostics.CodeAnalysis;
using System.Text;
using ToledoMessage.Crypto.KeyManagement;
using ToledoMessage.Crypto.Protocol;

namespace ToledoMessage.Integration.Tests;

[SuppressMessage("ReSharper", "ArgumentsStyleLiteral")]
[SuppressMessage("ReSharper", "ArgumentsStyleNamedExpression")]
[TestClass]
public class TwoUserMessagingTests
{
    [TestMethod]
    public void FullTwoUserMessagingFlow()
    {
        // ================================================================
        // 1. Generate identity keys for Alice and Bob
        // ================================================================
        var bobIdentity = IdentityKeyGenerator.Generate();

        // ================================================================
        // 2. Bob generates signed pre-key, Kyber pre-key, one-time pre-keys
        // ================================================================
        var bobSignedPreKey = PreKeyGenerator.GenerateSignedPreKey(
            keyId: 1,
            identityClassicalPrivate: bobIdentity.ClassicalPrivateKey,
            identityPqPrivate: bobIdentity.PostQuantumPrivateKey);

        var bobKyberPreKey = PreKeyGenerator.GenerateKyberPreKey(
            identityClassicalPrivate: bobIdentity.ClassicalPrivateKey,
            identityPqPrivate: bobIdentity.PostQuantumPrivateKey);

        var bobOneTimePreKeys = PreKeyGenerator.GenerateOneTimePreKeys(startKeyId: 1, count: 5);

        // ================================================================
        // 3. Build Bob's PreKeyBundle (simulating server publishing)
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

        // ================================================================
        // 4. Alice initiates X3DH against Bob's bundle
        // ================================================================
        var initResult = X3dhInitiator.Initiate(bobBundle);

        Assert.IsNotNull(initResult.RootKey);
        Assert.IsNotNull(initResult.ChainKey);
        Assert.IsNotNull(initResult.EphemeralPublicKey);
        Assert.IsNotNull(initResult.KemCiphertext);
        Assert.AreEqual(32, initResult.RootKey.Length);
        Assert.AreEqual(32, initResult.ChainKey.Length);
        Assert.AreEqual(bobOneTimePreKeys[0].KeyId, initResult.UsedOneTimePreKeyId);

        // ================================================================
        // 5. Bob responds to X3DH (using data Alice sent)
        // ================================================================
        var (bobRootKey, bobChainKey) = X3dhResponder.Respond(
            signedPreKeyPrivate: bobSignedPreKey.PrivateKey,
            kyberPreKeyPrivate: bobKyberPreKey.PrivateKey,
            oneTimePreKeyPrivate: bobOneTimePreKeys[0].PrivateKey,
            aliceEphemeralPublicKey: initResult.EphemeralPublicKey,
            kemCiphertext: initResult.KemCiphertext);

        Assert.AreEqual(32, bobRootKey.Length);
        Assert.AreEqual(32, bobChainKey.Length);

        // Verify both sides derived the same root and chain keys
        CollectionAssert.AreEqual(initResult.RootKey, bobRootKey);
        CollectionAssert.AreEqual(initResult.ChainKey, bobChainKey);

        // ================================================================
        // 6. Both initialize Double Ratchet sessions
        // ================================================================
        // Bob initializes as responder using his signed pre-key as initial ratchet key
        var bobRatchet = DoubleRatchet.InitializeAsResponder(
            sharedSecret: bobRootKey,
            localRatchetPrivateKey: bobSignedPreKey.PrivateKey,
            localRatchetPublicKey: bobSignedPreKey.PublicKey);

        // Alice initializes as initiator with Bob's signed pre-key public as remote ratchet key
        var aliceRatchet = DoubleRatchet.InitializeAsInitiator(
            sharedSecret: initResult.RootKey,
            remoteRatchetPublicKey: bobSignedPreKey.PublicKey);

        // ================================================================
        // 7. Alice encrypts "Hello Bob" -> Bob decrypts -> assert
        // ================================================================
        var aliceMessage1 = "Hello Bob"u8.ToArray();
        var (ciphertext1, header1) = aliceRatchet.Encrypt(aliceMessage1);

        Assert.IsNotNull(ciphertext1);
        CollectionAssert.AreNotEqual(aliceMessage1, ciphertext1); // ciphertext differs from plaintext

        var decrypted1 = bobRatchet.Decrypt(ciphertext1, header1);
        Assert.AreEqual("Hello Bob", Encoding.UTF8.GetString(decrypted1));

        // ================================================================
        // 8. Bob encrypts "Hello Alice" -> Alice decrypts -> assert
        // ================================================================
        var bobMessage1 = "Hello Alice"u8.ToArray();
        var (ciphertext2, header2) = bobRatchet.Encrypt(bobMessage1);

        Assert.IsNotNull(ciphertext2);
        CollectionAssert.AreNotEqual(bobMessage1, ciphertext2);

        var decrypted2 = aliceRatchet.Decrypt(ciphertext2, header2);
        Assert.AreEqual("Hello Alice", Encoding.UTF8.GetString(decrypted2));

        // ================================================================
        // 9. Exchange 10 more messages alternating sender
        // ================================================================
        var expectedMessages = new List<string>();

        for (var i = 0; i < 10; i++)
        {
            string messageText;
            byte[] ciphertext;
            MessageHeader header;
            byte[] decrypted;

            if (i % 2 == 0)
            {
                // Alice sends to Bob
                messageText = $"Alice message #{i + 1}: The quick brown fox jumps over the lazy dog.";
                var plaintext = Encoding.UTF8.GetBytes(messageText);
                (ciphertext, header) = aliceRatchet.Encrypt(plaintext);
                decrypted = bobRatchet.Decrypt(ciphertext, header);
            }
            else
            {
                // Bob sends to Alice
                messageText = $"Bob message #{i + 1}: Pack my box with five dozen liquor jugs.";
                var plaintext = Encoding.UTF8.GetBytes(messageText);
                (ciphertext, header) = bobRatchet.Encrypt(plaintext);
                decrypted = aliceRatchet.Decrypt(ciphertext, header);
            }

            expectedMessages.Add(messageText);

            // ================================================================
            // 10. Verify all messages decrypt correctly
            // ================================================================
            Assert.AreEqual(messageText, Encoding.UTF8.GetString(decrypted));
        }

        // Final assertion: we exchanged the expected number of messages
        Assert.AreEqual(10, expectedMessages.Count);
    }

    [TestMethod]
    public void FullTwoUserMessagingFlow_WithoutOneTimePreKey()
    {
        // Tests the flow when no one-time pre-key is available (all consumed)

        // 1. Generate identity keys
        var bobIdentity = IdentityKeyGenerator.Generate();

        // 2. Bob generates pre-keys (no one-time pre-keys)
        var bobSignedPreKey = PreKeyGenerator.GenerateSignedPreKey(
            1, bobIdentity.ClassicalPrivateKey, bobIdentity.PostQuantumPrivateKey);
        var bobKyberPreKey = PreKeyGenerator.GenerateKyberPreKey(
            bobIdentity.ClassicalPrivateKey, bobIdentity.PostQuantumPrivateKey);

        // 3. Build bundle WITHOUT one-time pre-key
        var bobBundle = new PreKeyBundle
        {
            IdentityKeyClassical = bobIdentity.ClassicalPublicKey,
            IdentityKeyPostQuantum = bobIdentity.PostQuantumPublicKey,
            SignedPreKeyPublic = bobSignedPreKey.PublicKey,
            SignedPreKeySignature = bobSignedPreKey.Signature,
            SignedPreKeyId = bobSignedPreKey.KeyId,
            KyberPreKeyPublic = bobKyberPreKey.PublicKey,
            KyberPreKeySignature = bobKyberPreKey.Signature,
            OneTimePreKeyPublic = null,
            OneTimePreKeyId = null
        };

        // 4. Alice initiates X3DH
        var initResult = X3dhInitiator.Initiate(bobBundle);
        Assert.IsNull(initResult.UsedOneTimePreKeyId);

        // 5. Bob responds
        var (bobRootKey, bobChainKey) = X3dhResponder.Respond(
            bobSignedPreKey.PrivateKey,
            bobKyberPreKey.PrivateKey,
            oneTimePreKeyPrivate: null,
            initResult.EphemeralPublicKey,
            initResult.KemCiphertext);

        // Both derive same keys
        CollectionAssert.AreEqual(initResult.RootKey, bobRootKey);
        CollectionAssert.AreEqual(initResult.ChainKey, bobChainKey);

        // 6. Initialize Double Ratchet sessions
        var bobRatchet = DoubleRatchet.InitializeAsResponder(
            bobRootKey, bobSignedPreKey.PrivateKey, bobSignedPreKey.PublicKey);
        var aliceRatchet = DoubleRatchet.InitializeAsInitiator(
            initResult.RootKey, bobSignedPreKey.PublicKey);

        // 7. Exchange messages
        var (ct, hdr) = aliceRatchet.Encrypt("Without OPK"u8.ToArray());
        var decrypted = bobRatchet.Decrypt(ct, hdr);
        Assert.AreEqual("Without OPK", Encoding.UTF8.GetString(decrypted));

        var (ct2, hdr2) = bobRatchet.Encrypt("Reply without OPK"u8.ToArray());
        var decrypted2 = aliceRatchet.Decrypt(ct2, hdr2);
        Assert.AreEqual("Reply without OPK", Encoding.UTF8.GetString(decrypted2));
    }

    [TestMethod]
    public void ConsecutiveMessagesSameSender_DecryptCorrectly()
    {
        // Tests multiple messages from the same sender before the other replies
        // (no DH ratchet step between them, just symmetric ratchet)

        var bobIdentity = IdentityKeyGenerator.Generate();

        var bobSignedPreKey = PreKeyGenerator.GenerateSignedPreKey(
            1, bobIdentity.ClassicalPrivateKey, bobIdentity.PostQuantumPrivateKey);
        var bobKyberPreKey = PreKeyGenerator.GenerateKyberPreKey(
            bobIdentity.ClassicalPrivateKey, bobIdentity.PostQuantumPrivateKey);
        var bobOneTimePreKeys = PreKeyGenerator.GenerateOneTimePreKeys(1, 1);

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

        var initResult = X3dhInitiator.Initiate(bobBundle);
        var (bobRootKey, _) = X3dhResponder.Respond(
            bobSignedPreKey.PrivateKey, bobKyberPreKey.PrivateKey,
            bobOneTimePreKeys[0].PrivateKey, initResult.EphemeralPublicKey,
            initResult.KemCiphertext);

        var bobRatchet = DoubleRatchet.InitializeAsResponder(
            bobRootKey, bobSignedPreKey.PrivateKey, bobSignedPreKey.PublicKey);
        var aliceRatchet = DoubleRatchet.InitializeAsInitiator(
            initResult.RootKey, bobSignedPreKey.PublicKey);

        // Alice sends 5 messages in a row before Bob replies
        var aliceMessages = new List<(byte[] ciphertext, MessageHeader header)>();
        for (var i = 0; i < 5; i++)
        {
            var msg = Encoding.UTF8.GetBytes($"Alice burst message {i}");
            aliceMessages.Add(aliceRatchet.Encrypt(msg));
        }

        // Bob decrypts all 5
        for (var i = 0; i < 5; i++)
        {
            var decrypted = bobRatchet.Decrypt(aliceMessages[i].ciphertext, aliceMessages[i].header);
            Assert.AreEqual($"Alice burst message {i}", Encoding.UTF8.GetString(decrypted));
        }

        // Now Bob sends 3 messages in a row
        var bobMessages = new List<(byte[] ciphertext, MessageHeader header)>();
        for (var i = 0; i < 3; i++)
        {
            var msg = Encoding.UTF8.GetBytes($"Bob burst message {i}");
            bobMessages.Add(bobRatchet.Encrypt(msg));
        }

        // Alice decrypts all 3
        for (var i = 0; i < 3; i++)
        {
            var decrypted = aliceRatchet.Decrypt(bobMessages[i].ciphertext, bobMessages[i].header);
            Assert.AreEqual($"Bob burst message {i}", Encoding.UTF8.GetString(decrypted));
        }
    }

    [TestMethod]
    public void FingerprintVerification_BothSidesMatch()
    {
        // Tests that fingerprint generation produces the same result for both parties

        var aliceIdentity = IdentityKeyGenerator.Generate();
        var bobIdentity = IdentityKeyGenerator.Generate();

        var fingerprintAliceSees = FingerprintGenerator.GenerateFingerprint(
            aliceIdentity.ClassicalPublicKey, bobIdentity.ClassicalPublicKey);

        var fingerprintBobSees = FingerprintGenerator.GenerateFingerprint(
            bobIdentity.ClassicalPublicKey, aliceIdentity.ClassicalPublicKey);

        Assert.AreEqual(fingerprintAliceSees, fingerprintBobSees);

        // Verify format: 6 groups of 5 digits separated by spaces (35 chars total)
        Assert.AreEqual(35, fingerprintAliceSees.Length);
        var groups = fingerprintAliceSees.Split(' ');
        Assert.AreEqual(6, groups.Length);
        foreach (var group in groups)
        {
            Assert.AreEqual(5, group.Length);
            Assert.IsTrue(long.TryParse(group, out _), $"Group '{group}' should be numeric");
        }
    }
}
