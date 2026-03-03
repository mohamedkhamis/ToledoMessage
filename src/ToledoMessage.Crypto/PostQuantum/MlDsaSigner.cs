using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Security;

namespace ToledoMessage.Crypto.PostQuantum;

public static class MlDsaSigner
{
    public static (byte[] publicKey, byte[] privateKey) GenerateKeyPair()
    {
        var generator = new MLDsaKeyPairGenerator();
        generator.Init(new MLDsaKeyGenerationParameters(new SecureRandom(), MLDsaParameters.ml_dsa_65));

        var keyPair = generator.GenerateKeyPair();

        var publicKey = ((MLDsaPublicKeyParameters)keyPair.Public).GetEncoded();
        var privateKey = ((MLDsaPrivateKeyParameters)keyPair.Private).GetEncoded();

        return (publicKey, privateKey);
    }

    public static byte[] Sign(byte[] privateKey, byte[] message)
    {
        var privateKeyParams = MLDsaPrivateKeyParameters.FromEncoding(MLDsaParameters.ml_dsa_65, privateKey);

        var signer = new MLDsaSigner(MLDsaParameters.ml_dsa_65, true);
        signer.Init(true, privateKeyParams);
        signer.BlockUpdate(message, 0, message.Length);

        return signer.GenerateSignature();
    }

    public static bool Verify(byte[] publicKey, byte[] message, byte[] signature)
    {
        var publicKeyParams = MLDsaPublicKeyParameters.FromEncoding(MLDsaParameters.ml_dsa_65, publicKey);

        var signer = new MLDsaSigner(MLDsaParameters.ml_dsa_65, true);
        signer.Init(false, publicKeyParams);
        signer.BlockUpdate(message, 0, message.Length);

        return signer.VerifySignature(signature);
    }
}
