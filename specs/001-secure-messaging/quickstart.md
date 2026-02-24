# Quickstart: ToledoMessage

**Branch**: `001-secure-messaging` | **Date**: 2026-02-25

## Prerequisites

- .NET 10 SDK
- SQL Server 2022 (or SQL Server Express / LocalDB)
- Node.js (for optional front-end tooling)
- A modern web browser (Chrome, Edge, Firefox)

## Setup

### 1. Clone and checkout

```bash
git clone <repo-url>
cd ToledoMessage
git checkout 001-secure-messaging
```

### 2. Restore dependencies

```bash
dotnet restore
```

### 3. Configure database

Update the connection string in `src/ToledoMessage/appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=ToledoMessage;Trusted_Connection=True;MultipleActiveResultSets=true"
  }
}
```

### 4. Apply migrations

```bash
cd src/ToledoMessage
dotnet ef database update
```

### 5. Run the application

```bash
dotnet run --project src/ToledoMessage
```

The server starts and serves both the API and the Blazor Web App. Open `https://localhost:5001` in a browser.

### 6. Test with two users

1. Open two browser tabs (or one regular + one incognito window)
2. Register "alice" in tab 1
3. Register "bob" in tab 2
4. In tab 1, search for "bob" and start a conversation
5. Send a message — it should appear instantly in tab 2

## Running Tests

```bash
# All tests
dotnet test

# Crypto tests only
dotnet test tests/ToledoMessage.Crypto.Tests

# With coverage
dotnet test --collect:"XPlat Code Coverage"

# Performance benchmarks
dotnet run --project tests/ToledoMessage.Benchmarks -c Release
```

## Project Structure

```
ToledoMessage.sln
src/
  ToledoMessage/              # ASP.NET Core server (host + API + SignalR)
  ToledoMessage.Client/       # Blazor WASM client (crypto + UI)
  ToledoMessage.Shared/       # Shared DTOs and contracts
  ToledoMessage.Crypto/       # Cryptographic library (BouncyCastle wrappers)
tests/
  ToledoMessage.Crypto.Tests/ # Crypto unit tests
  ToledoMessage.Server.Tests/ # API and SignalR hub tests
  ToledoMessage.Client.Tests/ # Client component tests
  ToledoMessage.Integration.Tests/ # End-to-end tests
  ToledoMessage.Benchmarks/   # Performance benchmarks
```

## Key Architectural Rules

1. **ALL crypto runs client-side** — the server is an untrusted relay
2. **BouncyCastle.Cryptography** is the single crypto library (pure managed C#, works in WASM)
3. **Hybrid crypto** — every key exchange combines X25519 + ML-KEM-768
4. **Never log plaintext** — server logs contain only message IDs, timestamps, error codes
