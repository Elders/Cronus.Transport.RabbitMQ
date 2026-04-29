using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using Testcontainers.RabbitMq;
using Xunit;

namespace Elders.Cronus.Transport.RabbitMQ.IntegrationTests;

/// <summary>
/// xUnit class fixture that provisions a single RabbitMQ container shared
/// by every integration test in the collection. Sharing the container
/// across tests avoids paying the ~10s cold-start cost per fact.
/// </summary>
public sealed class RabbitMqFixture : IAsyncLifetime
{
    private RabbitMqContainer? broker;

    /// <summary>
    /// Gets the live RabbitMQ container provisioned by Testcontainers.
    /// </summary>
    public RabbitMqContainer Broker => broker ?? throw new InvalidOperationException("Broker is not initialized.");

    /// <summary>
    /// Gets the <see cref="RabbitMqOptions"/> pointed at the live container.
    /// </summary>
    public RabbitMqOptions Options => new RabbitMqOptions
    {
        Server = Broker.Hostname,
        Port = Broker.GetMappedPublicPort(5672),
        VHost = "/",
        Username = "rabbitmq",
        Password = "rabbitmq",
        UseSsl = false,
    };

    /// <summary>
    /// Builds a <see cref="RabbitMqConnectionFactory{TOptions}"/> wired to the live container.
    /// </summary>
    /// <returns>A connection factory ready to open connections against the test broker.</returns>
    public RabbitMqConnectionFactory<RabbitMqOptions> CreateConnectionFactory()
    {
        IOptionsMonitor<RabbitMqOptions> monitor = new TestOptionsMonitor<RabbitMqOptions>(Options);
        return new RabbitMqConnectionFactory<RabbitMqOptions>(monitor, NullLogger<RabbitMqConnectionFactory<RabbitMqOptions>>.Instance);
    }

    /// <summary>
    /// Boots the shared RabbitMQ container.
    /// </summary>
    public async Task InitializeAsync()
    {
        broker = new RabbitMqBuilder("rabbitmq:3.13-management").Build();
        await broker.StartAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Tears down the shared RabbitMQ container.
    /// </summary>
    public async Task DisposeAsync()
    {
        if (broker is not null)
            await broker.DisposeAsync().ConfigureAwait(false);
    }
}

/// <summary>
/// Minimal in-memory <see cref="IOptionsMonitor{TOptions}"/> implementation that returns the
/// supplied options instance and never raises change notifications.
/// </summary>
/// <typeparam name="TOptions">The options type to expose.</typeparam>
internal sealed class TestOptionsMonitor<TOptions> : IOptionsMonitor<TOptions>
{
    private readonly TOptions value;

    /// <summary>
    /// Initializes a new instance of the <see cref="TestOptionsMonitor{TOptions}"/> class.
    /// </summary>
    /// <param name="value">The options instance to expose to consumers.</param>
    public TestOptionsMonitor(TOptions value)
    {
        ArgumentNullException.ThrowIfNull(value);
        this.value = value;
    }

    /// <inheritdoc />
    public TOptions CurrentValue => value;

    /// <inheritdoc />
    public TOptions Get(string? name) => value;

    /// <inheritdoc />
    public IDisposable OnChange(Action<TOptions, string> listener) => NoopDisposable.Instance;

    private sealed class NoopDisposable : IDisposable
    {
        public static readonly NoopDisposable Instance = new NoopDisposable();
        public void Dispose() { }
    }
}
