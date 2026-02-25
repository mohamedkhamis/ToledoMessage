using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Security;

namespace ToledoMessage.Crypto.Classical;

public static class Ed25519Signer
{
    public static (byte[] publicKey, byte[] privateKey) GenerateKeyPair()
    {
        var generator = new Ed25519KeyPairGenerator();
        generator.Init(new Ed25519KeyGenerationParameters(new SecureRandom()));

        var keyPair = generator.GenerateKeyPair();

        var publicKey = ((Ed25519PublicKeyParameters)keyPair.Public).GetEncoded();
        var privateKey = ((Ed25519PrivateKeyParameters)keyPair.Private).GetEncoded();

        return (publicKey, privateKey);
    }

    public static byte[] Sign(byte[] privateKey, byte[] message)
    {
        var privateKeyParams = new Ed25519PrivateKeyParameters(privateKey, 0);

        var signer = new Org.BouncyCastle.Crypto.Signers.Ed25519Signer();
        signer.Init(true, privateKeyParams);
        signer.BlockUpdate(message, 0, message.Length);

        return signer.GenerateSignature();
    }

    public static bool Verify(byte[] publicKey, byte[] message, byte[] signature)
    {
        var publicKeyParams = new Ed25519PublicKeyParameters(publicKey, 0);

        var verifier = new Org.BouncyCastle.Crypto.Signers.Ed25519Signer();
        verifier.Init(false, publicKeyParams);
        verifier.BlockUpdate(message, 0, message.Length);

        return verifier.VerifySignature(signature);
    }
}
