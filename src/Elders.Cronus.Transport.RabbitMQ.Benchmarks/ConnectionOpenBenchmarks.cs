using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Elders.Cronus.Transport.RabbitMQ.Benchmarks;

/// <summary>
/// Measures the cost of opening (and immediately closing) a brand-new RabbitMQ
/// connection through <see cref="ConnectionFactory.CreateConnectionAsync"/>.
///
/// Requires a RabbitMQ broker on <c>localhost:5672</c> with the default
/// <c>guest/guest</c> credentials.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 5)]
public class ConnectionOpenBenchmarks
{
    private RabbitMqOptions options = null!;
    private RabbitMqConnectionFactory<RabbitMqOptions> factory = null!;

    [GlobalSetup]
    public void Setup()
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
    }

    /// <summary>
    /// Open + close one connection per iteration.
    /// </summary>
    [Benchmark]
    public async Task OpenAndClose()
    {
        await using IConnection connection = await factory.CreateConnectionAsync();
    }
}
