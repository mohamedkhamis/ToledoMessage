using Microsoft.AspNetCore.SignalR.Client;
using ToledoVault.Shared.Converters;
using ToledoVault.Shared.DTOs;

// ReSharper disable RemoveRedundantBraces

namespace ToledoVault.Client.Services;

/// <inheritdoc />
/// <summary>
/// Client-side SignalR hub connection wrapper.
/// Manages the real-time connection to the chat hub and exposes events
/// for incoming messages, delivery/read receipts, and typing indicators.
/// </summary>
public class SignalRService : IAsyncDisposable
{
    private HubConnection? _hubConnection;
    private string? _currentHubUrl;
    private long _registeredDeviceId;
    private Func<string?, Task>? _reconnectedHandler;

    /// <summary>Raised when a new encrypted message is received from the server.</summary>
    public event Action<MessageEnvelope>? OnMessageReceived;

    /// <summary>Raised when a sent message has been delivered to the recipient's device.</summary>
    public event Action<long>? OnMessageDelivered;

    /// <summary>Raised when a sent message has been read by the recipient.</summary>
    public event Action<long>? OnMessageRead;

    /// <summary>Raised when a user is typing in a conversation. Parameters: conversationId, displayName.</summary>
    public event Action<long, string>? OnTypingIndicator;

    /// <summary>Raised when a remote device's identity key has changed. Parameters: deviceId, displayName.</summary>
    public event Action<long, string>? OnIdentityKeyChanged;

    /// <summary>Raised when the server detects that a device's pre-key count is low. Parameters: deviceId, remainingCount.</summary>
    public event Action<long, int>? OnPreKeyCountLow;

    /// <summary>Raised when a participant is added to a group conversation. Parameters: conversationId, userId, displayName.</summary>
    public event Action<long, long, string>? OnParticipantAdded;

    /// <summary>Raised when a participant is removed from a group conversation. Parameters: conversationId, userId.</summary>
    public event Action<long, long>? OnParticipantRemoved;

    /// <summary>Raised when a user is online. Parameter: userId.</summary>
    public event Action<long>? OnUserOnline;

    /// <summary>Raised when a user goes offline. Parameters: userId, lastSeenAt.</summary>
    public event Action<long, DateTimeOffset>? OnUserOffline;

    /// <summary>Raised when a reaction is added. Parameters: messageId, userId, displayName, emoji.</summary>
    public event Action<long, long, string, string>? OnReactionAdded;

    /// <summary>Raised when a reaction is removed. Parameters: messageId, userId, emoji.</summary>
    public event Action<long, long, string>? OnReactionRemoved;

    /// <summary>Raised when a message is deleted for everyone. Parameters: messageId, conversationId.</summary>
    public event Action<long, long>? OnMessageDeleted;

    /// <summary>Raised locally when the Chat page marks messages as read. Parameters: conversationId, newUnreadCount.</summary>
    public event Action<long, int>? OnUnreadCountChanged;

    /// <summary>Raised when the SignalR connection reconnects. Used for flushing offline queue.</summary>
    public event Action? OnReconnected;

    /// <summary>
    /// Whether the hub connection is currently active.
    /// </summary>
    public bool IsConnected =>
        _hubConnection?.State == HubConnectionState.Connected;

