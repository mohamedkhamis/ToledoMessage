using ToledoMessage.Crypto.KeyManagement;

namespace ToledoMessage.Client.Services;

/// <summary>
/// Computes safety numbers from both users' identity keys and manages verification state.
/// Safety numbers allow users to verify that they are communicating with the intended party
/// by comparing a shared numeric fingerprint out-of-band.
/// </summary>
public class FingerprintService(LocalStorageService storage)
{
    /// <summary>
    /// Computes the safety number for verification with a remote device.
    /// Loads the local identity key and the cached remote identity key, then
    /// generates a deterministic 30-digit safety number.
    /// </summary>
    /// <param name="remoteDeviceId">The remote device's ID.</param>
    /// <returns>A 30-digit safety number formatted as 6 groups of 5 digits.</returns>
    public async Task<string> ComputeSafetyNumberAsync(long remoteDeviceId)
    {
        // 1. Load local identity key
        var localKey = await storage.GetAsync("identity.classical.public")
                       ?? throw new InvalidOperationException(
                           "Local identity key not found. Please register or log in first.");

        // 2. Load remote device's identity key (cached during session establishment)
        var remoteKey = await storage.GetAsync($"remote.identity.{remoteDeviceId}")
                        ?? throw new InvalidOperationException(
                            $"Remote identity key for device {remoteDeviceId} not found. " +
                            "A session must be established before verifying the safety number.");

        // 3. Generate the safety number using both identity keys
        return FingerprintGenerator.GenerateFingerprint(localKey, remoteKey);
    }

    /// <summary>
    /// Checks whether the user has marked a conversation as verified.
    /// </summary>
    /// <param name="conversationId">The conversation ID to check.</param>
    /// <returns>True if the conversation has been marked as verified.</returns>
    public async Task<bool> IsVerifiedAsync(long conversationId)
    {
        return await storage.ContainsKeyAsync($"verified.{conversationId}");
    }

    /// <summary>
    /// Marks a conversation as verified after the user has confirmed
    /// the safety number matches out-of-band.
    /// </summary>
    /// <param name="conversationId">The conversation ID to mark as verified.</param>
    public async Task MarkVerifiedAsync(long conversationId)
    {
        await storage.StoreAsync($"verified.{conversationId}", [1]);
    }

    /// <summary>
    /// Removes the verified status for a conversation, typically after
    /// a key change has been detected.
    /// </summary>
    /// <param name="conversationId">The conversation ID to mark as unverified.</param>
    public async Task MarkUnverifiedAsync(long conversationId)
    {
        await storage.DeleteAsync($"verified.{conversationId}");
    }
}
