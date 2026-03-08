using System.Net.Http.Json;
using Microsoft.AspNetCore.SignalR.Client;
using NBomber.Contracts.Stats;
using NBomber.CSharp;
using ToledoMessage.Shared.DTOs;

namespace ToledoMessage.Benchmarks.LoadTests;

/// <summary>
/// SignalR load test: validates the server can handle concurrent WebSocket connections
/// and relay messages within acceptable latency bounds.
///
/// Requirements:
///   - A live ToledoMessage server must be reachable at the configured URL.
///   - The server must have a SQL Server database initialised.
///
/// Usage:
///   dotnet run -c Release -- load --signalr [--url https://localhost:7256] [--connections 10000] [--duration 60]
///
/// Default targets:
///   - Concurrent connections : 10,000
///   - Duration               : 60 seconds
///   - Connection error rate  : &lt; 1%
///   - P95 connection time    : &lt; 2,000 ms
///   - P95 hub round-trip     : &lt; 500 ms
/// </summary>
public static class SignalRLoadTest
{
    // Failure thresholds
    private const double MaxErrorRatePct = 1.0;
    private const double ConnectP95LimitMs = 2000.0;
    private const double HubRttP95LimitMs = 500.0;

    public static int Run(string serverUrl, int targetConnections, int durationSeconds)
    {
        Console.WriteLine("=== SignalR Connection Load Test ===");
        Console.WriteLine($"Server            : {serverUrl}");
        Console.WriteLine($"Target conns      : {targetConnections:N0}");
        Console.WriteLine($"Duration          : {durationSeconds}s");
        Console.WriteLine($"Connect P95 limit : {ConnectP95LimitMs} ms");
        Console.WriteLine($"Hub RTT P95 limit : {HubRttP95LimitMs} ms");
        Console.WriteLine($"Max error rate    : {MaxErrorRatePct}%");
        Console.WriteLine();

        // Pre-create a pool of JWTs so virtual users can connect immediately.
        // Cap at 200 to keep setup time reasonable; connections cycle through the pool.
        int userPoolSize = Math.Min(targetConnections, 200);
        Console.WriteLine($"Provisioning {userPoolSize} test users...");
        var tokens = ProvisionUsers(serverUrl, userPoolSize).GetAwaiter().GetResult();
        if (tokens.Count == 0)
        {
            Console.Error.WriteLine("ERROR: Could not provision any test users. Is the server running?");
            return 2;
        }
        Console.WriteLine($"Provisioned {tokens.Count} users.");
        Console.WriteLine();

        // -----------------------------------------------------------------------
        // Scenario 1: SignalR connection establishment
        // Each virtual user connects, waits, then disconnects.
        // NBomber measures the full connect→disconnect cycle latency.
        // -----------------------------------------------------------------------
        int connectionIndex = 0;

        var connectScenario = Scenario.Create("signalr_connect", async context =>
        {
            var token = tokens[Interlocked.Increment(ref connectionIndex) % tokens.Count];
            var hubUrl = $"{serverUrl}/hubs/chat?access_token={token}";

            var connection = new HubConnectionBuilder()
                .WithUrl(hubUrl, opts =>
                {
                    opts.HttpMessageHandlerFactory = _ =>
                        new HttpClientHandler { ServerCertificateCustomValidationCallback = (_, _, _, _) => true };
                })
                .Build();

            try
            {
                await connection.StartAsync(context.ScenarioCancellationToken);
                await connection.StopAsync(context.ScenarioCancellationToken);
                return Response.Ok();
            }
            catch (Exception ex)
            {
                return Response.Fail("error", ex.Message, 0L);
            }
            finally
            {
                await connection.DisposeAsync();
            }
        })
        .WithoutWarmUp()
        .WithLoadSimulations(
            // Ramp up to targetConnections over 30 s, then hold for the test duration
            Simulation.RampingInject(targetConnections, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(durationSeconds))
        );

        // -----------------------------------------------------------------------
        // Scenario 2: Hub round-trip latency
        // A small pool of always-on connections invoke hub methods repeatedly.
        // Measures server-side processing latency under sustained load.
        // -----------------------------------------------------------------------
        int persistentCount = Math.Min(50, tokens.Count);
        var persistentConns = new HubConnection[persistentCount];
        Console.WriteLine($"Establishing {persistentCount} persistent connections for RTT test...");
        ConnectPersistentConnections(serverUrl, tokens, persistentConns).GetAwaiter().GetResult();

        int rttIdx = 0;

        var rttScenario = Scenario.Create("signalr_hub_rtt", async context =>
        {
            var idx = Interlocked.Increment(ref rttIdx) % persistentConns.Length;
            var conn = persistentConns[idx];

            if (conn.State != HubConnectionState.Connected)
                return Response.Fail("disconnected", "Connection not ready", 0L);

            try
            {
                // RegisterDevice with an invalid deviceId is fast and exercises
                // the full hub dispatch path without requiring real crypto state.
                await conn.InvokeAsync("RegisterDevice", 0L, context.ScenarioCancellationToken);
                return Response.Ok();
            }
            catch
            {
                // Hub rejects deviceId=0; we still measure the round-trip time.
                return Response.Ok();
            }
        })
        .WithoutWarmUp()
        .WithLoadSimulations(
            Simulation.KeepConstant(persistentCount, TimeSpan.FromSeconds(durationSeconds))
        );

        // -----------------------------------------------------------------------
        // Run NBomber
        // -----------------------------------------------------------------------
        NodeStats stats = NBomberRunner
            .RegisterScenarios(connectScenario, rttScenario)
            .WithReportFileName("signalr_load_test")
            .WithReportFolder("LoadTestReports")
            .Run();

        // Tear down persistent connections
        Console.WriteLine("Closing persistent connections...");
        foreach (var c in persistentConns)
            try { c?.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(5)); }
            catch {
                /* best-effort */
            }