    /// <summary>
    /// Builds and starts the SignalR hub connection with automatic reconnection.
    /// Reuses the existing connection if already connected to the same hub.
    /// </summary>
    /// <param name="hubUrl">The full URL to the chat hub (e.g., "https://host/hubs/chat").</param>
    /// <param name="accessToken">The JWT access token for authentication.</param>
    public async Task ConnectAsync(string hubUrl, string accessToken)
    {
        // Reuse existing connection if already connected to the same hub
        if (_hubConnection?.State is HubConnectionState.Connected or HubConnectionState.Connecting or HubConnectionState.Reconnecting
            && _currentHubUrl == hubUrl)
        {
            return;
        }

        if (_hubConnection is not null) await _hubConnection.DisposeAsync();
        // BUG-CR-010 FIX: Reset registered device ID on new connection
        _registeredDeviceId = 0;
        _currentHubUrl = hubUrl;

        _hubConnection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.AccessTokenProvider = () => Task.FromResult<string?>(accessToken);
            })
            .AddJsonProtocol(static options =>
            {
                options.PayloadSerializerOptions.PropertyNameCaseInsensitive = true;
                options.PayloadSerializerOptions.Converters.Add(new LongToStringConverter());
                options.PayloadSerializerOptions.Converters.Add(new LongNullableToStringConverter());
            })
            .WithAutomaticReconnect()
            .Build();

        // Increase max message size to support encrypted media payloads
        _hubConnection.ServerTimeout = TimeSpan.FromSeconds(60);

        // Register server-to-client handlers
        _hubConnection.On<MessageEnvelope>("ReceiveMessage", envelope =>
        {
            OnMessageReceived?.Invoke(envelope);
        });

        _hubConnection.On<long>("MessageDelivered", messageId =>
        {
            OnMessageDelivered?.Invoke(messageId);
        });

        // FR-014: Handle batched delivery acknowledgments
        _hubConnection.On<List<long>>("MessagesDelivered", messageIds =>
        {
            foreach (var messageId in messageIds)
                OnMessageDelivered?.Invoke(messageId);
        });

        _hubConnection.On<long>("MessageRead", messageId =>
        {
            OnMessageRead?.Invoke(messageId);
        });

        _hubConnection.On<long, string>("UserTyping", (conversationId, displayName) =>
        {
            OnTypingIndicator?.Invoke(conversationId, displayName);
        });

        _hubConnection.On<long, string>("IdentityKeyChanged", (deviceId, displayName) =>
        {
            OnIdentityKeyChanged?.Invoke(deviceId, displayName);
        });

        _hubConnection.On<long, int>("PreKeyCountLow", (deviceId, remainingCount) =>
        {
            OnPreKeyCountLow?.Invoke(deviceId, remainingCount);
        });

        _hubConnection.On<long, long, string>("ParticipantAdded", (conversationId, userId, displayName) =>
        {
            OnParticipantAdded?.Invoke(conversationId, userId, displayName);
        });

        _hubConnection.On<long, long>("ParticipantRemoved", (conversationId, userId) =>
        {
            OnParticipantRemoved?.Invoke(conversationId, userId);
        });

        _hubConnection.On<long>("UserOnline", userId =>
        {
            OnUserOnline?.Invoke(userId);
        });

        _hubConnection.On<long, DateTimeOffset>("UserOffline", (userId, lastSeenAt) =>
        {
            OnUserOffline?.Invoke(userId, lastSeenAt);
        });

        _hubConnection.On<long, long, string, string>("ReactionAdded", (messageId, userId, displayName, emoji) =>
        {
            OnReactionAdded?.Invoke(messageId, userId, displayName, emoji);
        });

        _hubConnection.On<long, long, string>("ReactionRemoved", (messageId, userId, emoji) =>
        {
            OnReactionRemoved?.Invoke(messageId, userId, emoji);
        });

        _hubConnection.On<long, long>("MessageDeleted", (messageId, conversationId) =>
        {
            OnMessageDeleted?.Invoke(messageId, conversationId);
        });

        await _hubConnection.StartAsync();
    }

    /// <summary>
    /// Registers this device with the hub so that messages can be routed to it.
    /// Also sets up automatic re-registration on reconnect.
    /// Skips if the same device is already registered on this connection.
    /// </summary>
    /// <param name="deviceId">The local device ID.</param>
    public async Task RegisterDeviceAsync(long deviceId)
    {
        if (_hubConnection is null)
            throw new InvalidOperationException("SignalR connection has not been started. Call ConnectAsync first.");

        // Skip if already registered on this connection
        if (_registeredDeviceId == deviceId)
            return;

        // Remove previous reconnect handler before adding a new one
        if (_reconnectedHandler is not null)
        {
            _hubConnection.Reconnected -= _reconnectedHandler;
        }

        _reconnectedHandler = async _ =>
        {
            try
            {
                await _hubConnection.InvokeAsync("RegisterDevice", deviceId);
                // FR-032: Notify listeners that reconnection occurred (for offline queue flush)
                OnReconnected?.Invoke();
            }
            catch
            {
                // ignored
            }
        };
        _hubConnection.Reconnected += _reconnectedHandler;

        await _hubConnection.InvokeAsync("RegisterDevice", deviceId);
        _registeredDeviceId = deviceId;
    }

    /// <summary>
    /// Sends an encrypted message via the hub and returns the server result.
    /// </summary>
    /// <param name="request">The send message request containing ciphertext and metadata.</param>
    /// <returns>The server-assigned message ID, timestamp, and sequence number.</returns>
    public async Task<SendMessageResult> SendMessageAsync(SendMessageRequest request)
    {
        if (_hubConnection is null)
            throw new InvalidOperationException("SignalR connection has not been started. Call ConnectAsync first.");

        return await _hubConnection.InvokeAsync<SendMessageResult>("SendMessage", request);
    }

    /// <summary>
    /// Acknowledges that a message has been delivered to this device.
    /// </summary>
    /// <param name="messageId">The ID of the delivered message.</param>
    public async Task AcknowledgeDeliveryAsync(long messageId)
    {
        if (_hubConnection is null)
            throw new InvalidOperationException("SignalR connection has not been started. Call ConnectAsync first.");

        await _hubConnection.InvokeAsync("AcknowledgeDelivery", messageId);
    }

    /// <summary>
    /// Sends a typing indicator to the conversation.
    /// </summary>
    /// <param name="conversationId">The conversation in which the user is typing.</param>
    public async Task SendTypingIndicatorAsync(long conversationId)
    {
        if (_hubConnection is null)
            throw new InvalidOperationException("SignalR connection has not been started. Call ConnectAsync first.");

        await _hubConnection.InvokeAsync("TypingIndicator", conversationId);
    }

    /// <summary>
    /// Adds a reaction to a message.
    /// </summary>
    public async Task AddReactionAsync(long messageId, string emoji)
    {
        if (_hubConnection is null)
            throw new InvalidOperationException("SignalR connection has not been started. Call ConnectAsync first.");

        await _hubConnection.InvokeAsync("AddReaction", messageId, emoji);
    }

    /// <summary>
    /// Removes a reaction from a message.
    /// </summary>
    public async Task RemoveReactionAsync(long messageId, string emoji)
    {
        if (_hubConnection is null)
            throw new InvalidOperationException("SignalR connection has not been started. Call ConnectAsync first.");

        await _hubConnection.InvokeAsync("RemoveReaction", messageId, emoji);
    }

    public async Task DeleteForEveryoneAsync(long messageId)
    {
        if (_hubConnection is null)
            throw new InvalidOperationException("SignalR connection has not been started. Call ConnectAsync first.");

        await _hubConnection.InvokeAsync("DeleteForEveryone", messageId);
    }

    /// <summary>
    /// Notifies subscribers (e.g. sidebar) that the unread count for a conversation changed.
    /// </summary>
    public void NotifyUnreadCountChanged(long conversationId, int newUnreadCount)
    {
        OnUnreadCountChanged?.Invoke(conversationId, newUnreadCount);
    }

    public async Task ClearMessagesAsync(long conversationId, DateTimeOffset from, DateTimeOffset to)
    {
        if (_hubConnection is null)
            throw new InvalidOperationException("SignalR connection has not been started. Call ConnectAsync first.");

        await _hubConnection.InvokeAsync("ClearMessages", conversationId, from, to);
    }

    /// <inheritdoc />
    /// <summary>
    /// Disposes the hub connection.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_hubConnection is not null)
        {
            await _hubConnection.DisposeAsync();
            _hubConnection = null;
        }

        _currentHubUrl = null;
        _registeredDeviceId = 0;
        GC.SuppressFinalize(this);
    }
}
