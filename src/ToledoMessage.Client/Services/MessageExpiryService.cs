namespace ToledoMessage.Client.Services;

/// <inheritdoc />
/// <summary>
/// Tracks messages that have a disappearing timer and fires expiry events
/// when messages should be removed from the local display.
/// </summary>
public class MessageExpiryService : IDisposable
{
    private readonly Dictionary<long, DateTimeOffset> _trackedMessages = new();
    private readonly Timer _timer;

    /// <summary>
    /// Fired when a message has expired and should be removed from display.
    /// The parameter is the expired message ID.
    /// </summary>
    public event Action<long>? OnMessageExpired;

    public MessageExpiryService()
    {
        // Check for expired messages every 30 seconds
        _timer = new Timer(_ => CheckExpiredMessages(), null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }

    /// <summary>
    /// Start tracking a message for expiry. When the expiry time arrives,
    /// <see cref="OnMessageExpired"/> will fire with the message ID.
    /// </summary>
    public void TrackMessage(long messageId, DateTimeOffset expiresAt)
    {
        _trackedMessages[messageId] = expiresAt;
    }


    /// <summary>
    /// Remove all messages that are already expired and return their IDs.
    /// Useful for cleaning up on app startup without waiting for the timer.
    /// </summary>
    public List<long> FlushExpired()
    {
        var now = DateTimeOffset.UtcNow;
        var expiredIds = _trackedMessages
            .Where(kv => now >= kv.Value)
            .Select(static kv => kv.Key)
            .ToList();

        foreach (var id in expiredIds)
            _trackedMessages.Remove(id);

        return expiredIds;
    }

    /// <summary>
    /// Check all tracked messages and fire expiry events for those past their expiration time.
    /// </summary>
    private void CheckExpiredMessages()
    {
        var now = DateTimeOffset.UtcNow;
        var expiredIds = new List<long>();

        foreach (var (messageId, expiresAt) in _trackedMessages)
            if (now >= expiresAt)
                expiredIds.Add(messageId);

        foreach (var messageId in expiredIds)
        {
            _trackedMessages.Remove(messageId);
            OnMessageExpired?.Invoke(messageId);
        }
    }

    public void Dispose()
    {
        _timer.Dispose();
        GC.SuppressFinalize(this);
    }
}
