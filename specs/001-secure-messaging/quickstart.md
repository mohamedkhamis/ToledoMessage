# Quickstart: ToledoMessage

**Branch**: `001-secure-messaging` | **Date**: 2026-02-26

## Prerequisites

- .NET 10 SDK (LTS) — [download](https://dotnet.microsoft.com/download)
- SQL Server 2022 (LocalDB, Express, or full instance)
- A modern web browser (Chrome, Edge, Firefox)
- Git

## 1. Clone and Checkout

```bash
git clone <repository-url>
cd ToledoMessage
git checkout 001-secure-messaging
```

## 2. Configure Database Connection

Edit `src/ToledoMessage/appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=ToledoMessage;Trusted_Connection=True;MultipleActiveResultSets=true"
  },
  "Jwt": {
    "SecretKey": "your-development-secret-key-at-least-32-characters-long",
    "Issuer": "ToledoMessage",
    "Audience": "ToledoMessage",
    "ExpiryMinutes": 60
  }
}
```

> **Security note**: Use environment variables or user-secrets for
> production. Never commit real secrets to the repository.

## 3. Apply Database Migrations

```bash
cd src/ToledoMessage
dotnet ef database update
```

This creates the database with all tables: Users, Devices,
OneTimePreKeys, Conversations, ConversationParticipants,
EncryptedMessages.

## 4. Build and Run

```bash
# From repository root
dotnet build
dotnet run --project src/ToledoMessage
```

The server starts on `https://localhost:5001` (HTTPS) and
`http://localhost:5000` (HTTP).

Open `https://localhost:5001` in your browser to access the
Blazor WebAssembly client.

## 5. Test the Application

### Register Two Users

1. Navigate to the Register page
2. Create User A with a display name and password (min 12 chars)
3. Open a second browser (or incognito window)
4. Create User B

### Send a Message

1. As User A, go to New Conversation
2. Search for User B's display name
3. Select User B and send a message
4. Switch to User B's browser — the message appears in real-time

### Verify Security

1. Open Security Info on either user's conversation
2. Compare the displayed fingerprint (safety number) between both users
3. Mark the conversation as verified

## 6. Run Tests

```bash
# Unit tests (crypto library)
dotnet test tests/ToledoMessage.Crypto.Tests

# Integration tests (requires SQL Server)
dotnet test tests/ToledoMessage.Integration.Tests

# All tests
dotnet test

# Performance benchmarks
dotnet run --project tests/ToledoMessage.Benchmarks -c Release
```

## 7. Project Structure Overview

| Project | Purpose |
|---------|---------|
| `src/ToledoMessage` | ASP.NET Core server (API + SignalR + Blazor host) |
| `src/ToledoMessage.Client` | Blazor WebAssembly client (UI + crypto) |
| `src/ToledoMessage.Crypto` | Cryptographic library (BouncyCastle) |
| `src/ToledoMessage.Shared` | Shared DTOs, enums, constants |
| `src/Toledo.SharedKernel` | Cross-cutting utilities |

## Key URLs

| URL | Purpose |
|-----|---------|
| `/` | Home page |
| `/register` | Account registration |
| `/login` | Login |
| `/chats` | Conversation list |
| `/chat/{id}` | Active chat |
| `/new-conversation` | Start new conversation |
| `/security-info/{id}` | Fingerprint verification |
| `/settings` | User settings |
| `/hubs/chat` | SignalR hub (WebSocket) |
| `/api/*` | REST API endpoints |
