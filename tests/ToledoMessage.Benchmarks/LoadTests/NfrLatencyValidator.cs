using System.Diagnostics;
using System.Text;
using ToledoMessage.Crypto.Classical;
using ToledoMessage.Crypto.Hybrid;
using ToledoMessage.Crypto.KeyManagement;
using ToledoMessage.Crypto.Protocol;

namespace ToledoMessage.Benchmarks.LoadTests;

/// <summary>
/// Validates that cryptographic operations meet the project's NFR latency targets.
/// Runs in-process — no live server required.
///
/// NFR targets:
///   - Key exchange (full X3DH hybrid handshake): &lt; 500 ms (P95 over 100 iterations)
///   - Message encrypt (AES-256-GCM ratchet step): &lt; 50 ms  (P95 over 1000 iterations)
///
/// Run: dotnet run -c Release -- load --nfr
/// </summary>
public static class NfrLatencyValidator
{
    // NFR thresholds
    private const double KeyExchangeP95LimitMs = 500.0;
    private const double MessageEncryptP95LimitMs = 50.0;

    // Sample counts
    private const int KeyExchangeIterations = 100;
    private const int MessageEncryptIterations = 1000;

    public static int Run()
    {
        Console.WriteLine("=== NFR Latency Validation ===");
        Console.WriteLine($"Key exchange target : P95 < {KeyExchangeP95LimitMs} ms ({KeyExchangeIterations} iterations)");
        Console.WriteLine($"Message encrypt target: P95 < {MessageEncryptP95LimitMs} ms ({MessageEncryptIterations} iterations)");
        Console.WriteLine();

        bool allPassed = true;
        allPassed &= ValidateKeyExchange();
        allPassed &= ValidateMessageEncrypt();

        Console.WriteLine();
        Console.WriteLine(allPassed
            ? "✓  All NFR latency targets passed."
            : "✗  One or more NFR latency targets FAILED.");

        return allPassed ? 0 : 1;
    }

    // -------------------------------------------------------------------------
    // Key Exchange (full hybrid X3DH handshake)
    // -------------------------------------------------------------------------
    private static bool ValidateKeyExchange()
    {
        Console.WriteLine("--- Hybrid X3DH Key Exchange ---");

        // Pre-build Bob's bundle once — we are only benchmarking the initiator side
        var bobIdentity = IdentityKeyGenerator.Generate();
        var signedPreKey = PreKeyGenerator.GenerateSignedPreKey(
            1, bobIdentity.ClassicalPrivateKey, bobIdentity.PostQuantumPrivateKey);
        var kyberPreKey = PreKeyGenerator.GenerateKyberPreKey(
            bobIdentity.ClassicalPrivateKey, bobIdentity.PostQuantumPrivateKey);
        var otpk = PreKeyGenerator.GenerateOneTimePreKeys(1, 1)[0];

        var bundle = new PreKeyBundle
        {
            IdentityKeyClassical = bobIdentity.ClassicalPublicKey,
            IdentityKeyPostQuantum = bobIdentity.PostQuantumPublicKey,
            SignedPreKeyPublic = signedPreKey.PublicKey,
            SignedPreKeySignature = signedPreKey.Signature,
            SignedPreKeyId = signedPreKey.KeyId,
            KyberPreKeyPublic = kyberPreKey.PublicKey,
            KyberPreKeySignature = kyberPreKey.Signature,
            OneTimePreKeyPublic = otpk.PublicKey,
            OneTimePreKeyId = otpk.KeyId
        };

        // Warm-up
        for (int i = 0; i < 3; i++)
            X3dhInitiator.Initiate(bundle);

        var samples = new double[KeyExchangeIterations];
        var sw = new Stopwatch();

        for (int i = 0; i < KeyExchangeIterations; i++)
        {
            sw.Restart();
            X3dhInitiator.Initiate(bundle);
            sw.Stop();
            samples[i] = sw.Elapsed.TotalMilliseconds;
        }

        return PrintStats("X3DH hybrid handshake", samples, KeyExchangeP95LimitMs);
    }

