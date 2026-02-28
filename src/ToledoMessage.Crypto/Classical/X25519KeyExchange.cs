using Org.BouncyCastle.Crypto.Agreement;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;

namespace ToledoMessage.Crypto.Classical;

public static class X25519KeyExchange
{
    public static (byte[] publicKey, byte[] privateKey) GenerateKeyPair()
    {
        var generator = new X25519KeyPairGenerator();
        generator.Init(new X25519KeyGenerationParameters(new SecureRandom()));

        var keyPair = generator.GenerateKeyPair();

        var publicKey = ((X25519PublicKeyParameters)keyPair.Public).GetEncoded();
        var privateKey = ((X25519PrivateKeyParameters)keyPair.Private).GetEncoded();

        return (publicKey, privateKey);
    }

    public static byte[] ComputeSharedSecret(byte[] privateKey, byte[] peerPublicKey)
    {
        var privateKeyParams = new X25519PrivateKeyParameters(privateKey, 0);
        var publicKeyParams = new X25519PublicKeyParameters(peerPublicKey, 0);

        var agreement = new X25519Agreement();
        agreement.Init(privateKeyParams);

        var sharedSecret = new byte[agreement.AgreementSize];
        agreement.CalculateAgreement(publicKeyParams, sharedSecret, 0);

        return sharedSecret;
    }
}
