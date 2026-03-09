using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;

namespace ToledoMessage.Crypto.Hybrid;

public static class HybridKeyDerivation
{
    public static byte[] DeriveKey(byte[] inputKeyMaterial, byte[] salt, byte[] info, int outputLength)
    {
        var hkdf = new HkdfBytesGenerator(new Sha256Digest());
        hkdf.Init(new HkdfParameters(inputKeyMaterial, salt, info));

        var output = new byte[outputLength];
        hkdf.GenerateBytes(output, 0, outputLength);

        return output;
    }

    public static byte[] DeriveKey(byte[] inputKeyMaterial, byte[] info, int outputLength)
    {
        return DeriveKey(inputKeyMaterial, [], info, outputLength);
    }
}
