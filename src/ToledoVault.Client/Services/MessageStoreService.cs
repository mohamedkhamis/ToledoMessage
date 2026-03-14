using Microsoft.JSInterop;

namespace ToledoVault.Client.Services;

public sealed class MessageStoreService(IJSRuntime js)
{
    public async Task InitializeAsync()
    {
        await js.InvokeVoidAsync("toledoMessageStore.open");
    }

    public async Task StoreMessageAsync(StoredMessage msg)
    {
        await js.InvokeVoidAsync("toledoMessageStore.storeMessage", msg);
    }

    public async Task<int> GetMessageCountAsync(string conversationId)
    {
        return await js.InvokeAsync<int>("toledoMessageStore.getMessageCount", conversationId);
    }

    public async Task<List<StoredMessage>> GetMessagesPagedAsync(string conversationId, int offset, int count)
    {
        return await js.InvokeAsync<List<StoredMessage>>(
            "toledoMessageStore.getMessagesPaged", conversationId, offset, count);
    }


    public async Task DeleteMessageAsync(string messageId)
    {
        await js.InvokeVoidAsync("toledoMessageStore.deleteMessage", messageId);
    }

    public async Task UpdateMessageStatusAsync(string messageId, int status)
    {
        await js.InvokeVoidAsync("toledoMessageStore.updateMessageStatus", messageId, status);
    }

    public async Task DeleteConversationMessagesAsync(string conversationId, DateTimeOffset? fromTimestamp = null)
    {
        if (fromTimestamp.HasValue)
            await js.InvokeVoidAsync("toledoMessageStore.deleteConversationMessages", conversationId, fromTimestamp.Value.ToString("O"));
        else
            await js.InvokeVoidAsync("toledoMessageStore.deleteConversationMessages", conversationId);
    }

    public async Task ClearAllAsync()
    {
        await js.InvokeVoidAsync("toledoMessageStore.clearAll");
    }
}

public sealed class StoredMessage
{
    public string MessageId { get; set; } = "";
    public long SequenceNumber { get; set; }
    public string ConversationId { get; set; } = "";
    public string Text { get; set; } = "";
    public string Timestamp { get; set; } = "";
    public bool IsMine { get; set; }
    public int ContentType { get; set; }
    public string? FileName { get; set; }
    public string? MediaDataBase64 { get; set; }
    public string? MimeType { get; set; }
    public int Status { get; set; }

    // ReSharper disable once UnusedMember.Global
    public string? SenderDisplayName { get; set; }
    public string? ReplyToMessageId { get; set; }
    public string? ReplyToText { get; set; }
    public string? ReplyToSenderName { get; set; }
}
