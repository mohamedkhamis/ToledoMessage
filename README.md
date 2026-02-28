# ToledoMessage

A secure messaging application with **hybrid post-quantum cryptography**, protecting conversations against both current and future quantum computing threats. Built with .NET 10, Blazor WebAssembly, and the Signal Protocol enhanced with post-quantum primitives.

## Why ToledoMessage?

Traditional end-to-end encrypted messengers rely solely on classical cryptography (e.g., Curve25519). When large-scale quantum computers arrive, these algorithms will be broken — and encrypted messages harvested today could be decrypted retroactively.

ToledoMessage uses a **hybrid "belt-and-suspenders" approach**: every key exchange and signature combines a classical algorithm with a post-quantum counterpart. Both must be broken simultaneously to compromise a conversation, providing security today and into the quantum era.

## Key Features

### Cryptography

- **Hybrid Key Exchange** — X25519 (classical) + ML-KEM-768 (post-quantum NIST standard)
- **Hybrid Signatures** — Ed25519 (classical) + ML-DSA-65 (post-quantum NIST standard)
- **Signal Protocol** — X3DH key agreement + Double Ratchet for forward secrecy and post-compromise security
- **AES-256-GCM** authenticated encryption for every message
- **Zero-trust server** — all crypto operations execute client-side in WebAssembly; the server never sees plaintext

### Messaging

- Real-time delivery via SignalR with delivery and read receipts
- Typing indicators
- Offline message queue (90-day retention)
- Group messaging with Sender Keys protocol (O(1) encryption per send)
- Disappearing messages with configurable timers
- Security fingerprints for out-of-band identity verification
- Key change warnings when a contact's identity changes

### Multi-Device

- Up to 10 linked devices per account
- Independent ratchet state per device
- Multi-tab support via BroadcastChannel leader election
- Browser notifications for incoming messages

### User Experience

- WhatsApp-style two-panel layout (sidebar + chat)
- User search by display name
- Account deactivation with 7-day grace period
- Mobile-responsive design

## Tech Stack

| Layer | Technology |
|---|---|
| **Server** | ASP.NET Core (.NET 10), SignalR, EF Core 10 |
| **Client** | Blazor WebAssembly (.NET 10) |
| **Database** | SQL Server 2022 (server) + IndexedDB (client) |
| **Cryptography** | BouncyCastle.Cryptography (ML-KEM-768, ML-DSA-65, X25519, Ed25519) |
| **Auth** | JWT (15-min access tokens + refresh tokens) |
| **Logging** | Serilog (structured, no plaintext/key material) |
| **Testing** | MSTest 4.1, BenchmarkDotNet |

## Project Structure

```
src/
  ToledoMessage/              # ASP.NET Core server (API, SignalR hub, Blazor host)
  ToledoMessage.Client/       # Blazor WebAssembly client (UI + crypto operations)
  ToledoMessage.Crypto/       # Cryptographic library (Signal Protocol, hybrid primitives)
  ToledoMessage.Shared/       # Shared DTOs, enums, constants
  Toledo.SharedKernel/        # Cross-cutting utilities
tests/
  ToledoMessage.Server.Tests/       # Server unit tests
  ToledoMessage.Crypto.Tests/       # Cryptography unit tests
  ToledoMessage.Client.Tests/       # Client service tests
  ToledoMessage.Integration.Tests/  # End-to-end tests
  ToledoMessage.Benchmarks/         # Performance & load tests
```

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [SQL Server 2022](https://www.microsoft.com/en-us/sql-server/) (or LocalDB for development)

### Setup

```bash
# Clone the repository
git clone https://github.com/your-username/ToledoMessage.git
cd ToledoMessage

# Restore dependencies
dotnet restore

# Apply database migrations
dotnet ef database update --project src/ToledoMessage

# Run the application
dotnet run --project src/ToledoMessage
```

The app will be available at `https://localhost:7159` (HTTPS) or `http://localhost:5005` (HTTP).

### Configuration

Update `src/ToledoMessage/appsettings.json` with your connection string:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=.;Database=ToledoMessage;Trusted_Connection=True;TrustServerCertificate=True"
  }
}
```

> **Important:** Replace the JWT `SecretKey` in production via environment variables. Never use the default development key.

### Running Tests

```bash
# Run all tests
dotnet test

