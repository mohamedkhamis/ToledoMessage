using Microsoft.JSInterop;

namespace ToledoMessage.Client.Services;

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

    public async Task StoreMessagesAsync(List<StoredMessage> msgs)
    {
        await js.InvokeVoidAsync("toledoMessageStore.storeMessages", msgs);
    }

    public async Task<List<StoredMessage>> GetMessagesAsync(string conversationId, int limit = 200)
    {
        return await js.InvokeAsync<List<StoredMessage>>(
            "toledoMessageStore.getMessages", conversationId, limit, null);
    }

    public async Task UpdateMessageStatusAsync(string messageId, int status)
    {
        await js.InvokeVoidAsync("toledoMessageStore.updateMessageStatus", messageId, status);
    }

    // ReSharper disable  UnusedMember.Global
    public async Task DeleteConversationMessagesAsync(string conversationId)
    {
        await js.InvokeVoidAsync("toledoMessageStore.deleteConversationMessages", conversationId);
    }

    public async Task SetMetaAsync(string key, string value)
    {
        await js.InvokeVoidAsync("toledoMessageStore.setMeta", key, value);
    }

    public async Task<string?> GetMetaAsync(string key)
    {
        return await js.InvokeAsync<string?>("toledoMessageStore.getMeta", key);
    }

    public async Task ClearAllAsync()
    {
        await js.InvokeVoidAsync("toledoMessageStore.clearAll");
    }
}

public sealed class StoredMessage
{
    public string MessageId { get; set; } = "";
    public string ConversationId { get; set; } = "";
    public string Text { get; set; } = "";
    public string Timestamp { get; set; } = "";
    public bool IsMine { get; set; }
    public int ContentType { get; set; }
    public string? FileName { get; set; }
    public string? MediaDataBase64 { get; set; }
    public string? MimeType { get; set; }
    public int Status { get; set; }
    public string? SenderDisplayName { get; set; }
    public string? ReplyToMessageId { get; set; }
    public string? ReplyToText { get; set; }
    public string? ReplyToSenderName { get; set; }
}
