using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Elders.Cronus.Transport.RabbitMQ.Benchmarks;

/// <summary>
/// Measures <see cref="PublisherChannelResolver.ResolveAsync"/> throughput under
/// varying levels of parallelism. The interesting question is whether the static
/// <c>SemaphoreSlim</c> in <see cref="ChannelResolverBase"/> becomes a contention
/// bottleneck when many publishers race for the cached channel.
///
/// Requires a RabbitMQ broker on <c>localhost:5672</c> with the default
/// <c>guest/guest</c> credentials. The simplest way to provision one is:
/// <c>docker run -d --rm -p 5672:5672 rabbitmq:3.13</c>.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 5)]
public class ChannelResolutionBenchmarks
{
    private const string Exchange = "cronus.bench.exchange";

    private RabbitMqOptions options = null!;
    private ConnectionResolver connectionResolver = null!;
    private PublisherChannelResolver resolver = null!;

    /// <summary>
    /// Provisions the resolver, declares the benchmark exchange, and warms the
    /// channel cache so steady-state benchmarks measure the cached path.
    /// </summary>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        options = new RabbitMqOptions
        {
            Server = "localhost",
            Port = 5672,
            VHost = "/",
            Username = "guest",
            Password = "guest",
            UseSsl = false,
            BoundedContext = "benchmarks",
        };

        IOptionsMonitor<RabbitMqOptions> monitor = new SingleValueOptionsMonitor<RabbitMqOptions>(options);
        RabbitMqConnectionFactory<RabbitMqOptions> factory = new RabbitMqConnectionFactory<RabbitMqOptions>(
            monitor,
            NullLogger<RabbitMqConnectionFactory<RabbitMqOptions>>.Instance);

        // Declare the benchmark exchange once via a throwaway connection so the resolver's
        // passive declare succeeds for every iteration.
        await using (IConnection setupConnection = await factory.CreateConnectionAsync())
        await using (IChannel setupChannel = await setupConnection.CreateChannelAsync())
        {
            await setupChannel.ExchangeDeclareAsync(Exchange, ExchangeType.Fanout, durable: false, autoDelete: false);
        }

        connectionResolver = new ConnectionResolver(factory);
        resolver = new PublisherChannelResolver(connectionResolver);

        // Warm the cache: subsequent ResolveAsync calls hit the lock + cached lookup path.
        _ = await resolver.ResolveAsync(Exchange, options, options.BoundedContext, CancellationToken.None);
    }

    /// <summary>
    /// Closes the underlying connection.
    /// </summary>
    [GlobalCleanup]
    public async Task CleanupAsync()
    {
        if (connectionResolver is not null)
            await connectionResolver.DisposeAsync();
    }

    /// <summary>
    /// Cached single-call path: lock acquired, cache hit, lock released.
    /// </summary>
    /// <returns>A task representing the benchmark iteration.</returns>
    [Benchmark(Baseline = true)]
    public async Task ResolveOnce()
    {
        IChannel channel = await resolver.ResolveAsync(Exchange, options, options.BoundedContext, CancellationToken.None);
        _ = channel;
    }

    /// <summary>
    /// Concurrent-call path: measures contention on the static <c>SemaphoreSlim</c>
    /// when N callers race for the same cached channel.
    /// </summary>
    /// <param name="parallelism">The number of concurrent <c>ResolveAsync</c> calls per iteration.</param>
    /// <returns>A task representing the benchmark iteration.</returns>
    [Benchmark]
    [Arguments(10)]
    [Arguments(50)]
    [Arguments(100)]
    public async Task ResolveConcurrent(int parallelism)
    {
        await Task.WhenAll(
            Enumerable.Range(0, parallelism)
                .Select(_ => resolver.ResolveAsync(Exchange, options, options.BoundedContext, CancellationToken.None)));
    }

}
