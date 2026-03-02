namespace ToledoMessage.Client.Services;

public sealed record ThemeLabelSet
{
    // -- Sidebar --
    public string SearchPlaceholder { get; init; } = "Search conversations...";
    public string NoConversations { get; init; } = "No conversations yet";
    public string StartConversation { get; init; } = "Start a conversation";
    public string NewChat { get; init; } = "New conversation";
    public string SettingsLabel { get; init; } = "Settings";
    public string LogoutLabel { get; init; } = "Logout";

    // -- Chat header --
    public string Online { get; init; } = "online";
    public string LastSeen { get; init; } = "last seen {0}";
    public string TypingFormat { get; init; } = "{0} is typing...";
    public string MembersFormat { get; init; } = "{0} members";
    public string SearchMessages { get; init; } = "Search messages...";

    // -- Chat body --
    public string NoMessages { get; init; } = "No messages yet";
    public string SendFirstMessage { get; init; } = "Send the first message to start the conversation.";
    public string NewMessagesFormat { get; init; } = "{0} new message(s)";

    // -- Context menu --
    public string Copy { get; init; } = "Copy";
    public string Reply { get; init; } = "Reply";
    public string Forward { get; init; } = "Forward";
    public string DeleteForMe { get; init; } = "Delete for me";
    public string DeleteForEveryone { get; init; } = "Delete for everyone";
    public string ForwardMessage { get; init; } = "Forward message";

    // -- Clear chat dialog --
    public string ClearChat { get; init; } = "Clear chat";
    public string ClearChatHeader { get; init; } = "Clear chat messages";
    public string ChooseMessages { get; init; } = "Choose which messages to delete:";
    public string LastHour { get; init; } = "Last hour";
    public string Last24Hours { get; init; } = "Last 24 hours";
    public string Last7Days { get; init; } = "Last 7 days";
    public string Last30Days { get; init; } = "Last 30 days";
    public string AllMessages { get; init; } = "All messages";
    public string Cancel { get; init; } = "Cancel";

    // -- Message input --
    public string TypeMessage { get; init; } = "Type a message...";
    public string PhotoOrVideo { get; init; } = "Photo or Video";
    public string Audio { get; init; } = "Audio";
    public string Document { get; init; } = "Document";

    // -- Message bubble --
    public string Forwarded { get; init; } = "Forwarded";
    public string ImageUnavailable { get; init; } = "Image unavailable";
    public string VideoUnavailable { get; init; } = "Video unavailable";
    public string AudioUnavailable { get; init; } = "Audio unavailable";
    public string FileUnavailable { get; init; } = "File unavailable";
    public string Download { get; init; } = "Download";

    // -- Settings page --
    public string Appearance { get; init; } = "Appearance";
    public string FontSize { get; init; } = "Font Size";
    public string Privacy { get; init; } = "Privacy";
    public string ReadReceipts { get; init; } = "Read Receipts";
    public string TypingIndicators { get; init; } = "Typing Indicators";
    public string Security { get; init; } = "Security";
    public string Notifications { get; init; } = "Notifications";
    public string LinkedDevices { get; init; } = "Linked Devices";
    public string AccountDeletion { get; init; } = "Account Deletion";
    public string ShareKeysAcrossDevices { get; init; } = "Share Keys Across Devices";
    public string ShareKeysDescription { get; init; } = "Back up your identity keys (encrypted with your password) so new devices can decrypt previous conversations";
    public string DesktopNotifications { get; init; } = "Desktop Notifications";
    public string DesktopNotificationsDescription { get; init; } = "Receive notifications when a new message arrives";
    public string LoadingDevices { get; init; } = "Loading devices...";
    public string NoDevicesFound { get; init; } = "No active devices found.";

    // -- Delivery style --
    public DeliveryDisplayStyle DeliveryStyle { get; init; } = DeliveryDisplayStyle.Default;

    // -- Factory --
    public static ThemeLabelSet Default { get; } = new();

    public static ThemeLabelSet WhatsApp { get; } = new()
    {
        SearchPlaceholder = "Search or start new chat",
        NoConversations = "No chats yet",
        StartConversation = "Start a new chat",
        NewChat = "New chat",
        Online = "online",
        LastSeen = "last seen {0}",
        TypingFormat = "typing...",
        MembersFormat = "{0} participants",
        SearchMessages = "Search...",
        NoMessages = "No messages here yet",
        SendFirstMessage = "Send a message or tap the greeting below.",
        TypeMessage = "Type a message",
        ClearChat = "Clear chat",
        DeleteForMe = "Delete for me",
        DeleteForEveryone = "Delete for everyone",
        Forwarded = "Forwarded",
        DeliveryStyle = DeliveryDisplayStyle.DoubleTick
    };

    public static ThemeLabelSet Telegram { get; } = new()
    {
        SearchPlaceholder = "Search",
        NoConversations = "No chats",
        StartConversation = "Start messaging",
        NewChat = "New Message",
        Online = "online",
        LastSeen = "last seen {0}",
        TypingFormat = "typing...",
        MembersFormat = "{0} members",
        SearchMessages = "Search...",
        NoMessages = "No messages here yet",
        SendFirstMessage = "Send a message",
        TypeMessage = "Write a message...",
        ClearChat = "Clear History",
        DeleteForMe = "Delete for me",
        DeleteForEveryone = "Delete for everyone",
        Forwarded = "Forwarded message",
        DeliveryStyle = DeliveryDisplayStyle.SingleTick
    };

    public static ThemeLabelSet Signal { get; } = new()
    {
        SearchPlaceholder = "Search...",
        NoConversations = "No conversations",
        StartConversation = "Compose a new message",
        NewChat = "New message",
        Online = "Active now",
        LastSeen = "Active {0}",
        TypingFormat = "{0} is typing",
        MembersFormat = "{0} members",
        SearchMessages = "Search",
        NoMessages = "No messages",
        SendFirstMessage = "Send a message to get started",
        TypeMessage = "Message",
        ClearChat = "Delete conversation",
        DeleteForMe = "Delete for me",
        DeleteForEveryone = "Delete for everyone",
        Forwarded = "Forwarded",
        DeliveryStyle = DeliveryDisplayStyle.Minimal
    };
}

public enum DeliveryDisplayStyle
{
    Default,
    DoubleTick,
    SingleTick,
    Minimal
}
