using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Elders.Cronus.Transport.RabbitMQ.Benchmarks;

/// <summary>
/// Measures the cost of creating (and immediately closing) a channel on an
/// already-open connection via <see cref="IConnection.CreateChannelAsync"/>.
///
/// Requires a RabbitMQ broker on <c>localhost:5672</c> with the default
/// <c>guest/guest</c> credentials.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 5)]
public class ChannelOpenBenchmarks
{
    private RabbitMqOptions options = null!;
    private RabbitMqConnectionFactory<RabbitMqOptions> factory = null!;
    private IConnection connection = null!;

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
        factory = new RabbitMqConnectionFactory<RabbitMqOptions>(
            monitor,
            NullLogger<RabbitMqConnectionFactory<RabbitMqOptions>>.Instance);

        connection = await factory.CreateConnectionAsync();
    }

    [GlobalCleanup]
    public async Task CleanupAsync()
    {
        if (connection is not null)
            await connection.DisposeAsync();
    }

    /// <summary>
    /// Open + close one channel per iteration.
    /// </summary>
    [Benchmark]
    public async Task OpenAndClose()
    {
        await using IChannel channel = await connection.CreateChannelAsync();
    }
}
