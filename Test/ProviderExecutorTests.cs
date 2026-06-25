using System;
using System.Threading.Tasks;
using Librarian.Metadata;
using Librarian.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Test
{
    public class ProviderExecutorTests
    {
        private static readonly Guid Provider = Guid.Parse("33333333-3333-3333-3333-333333333333");

        private static ProviderExecutor NewExecutor(int maxRetries = 2, int threshold = 3)
            => new(NullLogger<ProviderExecutor>.Instance, maxRetries, baseDelayMs: 0, failureThreshold: threshold, circuitResetSeconds: 300);

        [Fact]
        public async Task Success_returns_result_without_failure()
        {
            int calls = 0;
            var (result, failed) = await NewExecutor().ExecuteAsync<string>(Provider, "P", () =>
            {
                calls++;
                return Task.FromResult<string?>("ok");
            });

            Assert.Equal("ok", result);
            Assert.False(failed);
            Assert.Equal(1, calls);
        }

        [Fact]
        public async Task Null_result_is_not_a_failure()
        {
            var (result, failed) = await NewExecutor().ExecuteAsync<string>(Provider, "P", () => Task.FromResult<string?>(null));

            Assert.Null(result);
            Assert.False(failed);
        }

        [Fact]
        public async Task Transient_then_success_is_retried()
        {
            int calls = 0;
            var (result, failed) = await NewExecutor().ExecuteAsync<string>(Provider, "P", () =>
            {
                calls++;
                if (calls == 1)
                    throw new TransientMetadataException("blip");
                return Task.FromResult<string?>("ok");
            });

            Assert.Equal("ok", result);
            Assert.False(failed);
            Assert.Equal(2, calls);
        }

        [Fact]
        public async Task Persistent_transient_exhausts_retries_and_flags_incomplete()
        {
            int calls = 0;
            var (result, failed) = await NewExecutor(maxRetries: 2).ExecuteAsync<string>(Provider, "P", () =>
            {
                calls++;
                throw new TransientMetadataException("down");
            });

            Assert.Null(result);
            Assert.True(failed);
            Assert.Equal(3, calls); // initial attempt + 2 retries
        }

        [Fact]
        public async Task Non_transient_is_not_retried_and_not_flagged()
        {
            int calls = 0;
            var (result, failed) = await NewExecutor().ExecuteAsync<string>(Provider, "P", () =>
            {
                calls++;
                throw new InvalidOperationException("malformed file");
            });

            Assert.Null(result);
            Assert.False(failed); // re-indexing wouldn't help, so the file is not flagged
            Assert.Equal(1, calls);
        }

        [Fact]
        public async Task Circuit_opens_after_threshold_then_short_circuits()
        {
            var executor = NewExecutor(maxRetries: 0, threshold: 3);
            Func<Task<string?>> alwaysDown = () => throw new TransientMetadataException("down");

            for (int i = 0; i < 3; i++)
                await executor.ExecuteAsync(Provider, "P", alwaysDown);

            int calls = 0;
            var (_, failed) = await executor.ExecuteAsync<string>(Provider, "P", () =>
            {
                calls++;
                return Task.FromResult<string?>("ok");
            });

            Assert.True(failed);
            Assert.Equal(0, calls); // short-circuited; the provider is not invoked

            // A different provider is unaffected by P's open circuit.
            var (result, failed2) = await executor.ExecuteAsync<string>(Guid.NewGuid(), "Q", () => Task.FromResult<string?>("ok"));
            Assert.Equal("ok", result);
            Assert.False(failed2);
        }

        [Fact]
        public async Task Reset_clears_open_circuit()
        {
            var executor = NewExecutor(maxRetries: 0, threshold: 2);
            Func<Task<string?>> down = () => throw new TransientMetadataException("down");
            for (int i = 0; i < 2; i++)
                await executor.ExecuteAsync(Provider, "P", down);

            executor.Reset();

            int calls = 0;
            var (result, failed) = await executor.ExecuteAsync<string>(Provider, "P", () =>
            {
                calls++;
                return Task.FromResult<string?>("ok");
            });

            Assert.Equal("ok", result);
            Assert.False(failed);
            Assert.Equal(1, calls);
        }
    }
}