    // -------------------------------------------------------------------------
    // Message Encrypt (AES-256-GCM via Double Ratchet encrypt step)
    // -------------------------------------------------------------------------
    private static bool ValidateMessageEncrypt()
    {
        Console.WriteLine("--- Double Ratchet Message Encrypt (AES-256-GCM) ---");

        // Build a full ratchet session: Alice → Bob
        var bobIdentity = IdentityKeyGenerator.Generate();
        var signedPreKey = PreKeyGenerator.GenerateSignedPreKey(
            1, bobIdentity.ClassicalPrivateKey, bobIdentity.PostQuantumPrivateKey);
        var kyberPreKey = PreKeyGenerator.GenerateKyberPreKey(
            bobIdentity.ClassicalPrivateKey, bobIdentity.PostQuantumPrivateKey);
        var otpk = PreKeyGenerator.GenerateOneTimePreKeys(1, 1)[0];

        var bundle = new PreKeyBundle
        {
            IdentityKeyClassical = bobIdentity.ClassicalPublicKey,
            IdentityKeyPostQuantum = bobIdentity.PostQuantumPublicKey,
            SignedPreKeyPublic = signedPreKey.PublicKey,
            SignedPreKeySignature = signedPreKey.Signature,
            SignedPreKeyId = signedPreKey.KeyId,
            KyberPreKeyPublic = kyberPreKey.PublicKey,
            KyberPreKeySignature = kyberPreKey.Signature,
            OneTimePreKeyPublic = otpk.PublicKey,
            OneTimePreKeyId = otpk.KeyId
        };

        var initResult = X3dhInitiator.Initiate(bundle);
        var aliceRatchet = DoubleRatchet.InitializeAsInitiator(
            initResult.RootKey, signedPreKey.PublicKey);

        var plaintext = Encoding.UTF8.GetBytes("Hello, this is a benchmark message for the Double Ratchet encrypt path.");

        // Warm-up
        for (int i = 0; i < 10; i++)
            aliceRatchet.Encrypt(plaintext);

        var samples = new double[MessageEncryptIterations];
        var sw = new Stopwatch();

        for (int i = 0; i < MessageEncryptIterations; i++)
        {
            sw.Restart();
            aliceRatchet.Encrypt(plaintext);
            sw.Stop();
            samples[i] = sw.Elapsed.TotalMilliseconds;
        }

        return PrintStats("Double Ratchet Encrypt", samples, MessageEncryptP95LimitMs);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------
    private static bool PrintStats(string label, double[] samples, double limitMs)
    {
        Array.Sort(samples);
        double min    = samples[0];
        double max    = samples[^1];
        double mean   = samples.Average();
        double p50    = Percentile(samples, 50);
        double p95    = Percentile(samples, 95);
        double p99    = Percentile(samples, 99);

        bool passed = p95 < limitMs;

        Console.WriteLine($"  {label}");
        Console.WriteLine($"    min={min:F2}ms  mean={mean:F2}ms  p50={p50:F2}ms  p95={p95:F2}ms  p99={p99:F2}ms  max={max:F2}ms");
        Console.WriteLine($"    limit={limitMs}ms  →  {(passed ? "PASS ✓" : $"FAIL ✗  (p95 {p95:F2}ms > {limitMs}ms)")}");
        Console.WriteLine();

        return passed;
    }

    private static double Percentile(double[] sorted, int pct)
    {
        if (sorted.Length == 1) return sorted[0];
        double idx = (pct / 100.0) * (sorted.Length - 1);
        int lo = (int)idx;
        int hi = Math.Min(lo + 1, sorted.Length - 1);
        return sorted[lo] + (idx - lo) * (sorted[hi] - sorted[lo]);
    }
}
