using System.Security.Cryptography;
using System.Text;
using ToledoMessage.Crypto.Classical;
using ToledoMessage.Crypto.Protocol;

namespace ToledoMessage.Crypto.Tests.Protocol;

[TestClass]
public class DoubleRatchetTests
{
    /// <summary>
    /// Creates an Alice/Bob session pair simulating the result of an X3DH handshake.
    /// </summary>
    private static (DoubleRatchet alice, DoubleRatchet bob) CreateSessionPair()
    {
        // Generate Bob's "signed pre-key" (serves as initial ratchet key)
        var (bobSpkPub, bobSpkPriv) = X25519KeyExchange.GenerateKeyPair();

        // Simulate X3DH shared secret: both sides derive the same root key
        var sharedSecret = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(sharedSecret);

        // Initialize Alice (initiator) - she performs a DH ratchet step internally
        var alice = DoubleRatchet.InitializeAsInitiator(sharedSecret, bobSpkPub);

        // Initialize Bob (responder) - he uses his SPK as initial ratchet key
        var bob = DoubleRatchet.InitializeAsResponder(sharedSecret, bobSpkPriv, bobSpkPub);

        return (alice, bob);
    }

    [TestMethod]
    public void Encrypt_Decrypt_RoundTrip()
    {
        var (alice, bob) = CreateSessionPair();
        var plaintext = "Hello, Bob!"u8.ToArray();

        var (ciphertext, header) = alice.Encrypt(plaintext);
        var decrypted = bob.Decrypt(ciphertext, header);

        CollectionAssert.AreEqual(plaintext, decrypted);
    }

    [TestMethod]
    public void MultipleMessages_EncryptDecrypt()
    {
        var (alice, bob) = CreateSessionPair();

        for (int i = 0; i < 5; i++)
        {
            var plaintext = Encoding.UTF8.GetBytes($"Message number {i}");
            var (ciphertext, header) = alice.Encrypt(plaintext);
            var decrypted = bob.Decrypt(ciphertext, header);

            CollectionAssert.AreEqual(plaintext, decrypted);
            Assert.AreEqual(i, header.MessageIndex);
        }
    }

    [TestMethod]
    public void BidirectionalMessages()
    {
        var (alice, bob) = CreateSessionPair();

        // Alice sends to Bob
        var aliceMsg = "Hello Bob, from Alice"u8.ToArray();
        var (ct1, hdr1) = alice.Encrypt(aliceMsg);
        var decrypted1 = bob.Decrypt(ct1, hdr1);
        CollectionAssert.AreEqual(aliceMsg, decrypted1);

        // Bob sends to Alice
        var bobMsg = "Hello Alice, from Bob"u8.ToArray();
        var (ct2, hdr2) = bob.Encrypt(bobMsg);
        var decrypted2 = alice.Decrypt(ct2, hdr2);
        CollectionAssert.AreEqual(bobMsg, decrypted2);

        // Alice sends again
        var aliceMsg2 = "Another message from Alice"u8.ToArray();
        var (ct3, hdr3) = alice.Encrypt(aliceMsg2);
        var decrypted3 = bob.Decrypt(ct3, hdr3);
        CollectionAssert.AreEqual(aliceMsg2, decrypted3);

        // Bob sends again
        var bobMsg2 = "Another message from Bob"u8.ToArray();
        var (ct4, hdr4) = bob.Encrypt(bobMsg2);
        var decrypted4 = alice.Decrypt(ct4, hdr4);
        CollectionAssert.AreEqual(bobMsg2, decrypted4);
    }

    [TestMethod]
    public void OutOfOrderMessages()
    {
        var (alice, bob) = CreateSessionPair();

        // Alice sends 3 messages
        var msg1 = "Message 1"u8.ToArray();
        var msg2 = "Message 2"u8.ToArray();
        var msg3 = "Message 3"u8.ToArray();

        var (ct1, hdr1) = alice.Encrypt(msg1);
        var (ct2, hdr2) = alice.Encrypt(msg2);
        var (ct3, hdr3) = alice.Encrypt(msg3);

        // Bob decrypts message 3 first (skipping 1 and 2)
        var dec3 = bob.Decrypt(ct3, hdr3);
        CollectionAssert.AreEqual(msg3, dec3);

        // Bob decrypts message 1 (out of order, from skipped keys)
        var dec1 = bob.Decrypt(ct1, hdr1);
        CollectionAssert.AreEqual(msg1, dec1);

        // Bob decrypts message 2 (out of order, from skipped keys)
        var dec2 = bob.Decrypt(ct2, hdr2);
        CollectionAssert.AreEqual(msg2, dec2);
    }

    [TestMethod]
    public void DhRatchetStep_NewKeys()
    {
        var (alice, bob) = CreateSessionPair();

        // Alice sends a message
        var msg1 = "First from Alice"u8.ToArray();
        var (ct1, hdr1) = alice.Encrypt(msg1);
        var dec1 = bob.Decrypt(ct1, hdr1);
        CollectionAssert.AreEqual(msg1, dec1);

        // Capture Alice's ratchet public key before Bob replies
        var aliceKeyBefore = alice.GetState().LocalRatchetPublicKey;

        // Bob replies - this triggers a DH ratchet step on Bob's side
        var msg2 = "Reply from Bob"u8.ToArray();
        var (ct2, hdr2) = bob.Encrypt(msg2);
        var dec2 = alice.Decrypt(ct2, hdr2);
        CollectionAssert.AreEqual(msg2, dec2);

        // Alice sends again - she should have performed a DH ratchet step upon decrypting Bob's reply
        var msg3 = "Second from Alice"u8.ToArray();
        var (ct3, hdr3) = alice.Encrypt(msg3);

        // Alice's ratchet key should have changed after receiving Bob's message
        var aliceKeyAfter = alice.GetState().LocalRatchetPublicKey;
        CollectionAssert.AreNotEqual(aliceKeyBefore, aliceKeyAfter);

        // Bob should still be able to decrypt
        var dec3 = bob.Decrypt(ct3, hdr3);
        CollectionAssert.AreEqual(msg3, dec3);
    }
}
