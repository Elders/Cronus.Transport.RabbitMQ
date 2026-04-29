using System;
using System.Linq;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Elders.Cronus.Transport.RabbitMQ.Benchmarks;

/// <summary>
/// Measures publish throughput on a v7 (async) <see cref="IChannel"/>. The
/// channel + connection are created once per process and reused across all
/// iterations because the v7 client is documented as thread-safe for publish.
///
/// A fanout exchange is declared in <see cref="SetupAsync"/> with no bindings
/// — messages are accepted by the broker and silently dropped, isolating the
/// publish path from broker fan-out cost.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 5)]
public class PublishBenchmarks
{
    private const string Exchange = "cronus.bench.publish";
    private const string RoutingKey = "bench";
    private static readonly byte[] Payload = new byte[256];

    private RabbitMqConnectionFactory<RabbitMqOptions> factory = null!;
    private IConnection connection = null!;
    private IChannel channel = null!;

    /// <summary>
    /// When <c>true</c>, the channel is created with publisher confirmations enabled
    /// (matches production wiring at <see cref="ChannelResolverBase"/>:57 and
    /// <see cref="PublisherChannelResolver"/>:77). Each publish then awaits a broker ack
    /// before <c>BasicPublishAsync</c> completes — durability vs latency trade-off.
    /// When <c>false</c>, the channel uses default options — fire-and-forget publish.
    /// </summary>
    [Params(false, true)]
    public bool ConfirmsEnabled { get; set; }

    [GlobalSetup]
    public async Task SetupAsync()
    {
        new Random(42).NextBytes(Payload);

        RabbitMqOptions options = new RabbitMqOptions
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
        channel = ConfirmsEnabled
            ? await connection.CreateChannelAsync(new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true))
            : await connection.CreateChannelAsync();
        await channel.ExchangeDeclareAsync(Exchange, ExchangeType.Fanout, durable: false, autoDelete: false);
    }

    [GlobalCleanup]
    public async Task CleanupAsync()
    {
        if (channel is not null)
            await channel.DisposeAsync();
        if (connection is not null)
            await connection.DisposeAsync();
    }

    /// <summary>
    /// Single sequential publish.
    /// </summary>
    [Benchmark]
    public async Task Single()
    {
        await channel.BasicPublishAsync(Exchange, RoutingKey, body: Payload);
    }

    /// <summary>
    /// N parallel publishes awaited together via <see cref="Task.WhenAll(Task[])"/>.
    /// </summary>
    [Benchmark]
    [Arguments(10)]
    [Arguments(50)]
    [Arguments(100)]
    public async Task Concurrent(int parallelism)
    {
        Task[] tasks = new Task[parallelism];
        for (int i = 0; i < parallelism; i++)
        {
            tasks[i] = channel.BasicPublishAsync(Exchange, RoutingKey, body: Payload).AsTask();
        }
        await Task.WhenAll(tasks);
    }
}
