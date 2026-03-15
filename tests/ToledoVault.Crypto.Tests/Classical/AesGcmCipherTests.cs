using Org.BouncyCastle.Security;
using ToledoVault.Crypto.Classical;

namespace ToledoVault.Crypto.Tests.Classical;

[TestClass]
public class AesGcmCipherTests
{
    private static byte[] GenerateRandomBytes(int length)
    {
        var random = new SecureRandom();
        var bytes = new byte[length];
        random.NextBytes(bytes);
        return bytes;
    }

    [TestMethod]
    public void Encrypt_Decrypt_RoundTrip()
    {
        var key = GenerateRandomBytes(32);
        var nonce = GenerateRandomBytes(12);
        var plaintext = "Hello, Toledo!"u8.ToArray();

        var ciphertext = AesGcmCipher.Encrypt(key, nonce, plaintext);
        var decrypted = AesGcmCipher.Decrypt(key, nonce, ciphertext);

        CollectionAssert.AreEqual(plaintext, decrypted);
    }

    [TestMethod]
    public void Decrypt_RejectsTamperedCiphertext()
    {
        var key = GenerateRandomBytes(32);
        var nonce = GenerateRandomBytes(12);
        var plaintext = "Hello, Toledo!"u8.ToArray();

        var ciphertext = AesGcmCipher.Encrypt(key, nonce, plaintext);

        ciphertext[0] ^= 0xFF;

        var threw = false;
        try
        {
            AesGcmCipher.Decrypt(key, nonce, ciphertext);
        }
        catch
        {
            threw = true;
        }

        Assert.IsTrue(threw);
    }

    [TestMethod]
    public void Encrypt_Decrypt_WithAssociatedData()
    {
        var key = GenerateRandomBytes(32);
        var nonce = GenerateRandomBytes(12);
        var plaintext = "Hello, Toledo!"u8.ToArray();
        var aad = "associated-data"u8.ToArray();

        var ciphertext = AesGcmCipher.Encrypt(key, nonce, plaintext, aad);
        var decrypted = AesGcmCipher.Decrypt(key, nonce, ciphertext, aad);

        CollectionAssert.AreEqual(plaintext, decrypted);
    }

    [TestMethod]
    public void Decrypt_RejectsMismatchedAssociatedData()
    {
        var key = GenerateRandomBytes(32);
        var nonce = GenerateRandomBytes(12);
        var plaintext = "Hello, Toledo!"u8.ToArray();
        var aad = "associated-data"u8.ToArray();
        var wrongAad = "wrong-associated-data"u8.ToArray();

        var ciphertext = AesGcmCipher.Encrypt(key, nonce, plaintext, aad);

        var threw = false;
        try
        {
            AesGcmCipher.Decrypt(key, nonce, ciphertext, wrongAad);
        }
        catch
        {
            threw = true;
        }

        Assert.IsTrue(threw);
    }

    [TestMethod]
    public void Encrypt_DifferentNonces_ProduceDifferentCiphertexts()
    {
        var key = GenerateRandomBytes(32);
        var nonce1 = GenerateRandomBytes(12);
        var nonce2 = GenerateRandomBytes(12);
        var plaintext = "Hello, Toledo!"u8.ToArray();

        var ciphertext1 = AesGcmCipher.Encrypt(key, nonce1, plaintext);
        var ciphertext2 = AesGcmCipher.Encrypt(key, nonce2, plaintext);

        CollectionAssert.AreNotEqual(ciphertext1, ciphertext2);
    }
}
