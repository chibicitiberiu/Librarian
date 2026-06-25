using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading.Tasks;
using Librarian.Metadata;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Librarian.Services
{
    /// <summary>
    /// Centralized resilience policy for metadata-provider calls made during indexing. It retries
    /// transient failures (network / timeout / 5xx, surfaced as <see cref="TransientMetadataException"/>
    /// and friends) with exponential backoff, and trips a per-provider circuit breaker when a provider
    /// keeps failing — so one down provider (typically Tika) neither stalls the whole index nor gets
    /// hammered file after file. A call that ultimately fails transiently (or is short-circuited by an
    /// open breaker) reports <c>Failed = true</c>, the signal for the caller to mark the file's
    /// extraction incomplete. Registered as a singleton so circuit state persists across files in a run.
    /// </summary>
    public class ProviderExecutor
    {
        private readonly ILogger<ProviderExecutor> logger;
        private readonly int maxRetries;
        private readonly TimeSpan baseDelay;
        private readonly int failureThreshold;
        private readonly TimeSpan circuitResetAfter;
        private readonly ConcurrentDictionary<Guid, Circuit> circuits = new();

        public ProviderExecutor(ILogger<ProviderExecutor> logger, IConfiguration configuration)
            : this(logger,
                   maxRetries: GetInt(configuration, "Metadata:MaxRetries", 2),
                   baseDelayMs: GetInt(configuration, "Metadata:RetryBaseDelayMs", 500),
                   failureThreshold: GetInt(configuration, "Metadata:CircuitFailureThreshold", 5),
                   circuitResetSeconds: GetInt(configuration, "Metadata:CircuitResetSeconds", 300))
        {
        }

        /// <summary>Explicit-policy constructor (used by tests, e.g. with a zero backoff).</summary>
        public ProviderExecutor(ILogger<ProviderExecutor> logger, int maxRetries, int baseDelayMs, int failureThreshold, int circuitResetSeconds)
        {
            this.logger = logger;
            this.maxRetries = Math.Max(0, maxRetries);
            this.baseDelay = TimeSpan.FromMilliseconds(Math.Max(0, baseDelayMs));
            this.failureThreshold = Math.Max(1, failureThreshold);
            this.circuitResetAfter = TimeSpan.FromSeconds(Math.Max(1, circuitResetSeconds));
        }

        private static int GetInt(IConfiguration? configuration, string key, int fallback)
            => int.TryParse(configuration?[key], out int value) ? value : fallback;

        /// <summary>Forgets all circuit state — call at the start of a full reindex so a provider that
        /// tripped earlier gets a clean chance.</summary>
        public void Reset() => circuits.Clear();

        /// <summary>
        /// Runs a provider call under the resilience policy. Returns the result (which may legitimately
        /// be null when the provider has nothing — e.g. Tika disabled, an unsupported file) and whether
        /// the call ultimately <b>failed transiently</b> (retries exhausted, or an open circuit). A
        /// non-transient error is logged but reported as not-failed, since re-indexing would not fix it.
        /// </summary>
        public async Task<(T? Result, bool Failed)> ExecuteAsync<T>(Guid providerId, string displayName, Func<Task<T?>> call) where T : class
        {
            var circuit = circuits.GetOrAdd(providerId, _ => new Circuit());

            if (circuit.IsOpen(failureThreshold, circuitResetAfter))
            {
                logger.LogWarning("Skipping provider {provider}: circuit open after repeated failures.", displayName);
                return (null, true);
            }

            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    T? result = await call();
                    circuit.RecordSuccess();
                    return (result, false);
                }
                catch (Exception ex) when (IsTransient(ex))
                {
                    if (attempt < maxRetries)
                    {
                        var delay = TimeSpan.FromMilliseconds(baseDelay.TotalMilliseconds * Math.Pow(2, attempt));
                        logger.LogWarning(ex, "Transient failure from {provider} (attempt {attempt}/{total}); retrying in {delay}ms.",
                            displayName, attempt + 1, maxRetries + 1, delay.TotalMilliseconds);
                        if (delay > TimeSpan.Zero)
                            await Task.Delay(delay);
                        continue;
                    }

                    circuit.RecordFailure();
                    logger.LogError(ex, "Provider {provider} failed after {total} attempts; marking extraction incomplete.", displayName, maxRetries + 1);
                    return (null, true);
                }
                catch (Exception ex)
                {
                    // Non-transient (e.g. a malformed file): re-indexing won't help, so don't retry,
                    // don't trip the breaker, and don't flag the file — just surface it in the log.
                    logger.LogError(ex, "Provider {provider} failed (non-transient).", displayName);
                    return (null, false);
                }
            }
        }

        private static bool IsTransient(Exception ex) =>
            ex is TransientMetadataException
            or HttpRequestException
            or TimeoutException
            or TaskCanceledException
            or IOException
            or SocketException;

        private sealed class Circuit
        {
            private int failureCount;
            private DateTimeOffset lastFailure = DateTimeOffset.MinValue;

            public bool IsOpen(int threshold, TimeSpan resetAfter)
                => failureCount >= threshold && DateTimeOffset.UtcNow - lastFailure < resetAfter;

            public void RecordSuccess() => failureCount = 0;

            public void RecordFailure()
            {
                failureCount++;
                lastFailure = DateTimeOffset.UtcNow;
            }
        }
    }
}
