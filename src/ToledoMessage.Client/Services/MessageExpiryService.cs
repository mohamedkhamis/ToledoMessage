namespace ToledoMessage.Client.Services;

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
    /// Begin tracking a message for expiry. If timerSeconds is null, the message is not tracked.
    /// </summary>
    public void TrackMessage(long messageId, DateTimeOffset receivedAt, int? timerSeconds)
    {
        if (!timerSeconds.HasValue || timerSeconds.Value <= 0)
            return;

        var expiresAt = receivedAt + TimeSpan.FromSeconds(timerSeconds.Value);
        _trackedMessages[messageId] = expiresAt;
    }

    /// <summary>
    /// Stop tracking a message (e.g., if it was manually deleted).
    /// </summary>
    public void UntrackMessage(long messageId)
    {
        _trackedMessages.Remove(messageId);
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