# Run specific test project
dotnet test tests/ToledoMessage.Crypto.Tests
dotnet test tests/ToledoMessage.Server.Tests
dotnet test tests/ToledoMessage.Integration.Tests
```

## Architecture

```
┌─────────────────────────────────────────────────────────┐
│                   Blazor WebAssembly Client              │
│  ┌──────────┐  ┌──────────────┐  ┌───────────────────┐  │
│  │    UI    │  │ Crypto Layer │  │ IndexedDB Storage │  │
│  │ (Razor) │  │ (BouncyCastle)│  │  (Private Keys)   │  │
│  └────┬─────┘  └──────┬───────┘  └───────────────────┘  │
│       │               │                                   │
│       └───────┬───────┘                                   │
│               │ Encrypted messages only                   │
└───────────────┼───────────────────────────────────────────┘
                │ SignalR + REST API
┌───────────────┼───────────────────────────────────────────┐
│               │         ASP.NET Core Server               │
│  ┌────────────▼──────────┐  ┌──────────────────────────┐  │
│  │   SignalR Hub         │  │   REST Controllers       │  │
│  │  (Real-time relay)    │  │  (Auth, Keys, Messages)  │  │
│  └────────────┬──────────┘  └────────────┬─────────────┘  │
│               │                          │                 │
│               └──────────┬───────────────┘                 │
│                          │                                 │
│               ┌──────────▼───────────┐                     │
│               │   SQL Server 2022    │                     │
│               │  (Encrypted blobs,   │                     │
│               │   public keys only)  │                     │
│               └──────────────────────┘                     │
└────────────────────────────────────────────────────────────┘
```

The server is a **zero-knowledge relay** — it stores only encrypted ciphertext and public keys. Private keys and plaintext messages never leave the client.

## Performance Targets

| Metric | Target |
|---|---|
| Key exchange latency | < 500ms |
| Message encrypt/decrypt | < 50ms |
| Hybrid overhead per message | < 1 KB vs classical-only |
| Message delivery (online) | < 2 seconds |
| Concurrent users | 10,000+ |
| Max message size | 64 KB plaintext |

## Roadmap

### In Progress

- [x] Hybrid post-quantum key exchange (X25519 + ML-KEM-768)
- [x] Hybrid signatures (Ed25519 + ML-DSA-65)
- [x] X3DH + Double Ratchet protocol
- [x] Real-time 1:1 messaging with delivery/read receipts
- [x] Group messaging with Sender Keys
- [x] Multi-device support
- [x] Disappearing messages
- [x] WhatsApp-style UI layout

### Planned

- [ ] Media attachments (images, audio, files) — encrypted as blobs with per-message keys
- [ ] Voice and video calls with end-to-end encryption
- [ ] Push notifications via service workers (offline browser support)
- [ ] Message search across conversations (client-side decrypted index)
- [ ] Message reactions and replies
- [ ] Contact blocking and reporting
- [ ] Profile pictures and status messages
- [ ] Message forwarding
- [ ] Desktop application (Electron / MAUI)
- [ ] Mobile application (MAUI / React Native)
- [ ] Federation support for cross-server messaging
- [ ] Formal security audit

## Security

ToledoMessage takes a defense-in-depth approach:

- **Hybrid cryptography**: Classical + post-quantum algorithms combined — both must be broken to compromise security
- **Forward secrecy**: Compromised session keys cannot decrypt past messages
- **Post-compromise security**: New keys protect future messages after a compromise
- **No key escrow**: The server cannot recover user keys; losing all devices requires creating a new identity
- **Established libraries only**: BouncyCastle — no custom cryptographic primitives
- **Rate limiting**: Server-enforced limits on registration, messaging, and search

### Responsible Disclosure

If you discover a security vulnerability, please report it responsibly by opening a private security advisory on GitHub rather than a public issue.

## Contributing

Contributions are welcome! Please:

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/your-feature`)
3. Commit your changes
4. Push to the branch (`git push origin feature/your-feature`)
5. Open a Pull Request

## License

This project is licensed under the MIT License — see the [LICENSE](LICENSE) file for details.
