using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Xunit;

namespace Elders.Cronus.Transport.RabbitMQ.IntegrationTests;

/// <summary>
/// End-to-end smoke test that validates the Stage 2 async refactor of
/// <c>Elders.Cronus.Transport.RabbitMQ</c> against a real RabbitMQ broker
/// provisioned by Testcontainers. Tagged <c>Integration</c> so the Azure
/// Pipelines build (which has no Docker) skips it.
/// </summary>
[Trait("Category", "Integration")]
public class PublishConsumeIntegrationTests : IClassFixture<RabbitMqFixture>
{
    private readonly RabbitMqFixture fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="PublishConsumeIntegrationTests"/> class.
    /// </summary>
    /// <param name="fixture">Shared RabbitMQ container fixture.</param>
    public PublishConsumeIntegrationTests(RabbitMqFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        this.fixture = fixture;
    }

    [Fact]
    public async Task Publish_round_trip_succeeds()
    {
        using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        CancellationToken token = cts.Token;

        RabbitMqConnectionFactory<RabbitMqOptions> factory = fixture.CreateConnectionFactory();

        // Open a connection + channel via the Stage 2 async API.
        await using IConnection connection = await factory.CreateConnectionAsync(token);
        await using IChannel channel = await connection.CreateChannelAsync(cancellationToken: token);

        // Declare topology: a fanout exchange wired to a queue.
        const string exchange = "cronus.smoke.exchange";
        const string queue = "cronus.smoke.queue";

        await channel.ExchangeDeclareAsync(exchange, ExchangeType.Fanout, durable: false, autoDelete: true, cancellationToken: token);
        await channel.QueueDeclareAsync(queue, durable: false, exclusive: false, autoDelete: true, arguments: null, cancellationToken: token);
        await channel.QueueBindAsync(queue, exchange, routingKey: string.Empty, arguments: null, cancellationToken: token);

        // Wire an async consumer that signals via TaskCompletionSource.
        TaskCompletionSource<string> received = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        AsyncEventingBasicConsumer consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, ea) =>
        {
            string payload = Encoding.UTF8.GetString(ea.Body.ToArray());
            received.TrySetResult(payload);
            await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: token).ConfigureAwait(false);
        };

        await channel.BasicConsumeAsync(queue, autoAck: false, consumerTag: string.Empty, consumer: consumer, cancellationToken: token);

        // Publish a message.
        const string expected = "stage2-async-refactor-ok";
        BasicProperties props = new BasicProperties
        {
            Persistent = false,
            Headers = new Dictionary<string, object?> { { "cronus_smoke", "1" } },
        };
        await channel.BasicPublishAsync(exchange, routingKey: string.Empty, mandatory: false, basicProperties: props, body: Encoding.UTF8.GetBytes(expected), cancellationToken: token);

        // Wait for round-trip with timeout.
        Task winner = await Task.WhenAny(received.Task, Task.Delay(Timeout.Infinite, token));
        Assert.Same(received.Task, winner);
        Assert.Equal(expected, await received.Task);
    }
}