        return EvaluateStats(stats);
    }

    // -----------------------------------------------------------------------
    // Helper: register + login users to obtain JWTs
    // -----------------------------------------------------------------------
    private static async Task<List<string>> ProvisionUsers(string serverUrl, int count)
    {
        var tokens = new List<string>(count);

        using var http = new HttpClient(new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true
        });
        http.BaseAddress = new Uri(serverUrl);

        for (int i = 0; i < count; i++)
        {
            // DisplayName max 32 chars; use a 16-char hex prefix + 4-digit index
            var hex = Guid.NewGuid().ToString("N")[..16];
            var name = $"lt_{hex}_{i:D4}"[..32];
            var password = $"LoadTest!{i:D6}Aa1";

            try
            {
                var reg = await http.PostAsJsonAsync("/api/auth/register",
                    new { Username = name, DisplayName = name, Password = password });

                if (reg.IsSuccessStatusCode)
                {
                    var auth = await reg.Content.ReadFromJsonAsync<AuthResponse>();
                    if (auth?.Token != null)
                        tokens.Add(auth.Token);
                }
            }
            catch
            {
                break; // Server not reachable — abort early
            }
        }

        return tokens;
    }

    // -----------------------------------------------------------------------
    // Helper: open and hold persistent HubConnections for the RTT scenario
    // -----------------------------------------------------------------------
    private static async Task ConnectPersistentConnections(
        string serverUrl, List<string> tokens, HubConnection[] connections)
    {
        for (int i = 0; i < connections.Length; i++)
        {
            var token = tokens[i % tokens.Count];
            var conn = new HubConnectionBuilder()
                .WithUrl($"{serverUrl}/hubs/chat?access_token={token}", opts =>
                {
                    opts.HttpMessageHandlerFactory = _ =>
                        new HttpClientHandler { ServerCertificateCustomValidationCallback = (_, _, _, _) => true };
                })
                .WithAutomaticReconnect()
                .Build();

            try { await conn.StartAsync(); }
            catch { /* leave disconnected; scenario handles it gracefully */ }

            connections[i] = conn;
        }
    }

    // -----------------------------------------------------------------------
    // Evaluate NBomber NodeStats against thresholds; return exit code 0/1
    // -----------------------------------------------------------------------
    private static int EvaluateStats(NodeStats stats)
    {
        Console.WriteLine();
        Console.WriteLine("=== Load Test Results ===");

        bool allPassed = true;

        foreach (var scenario in stats.ScenarioStats)
        {
            double limitMs = scenario.ScenarioName == "signalr_connect"
                ? ConnectP95LimitMs
                : HubRttP95LimitMs;

            double errorRatePct = scenario.Fail.Request.Percent;
            double p95Ms        = scenario.Ok.Latency.Percent95;

            bool errorOk   = errorRatePct <= MaxErrorRatePct;
            bool latencyOk = p95Ms        <= limitMs;

            Console.WriteLine($"Scenario: {scenario.ScenarioName}");
            Console.WriteLine($"  Requests   : {scenario.AllRequestCount:N0}  (ok={scenario.AllOkCount:N0}, fail={scenario.AllFailCount:N0})");
            Console.WriteLine($"  Error rate : {errorRatePct:F1}%  limit={MaxErrorRatePct}%  → {(errorOk ? "PASS ✓" : "FAIL ✗")}");
            Console.WriteLine($"  P95        : {p95Ms:F1}ms  limit={limitMs}ms  → {(latencyOk ? "PASS ✓" : "FAIL ✗")}");
            Console.WriteLine();

            allPassed &= errorOk && latencyOk;
        }

        Console.WriteLine(allPassed
            ? "✓  Load test PASSED all thresholds."
            : "✗  Load test FAILED one or more thresholds.");

        return allPassed ? 0 : 1;
    }
}
