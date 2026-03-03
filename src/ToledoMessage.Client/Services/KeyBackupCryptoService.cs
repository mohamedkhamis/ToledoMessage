using System.Text;
using System.Text.Json;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using ToledoMessage.Crypto.Classical;

namespace ToledoMessage.Client.Services;

public class KeyBackupCryptoService
{
    private const int Pbkdf2Iterations = 100_000;
    private const int KeyLengthBytes = 32;
    private const int SaltLengthBytes = 16;
    private const int NonceLengthBytes = 12;

    public (byte[] EncryptedBlob, byte[] Salt, byte[] Nonce) Encrypt(KeyBackupPayload payload, string password)
    {
        var json = JsonSerializer.Serialize(payload);
        var plaintext = Encoding.UTF8.GetBytes(json);

        var salt = new byte[SaltLengthBytes];
        var nonce = new byte[NonceLengthBytes];
        var random = new SecureRandom();
        random.NextBytes(salt);
        random.NextBytes(nonce);

        var key = DeriveKey(password, salt);
        var ciphertext = AesGcmCipher.Encrypt(key, nonce, plaintext);

        return (ciphertext, salt, nonce);
    }

    public KeyBackupPayload Decrypt(byte[] encryptedBlob, byte[] salt, byte[] nonce, string password)
    {
        var key = DeriveKey(password, salt);
        var plaintext = AesGcmCipher.Decrypt(key, nonce, encryptedBlob);
        var json = Encoding.UTF8.GetString(plaintext);
        return JsonSerializer.Deserialize<KeyBackupPayload>(json)
               ?? throw new InvalidOperationException("Failed to deserialize key backup payload.");
    }

    private static byte[] DeriveKey(string password, byte[] salt)
    {
        var generator = new Pkcs5S2ParametersGenerator(new Sha256Digest());
        generator.Init(Encoding.UTF8.GetBytes(password), salt, Pbkdf2Iterations);
        var keyParam = (KeyParameter)generator.GenerateDerivedMacParameters(KeyLengthBytes * 8);
        return keyParam.GetKey();
    }
}

public class KeyBackupPayload
{
    public byte[] ClassicalPrivateKey { get; set; } = [];
    public byte[] ClassicalPublicKey { get; set; } = [];
    public byte[] PostQuantumPrivateKey { get; set; } = [];
    public byte[] PostQuantumPublicKey { get; set; } = [];
}
