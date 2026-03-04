# ToledoMessage Development Guidelines

Auto-generated from all feature plans. Last updated: 2026-02-25

## Active Technologies
- SQL Server 2022 (server-side via EF Core Code First) + Browser IndexedDB (client-side) (001-secure-messaging)
- C# / .NET 10 (LTS) + BouncyCastle.Cryptography 2.6.2, ASP.NET Core Identity, SignalR, EF Core 10, Serilog (001-secure-messaging)
- C# / .NET 10 (LTS) + BouncyCastle.Cryptography 2.6.2, ASP.NET Core Identity (password hashing only), SignalR, EF Core 10, Serilog (001-secure-messaging)
- SQL Server 2022 (server-side via EF Core Code First) + Browser IndexedDB (client-side via JS interop) (001-secure-messaging)
- C# / .NET 10 (LTS) + Blazor WebAssembly, ASP.NET Core, SignalR, EF Core 10 (001-secure-messaging)
- SQL Server 2022 (server) + Browser localStorage/IndexedDB (client) (001-secure-messaging)
- C# / .NET 10, CSS3, JavaScript (Blazor WASM interop) + Blazor WebAssembly, ASP.NET Core, SignalR (003-fix-styles)
- N/A (no data model changes) (003-fix-styles)
- C# / .NET 10 (LTS), JavaScript (browser interop) + Blazor WebAssembly, ASP.NET Core, SignalR, BouncyCastle.Cryptography 2.6.2 (005-fix-chat-functions)
- SQL Server 2022 (server), Browser IndexedDB (client) (005-fix-chat-functions)
- C# / .NET 10 (LTS) + Blazor WebAssembly, ASP.NET Core, SignalR, BouncyCastle.Cryptography 2.6.2, EF Core 10 (005-fix-chat-functions)
- SQL Server 2022 (server) + Browser IndexedDB (client via JS interop) (005-fix-chat-functions)
- C# / .NET 10 (LTS), JavaScript (browser interop) + Blazor WebAssembly, ASP.NET Core, SignalR, BouncyCastle.Cryptography 2.6.2, EF Core 10 (006-fix-media-sharing)
- SQL Server 2022 (server-side encrypted ciphertext), Browser IndexedDB (client-side cached media as base64) (006-fix-media-sharing)

- C# / .NET 10 (LTS) + BouncyCastle.Cryptography 2.6.2, ASP.NET Core Identity, SignalR, EF Core 10 (001-secure-messaging)

## Project Structure

```text
backend/
frontend/
tests/
```

## Commands

# Add commands for C# / .NET 10 (LTS)

## Code Style

C# / .NET 10 (LTS): Follow standard conventions

## Recent Changes
- 006-fix-media-sharing: Added C# / .NET 10 (LTS), JavaScript (browser interop) + Blazor WebAssembly, ASP.NET Core, SignalR, BouncyCastle.Cryptography 2.6.2, EF Core 10
- 005-fix-chat-functions: Added C# / .NET 10 (LTS) + Blazor WebAssembly, ASP.NET Core, SignalR, BouncyCastle.Cryptography 2.6.2, EF Core 10
- 005-fix-chat-functions: Added C# / .NET 10 (LTS), JavaScript (browser interop) + Blazor WebAssembly, ASP.NET Core, SignalR, BouncyCastle.Cryptography 2.6.2


<!-- MANUAL ADDITIONS START -->
## Commit Workflow
- **NEVER commit any changes without user approval**
- Always ask the user to type "commit" before committing
- After completing any work, summarize changes and ask: "Ready to commit?"
- Wait for user to explicitly type "commit" before running git add/commit/push
- **Bug tracking:** All bugs go in `BUGS.md` (project root) — see its Bug Workflow section for format and rules
- Before starting work, check `BUGS.md` for open bugs related to your feature
- After code review, report any bugs found to `BUGS.md` under "Open Bugs" with full template (severity, file/line, fix steps)
- After fixing a bug, move it from "Open Bugs" to "Resolved Bugs" with fix date
- Do NOT create separate `BUG-REPORT-*.md` files — use `BUGS.md` only

## Deploy Workflow
- After finishing tasks or a group of tasks, ask user: "Deploy to IIS?"
- Run: `powershell -ExecutionPolicy Bypass -File ./deploy-iis.ps1 -Force`
- Requires Administrator privileges
- App deploys to http://localhost:8080
- Check status anytime: `powershell -ExecutionPolicy Bypass -File ./deploy-iis.ps1 -Action Status`
<!-- MANUAL ADDITIONS END -->
