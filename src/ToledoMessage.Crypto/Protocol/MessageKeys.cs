using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Macs;
using Org.BouncyCastle.Crypto.Parameters;

namespace ToledoMessage.Crypto.Protocol;

/// <summary>
/// Derives per-message keys from chain keys using HMAC-SHA256.
/// Follows the Signal Protocol chain key ratchet:
///   messageKey = HMAC-SHA256(chainKey, 0x01)
///   nextChainKey = HMAC-SHA256(chainKey, 0x02)
/// </summary>
public static class MessageKeys
{
    private static readonly byte[] MessageKeySeed = [0x01];
    private static readonly byte[] ChainKeySeed = [0x02];

    /// <summary>
    /// Derives a message key from the current chain key.
    /// </summary>
    public static byte[] DeriveMessageKey(byte[] chainKey)
    {
        return HmacSha256(chainKey, MessageKeySeed);
    }

    /// <summary>
    /// Advances the chain key to the next chain key.
    /// </summary>
    public static byte[] AdvanceChainKey(byte[] chainKey)
    {
        return HmacSha256(chainKey, ChainKeySeed);
    }

    /// <summary>
    /// Derives both a message key and the next chain key from the current chain key.
    /// </summary>
    public static (byte[] messageKey, byte[] nextChainKey) DeriveKeys(byte[] chainKey)
    {
        return (DeriveMessageKey(chainKey), AdvanceChainKey(chainKey));
    }

    private static byte[] HmacSha256(byte[] key, byte[] data)
    {
        var hmac = new HMac(new Sha256Digest());
        hmac.Init(new KeyParameter(key));
        hmac.BlockUpdate(data, 0, data.Length);
        var output = new byte[hmac.GetMacSize()];
        hmac.DoFinal(output, 0);
        return output;
    }
}
