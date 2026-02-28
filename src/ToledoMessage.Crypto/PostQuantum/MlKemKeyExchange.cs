using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Kems;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;

namespace ToledoMessage.Crypto.PostQuantum;

public static class MlKemKeyExchange
{
    public static (byte[] publicKey, byte[] privateKey) GenerateKeyPair()
    {
        var generator = new MLKemKeyPairGenerator();
        generator.Init(new MLKemKeyGenerationParameters(new SecureRandom(), MLKemParameters.ml_kem_768));

        var keyPair = generator.GenerateKeyPair();

        var publicKey = ((MLKemPublicKeyParameters)keyPair.Public).GetEncoded();
        var privateKey = ((MLKemPrivateKeyParameters)keyPair.Private).GetEncoded();

        return (publicKey, privateKey);
    }

    public static (byte[] ciphertext, byte[] sharedSecret) Encapsulate(byte[] publicKey)
    {
        var publicKeyParams = MLKemPublicKeyParameters.FromEncoding(MLKemParameters.ml_kem_768, publicKey);

        var encapsulator = new MLKemEncapsulator(MLKemParameters.ml_kem_768);
        encapsulator.Init(publicKeyParams);

        var ciphertext = new byte[encapsulator.EncapsulationLength];
        var sharedSecret = new byte[encapsulator.SecretLength];
        encapsulator.Encapsulate(ciphertext, 0, ciphertext.Length, sharedSecret, 0, sharedSecret.Length);

        return (ciphertext, sharedSecret);
    }

    public static byte[] Decapsulate(byte[] privateKey, byte[] ciphertext)
    {
        var privateKeyParams = MLKemPrivateKeyParameters.FromEncoding(MLKemParameters.ml_kem_768, privateKey);

        var decapsulator = new MLKemDecapsulator(MLKemParameters.ml_kem_768);
        decapsulator.Init(privateKeyParams);

        var sharedSecret = new byte[decapsulator.SecretLength];
        decapsulator.Decapsulate(ciphertext, 0, ciphertext.Length, sharedSecret, 0, sharedSecret.Length);

        return sharedSecret;
    }
}
