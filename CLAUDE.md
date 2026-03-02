# ToledoMessage Development Guidelines

Auto-generated from all feature plans. Last updated: 2026-02-25

## Active Technologies
- SQL Server 2022 (server-side via EF Core Code First) + Browser IndexedDB (client-side) (001-secure-messaging)
- C# / .NET 10 (LTS) + BouncyCastle.Cryptography 2.6.2, ASP.NET Core Identity, SignalR, EF Core 10, Serilog (001-secure-messaging)

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
- 001-secure-messaging: Added C# / .NET 10 (LTS) + BouncyCastle.Cryptography 2.6.2, ASP.NET Core Identity, SignalR, EF Core 10, Serilog
- 001-secure-messaging: Added C# / .NET 10 (LTS) + BouncyCastle.Cryptography 2.6.2, ASP.NET Core Identity, SignalR, EF Core 10, Serilog
- 001-secure-messaging: Added C# / .NET 10 (LTS) + BouncyCastle.Cryptography 2.6.2, ASP.NET Core Identity, SignalR, EF Core 10, Serilog


<!-- MANUAL ADDITIONS START -->
## Commit Workflow
- **NEVER commit any changes without user approval**
- Always ask the user to type "commit" before committing
- After completing any work, summarize changes and ask: "Ready to commit?"
- Wait for user to explicitly type "commit" before running git add/commit/push
- **Always check for BUG-REPORT-*.md files** in project root and fix all listed bugs before finishing
- After fixing bugs, mark them as done in the bug report file

## Deploy Workflow
- After finishing tasks or a group of tasks, ask user: "Deploy to IIS?"
- Run: `powershell -ExecutionPolicy Bypass -File ./deploy-iis.ps1 -Force`
- Requires Administrator privileges
- App deploys to http://localhost:8080
- Check status anytime: `powershell -ExecutionPolicy Bypass -File ./deploy-iis.ps1 -Action Status`
<!-- MANUAL ADDITIONS END -->
