# Quickstart: Testing Fix All Chat Functions

**Branch**: `005-fix-chat-functions`

## Prerequisites

- .NET 10 SDK installed
- SQL Server 2022 running locally
- Two browser windows/devices for sender/receiver testing

## Build & Run

```bash
cd src/ToledoVault
dotnet run
```

App runs at `https://localhost:5001` (or configured port).

## Run Tests

```bash
dotnet test ToledoVault.slnx
```

Expected: 215+ tests pass (142 server + 65 crypto + 8 integration).

## Manual Testing Checklist

### Media Sending (P1)
1. Open two browser windows, log in as two different users
2. Start a conversation between them
3. User A: Attach a JPEG image → verify preview shows → send
4. User B: Verify image renders in chat bubble (not garbled text)
5. Repeat with: PNG, video (MP4), audio recording, PDF file
6. From iPhone: Send HEIC photo → verify receiver sees it

### Clear Chat (P2)
1. Send 10+ messages in a conversation
2. Open context menu or settings → Clear chat → select "Last hour"
3. Verify only recent messages are removed
4. Verify reactions on remaining messages are still visible
5. Refresh page → verify cleared messages don't reappear
6. If server is offline during clear → verify error message shown

### Context Menu (P2)
1. Right-click a message → verify menu opens for correct message
2. Select "Delete for me" → verify correct message removed
3. Start replying to a message → delete that message → verify reply preview clears
4. Right-click media message → Forward → verify media arrives intact in target conversation
5. If forward media fails → verify user notification (not silent fallback)

### Audio Playback (P3)
1. Send voice message → receiver taps play → verify audio plays
2. Verify waveform animates and timer counts
3. Tap pause → verify playback stops
4. Tap play again → verify playback resumes

### Memory (P3)
1. Open conversation with 20+ images
2. Check browser memory (DevTools → Memory tab)
3. Verify no unbounded growth from raw byte arrays
