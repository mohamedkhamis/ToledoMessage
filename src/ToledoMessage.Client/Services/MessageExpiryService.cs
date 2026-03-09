namespace ToledoMessage.Client.Services;

/// <inheritdoc />
/// <summary>
/// Tracks messages that have a disappearing timer and fires expiry events
/// when messages should be removed from the local display.
/// </summary>
public class MessageExpiryService : IDisposable
{
    // ReSharper disable once CollectionNeverUpdated.Local
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
