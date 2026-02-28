using Microsoft.AspNetCore.SignalR.Client;
using ToledoMessage.Shared.DTOs;

namespace ToledoMessage.Client.Services;

/// <summary>
/// Client-side SignalR hub connection wrapper.
/// Manages the real-time connection to the chat hub and exposes events
/// for incoming messages, delivery/read receipts, and typing indicators.
/// </summary>
public class SignalRService : IAsyncDisposable
{
    private HubConnection? _hubConnection;

    /// <summary>Raised when a new encrypted message is received from the server.</summary>
    public event Action<MessageEnvelope>? OnMessageReceived;

    /// <summary>Raised when a sent message has been delivered to the recipient's device.</summary>
    public event Action<decimal>? OnMessageDelivered;

    /// <summary>Raised when a sent message has been read by the recipient.</summary>
    public event Action<decimal>? OnMessageRead;

    /// <summary>Raised when a user is typing in a conversation. Parameters: conversationId, displayName.</summary>
    public event Action<decimal, string>? OnTypingIndicator;

    /// <summary>Raised when a remote device's identity key has changed. Parameters: deviceId, displayName.</summary>
    public event Action<decimal, string>? OnIdentityKeyChanged;

    /// <summary>Raised when the server detects that a device's pre-key count is low. Parameters: deviceId, remainingCount.</summary>
    public event Action<decimal, int>? OnPreKeyCountLow;

    /// <summary>Raised when a participant is added to a group conversation. Parameters: conversationId, userId, displayName.</summary>
    public event Action<decimal, decimal, string>? OnParticipantAdded;

    /// <summary>Raised when a participant is removed from a group conversation. Parameters: conversationId, userId.</summary>
    public event Action<decimal, decimal>? OnParticipantRemoved;

    /// <summary>
    /// Whether the hub connection is currently active.
    /// </summary>
    public bool IsConnected =>
        _hubConnection?.State == HubConnectionState.Connected;

    /// <summary>
    /// Builds and starts the SignalR hub connection with automatic reconnection.
    /// </summary>
    /// <param name="hubUrl">The full URL to the chat hub (e.g., "https://host/hubs/chat").</param>
    /// <param name="accessToken">The JWT access token for authentication.</param>
    public async Task ConnectAsync(string hubUrl, string accessToken)
    {
        if (_hubConnection is not null) await _hubConnection.DisposeAsync();

        _hubConnection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.AccessTokenProvider = () => Task.FromResult<string?>(accessToken);
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

        _hubConnection.On<decimal>("MessageDelivered", messageId =>
        {
            OnMessageDelivered?.Invoke(messageId);
        });

        _hubConnection.On<decimal>("MessageRead", messageId =>
        {
            OnMessageRead?.Invoke(messageId);
        });

        _hubConnection.On<decimal, string>("UserTyping", (conversationId, displayName) =>
        {
            OnTypingIndicator?.Invoke(conversationId, displayName);
        });

        _hubConnection.On<decimal, string>("IdentityKeyChanged", (deviceId, displayName) =>
        {
            OnIdentityKeyChanged?.Invoke(deviceId, displayName);
        });

        _hubConnection.On<decimal, int>("PreKeyCountLow", (deviceId, remainingCount) =>
        {
            OnPreKeyCountLow?.Invoke(deviceId, remainingCount);
        });

        _hubConnection.On<decimal, decimal, string>("ParticipantAdded", (conversationId, userId, displayName) =>
        {
            OnParticipantAdded?.Invoke(conversationId, userId, displayName);
        });

        _hubConnection.On<decimal, decimal>("ParticipantRemoved", (conversationId, userId) =>
        {
            OnParticipantRemoved?.Invoke(conversationId, userId);
        });

        await _hubConnection.StartAsync();
    }

    /// <summary>
    /// Registers this device with the hub so that messages can be routed to it.
    /// </summary>
    /// <param name="deviceId">The local device ID.</param>
    public async Task RegisterDeviceAsync(decimal deviceId)
    {
        if (_hubConnection is null)
            throw new InvalidOperationException("SignalR connection has not been started. Call ConnectAsync first.");

        await _hubConnection.InvokeAsync("RegisterDevice", deviceId);
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
    public async Task AcknowledgeDeliveryAsync(decimal messageId)
    {
        if (_hubConnection is null)
            throw new InvalidOperationException("SignalR connection has not been started. Call ConnectAsync first.");

        await _hubConnection.InvokeAsync("AcknowledgeDelivery", messageId);
    }

    /// <summary>
    /// Acknowledges that a message has been read by the user.
    /// </summary>
    /// <param name="messageId">The ID of the read message.</param>
    public async Task AcknowledgeReadAsync(decimal messageId)
    {
        if (_hubConnection is null)
            throw new InvalidOperationException("SignalR connection has not been started. Call ConnectAsync first.");

        await _hubConnection.InvokeAsync("AcknowledgeRead", messageId);
    }

    /// <summary>
    /// Sends a typing indicator to the conversation.
    /// </summary>
    /// <param name="conversationId">The conversation in which the user is typing.</param>
    public async Task SendTypingIndicatorAsync(decimal conversationId)
    {
        if (_hubConnection is null)
            throw new InvalidOperationException("SignalR connection has not been started. Call ConnectAsync first.");

        await _hubConnection.InvokeAsync("TypingIndicator", conversationId);
    }

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
    }
}
