using BenchmarkDotNet.Running;
using ToledoVault.Benchmarks;
using ToledoVault.Benchmarks.LoadTests;

// ---------------------------------------------------------------------------
// CLI routing
//
//   (no args)                → BenchmarkDotNet crypto benchmarks (Release mode)
//   load --nfr               → NFR latency validator (in-process, no server)
//   load --signalr           → SignalR connection load test (requires live server)
//     [--url <url>]          → server base URL   (default: https://localhost:7256)
//     [--connections <n>]    → concurrent conns  (default: 10000)
//     [--duration <s>]       → test duration (s) (default: 60)
// ---------------------------------------------------------------------------

if (args.Length == 0)
{
    BenchmarkRunner.Run<CryptoBenchmarks>();
    return 0;
}

if (args[0] == "load")
{
    if (args.Contains("--nfr")) return NfrLatencyValidator.Run();

    if (args.Contains("--signalr"))
    {
        var url = GetArg(args, "--url") ?? "https://localhost:7256";
        var connections = int.TryParse(GetArg(args, "--connections"), out var c) ? c : 10_000;
        var duration = int.TryParse(GetArg(args, "--duration"), out var d) ? d : 60;

        return SignalRLoadTest.Run(url, connections, duration);
    }

    Console.Error.WriteLine("Usage: load --nfr | --signalr [--url <url>] [--connections <n>] [--duration <s>]");
    return 1;
}

Console.Error.WriteLine("Usage: (no args) for benchmarks | load --nfr | load --signalr");
return 1;

static string? GetArg(string[] args, string flag)
{
    var idx = Array.IndexOf(args, flag);
    return idx >= 0 && idx + 1 < args.Length ? args[idx + 1] : null;
}
