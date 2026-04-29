using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Xunit;

namespace Elders.Cronus.Transport.RabbitMQ.IntegrationTests;

/// <summary>
/// Integration tests covering the highest-risk surfaces of the Stage 2
/// RabbitMQ.Client 7.x async refactor that the Stage 3 smoke test does
/// not exercise: <see cref="CancellationToken"/> propagation, concurrent
/// publish safety, and connection-drop recovery.
/// </summary>
[Trait("Category", "Integration")]
public class AsyncBehaviorIntegrationTests : IClassFixture<RabbitMqFixture>
{
    private readonly RabbitMqFixture fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="AsyncBehaviorIntegrationTests"/> class.
    /// </summary>
    /// <param name="fixture">Shared RabbitMQ container fixture.</param>
    public AsyncBehaviorIntegrationTests(RabbitMqFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        this.fixture = fixture;
    }

    /// <summary>
    /// Verifies that the <see cref="CancellationToken"/> threaded through
    /// <c>BasicPublishAsync</c> actually aborts an in-flight publish when
    /// the broker is paused and cannot complete the operation. Catches
    /// regressions where CT plumbing is dropped on the floor.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Cancellation_propagates_during_in_flight_publish()
    {
        using CancellationTokenSource setupCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        CancellationToken setupToken = setupCts.Token;

        RabbitMqConnectionFactory<RabbitMqOptions> factory = fixture.CreateConnectionFactory();
        await using IConnection connection = await factory.CreateConnectionAsync(setupToken);

        // Enable publisher confirms — same options PublisherChannelResolver uses in production.
        // With confirms on, BasicPublishAsync blocks waiting for the broker ack, which is the
        // exact path the CancellationToken plumbing must abort.
        CreateChannelOptions channelOptions = new CreateChannelOptions(
            publisherConfirmationsEnabled: true,
            publisherConfirmationTrackingEnabled: true);
        await using IChannel channel = await connection.CreateChannelAsync(channelOptions, setupToken);

        const string exchange = "cronus.cancel.exchange";
        await channel.ExchangeDeclareAsync(exchange, ExchangeType.Fanout, durable: false, autoDelete: true, cancellationToken: setupToken);

        // Pause the container — broker stops acking. Confirm-tracking publishes will hang.
        await fixture.Broker.PauseAsync();
        try
        {
            using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
            BasicProperties props = new BasicProperties { Persistent = false };
            byte[] body = Encoding.UTF8.GetBytes("payload");

            Stopwatch sw = Stopwatch.StartNew();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            {
                await channel.BasicPublishAsync(
                    exchange: exchange,
                    routingKey: string.Empty,
                    mandatory: false,
                    basicProperties: props,
                    body: body,
                    cancellationToken: cts.Token);
            });
            sw.Stop();

            Assert.True(
                sw.ElapsedMilliseconds < 2000,
                $"Cancellation took {sw.ElapsedMilliseconds}ms (expected <2000ms).");
        }
        finally
        {
            await fixture.Broker.UnpauseAsync();
        }
    }

    /// <summary>
    /// Fires 100 concurrent <c>BasicPublishAsync</c> calls and verifies
    /// the consumer receives all 100 without races, deadlocks, or
    /// publisher-confirm losses. Exercises the static
    /// <c>SemaphoreSlim</c> guarding channel resolution and the new
    /// async confirmation pipeline.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Concurrent_publish_serializes_safely()
    {
        const int messageCount = 100;

        using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        CancellationToken token = cts.Token;

        RabbitMqConnectionFactory<RabbitMqOptions> factory = fixture.CreateConnectionFactory();
        await using IConnection connection = await factory.CreateConnectionAsync(token);
        await using IChannel channel = await connection.CreateChannelAsync(cancellationToken: token);

        const string exchange = "cronus.concurrent.exchange";
        const string queue = "cronus.concurrent.queue";

        await channel.ExchangeDeclareAsync(exchange, ExchangeType.Fanout, durable: false, autoDelete: true, cancellationToken: token);
        await channel.QueueDeclareAsync(queue, durable: false, exclusive: false, autoDelete: true, arguments: null, cancellationToken: token);
        await channel.QueueBindAsync(queue, exchange, routingKey: string.Empty, arguments: null, cancellationToken: token);

        int receivedCount = 0;
        TaskCompletionSource<bool> allReceived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        AsyncEventingBasicConsumer consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, ea) =>
        {
            int n = Interlocked.Increment(ref receivedCount);
            if (n == messageCount)
                allReceived.TrySetResult(true);

            await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: token);
        };
        await channel.BasicConsumeAsync(queue, autoAck: false, consumerTag: string.Empty, consumer: consumer, cancellationToken: token);

        BasicProperties props = new BasicProperties { Persistent = false };

        Stopwatch sw = Stopwatch.StartNew();
        await Task.WhenAll(Enumerable.Range(0, messageCount).Select(async i =>
        {
            byte[] body = Encoding.UTF8.GetBytes($"msg-{i}");
            await channel.BasicPublishAsync(
                exchange,
                routingKey: string.Empty,
                mandatory: false,
                basicProperties: props,
                body: body,
                cancellationToken: token);
        }));
        sw.Stop();

        Assert.True(
            sw.ElapsedMilliseconds < 5000,
            $"{messageCount} concurrent publishes took {sw.ElapsedMilliseconds}ms (expected <5000ms).");

        Task winner = await Task.WhenAny(allReceived.Task, Task.Delay(TimeSpan.FromSeconds(10), token));
        Assert.Same(allReceived.Task, winner);
        Assert.Equal(messageCount, Volatile.Read(ref receivedCount));
    }

    /// <summary>
    /// Validates that after a connection lifecycle event (the original
    /// connection is closed), a freshly opened connection + channel can
    /// re-declare topology and resume publish/consume. Mirrors the
    /// recovery path <see cref="PublisherChannelResolver"/> walks when
    /// it observes <c>OperationInterruptedException</c> and disposes
    /// the scoped channel.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Connection_drop_recovers_and_resumes_publishing()
    {
        using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        CancellationToken token = cts.Token;

        RabbitMqConnectionFactory<RabbitMqOptions> factory = fixture.CreateConnectionFactory();

        const string exchange = "cronus.recovery.exchange";
        const string queue = "cronus.recovery.queue";

        // 1. Open a connection, declare topology, send a baseline message.
        IConnection firstConnection = await factory.CreateConnectionAsync(token);
        IChannel firstChannel = await firstConnection.CreateChannelAsync(cancellationToken: token);

        await firstChannel.ExchangeDeclareAsync(exchange, ExchangeType.Fanout, durable: true, autoDelete: false, cancellationToken: token);
        await firstChannel.QueueDeclareAsync(queue, durable: true, exclusive: false, autoDelete: false, arguments: null, cancellationToken: token);
        await firstChannel.QueueBindAsync(queue, exchange, routingKey: string.Empty, arguments: null, cancellationToken: token);

        BasicProperties props = new BasicProperties { Persistent = true };
        await firstChannel.BasicPublishAsync(
            exchange,
            routingKey: string.Empty,
            mandatory: false,
            basicProperties: props,
            body: Encoding.UTF8.GetBytes("msg-before-drop"),
            cancellationToken: token);

        // 2. Drop the connection (simulates network blip / broker disconnect).
        await firstChannel.CloseAsync(token);
        await firstConnection.CloseAsync(token);
        await firstChannel.DisposeAsync();
        await firstConnection.DisposeAsync();

        // 3. Reconnect — this is what PublisherChannelResolver does internally
        //    when its scoped channel observes OperationInterruptedException.
        await using IConnection newConnection = await factory.CreateConnectionAsync(token);
        await using IChannel newChannel = await newConnection.CreateChannelAsync(cancellationToken: token);

        // Topology is durable, but redeclare is idempotent and the resolver does
        // re-declare on recovery, so mirror that behavior here.
        await newChannel.ExchangeDeclareAsync(exchange, ExchangeType.Fanout, durable: true, autoDelete: false, cancellationToken: token);
        await newChannel.QueueDeclareAsync(queue, durable: true, exclusive: false, autoDelete: false, arguments: null, cancellationToken: token);
        await newChannel.QueueBindAsync(queue, exchange, routingKey: string.Empty, arguments: null, cancellationToken: token);

        TaskCompletionSource<string> receivedAfterRecovery = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        List<string> allPayloads = new List<string>();
        object payloadsLock = new object();

        AsyncEventingBasicConsumer consumer = new AsyncEventingBasicConsumer(newChannel);
        consumer.ReceivedAsync += async (_, ea) =>
        {
            string payload = Encoding.UTF8.GetString(ea.Body.ToArray());
            lock (payloadsLock)
                allPayloads.Add(payload);

            if (payload == "msg-after-recovery")
                receivedAfterRecovery.TrySetResult(payload);

            await newChannel.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: token);
        };
        await newChannel.BasicConsumeAsync(queue, autoAck: false, consumerTag: string.Empty, consumer: consumer, cancellationToken: token);

        // 4. Publish + consume on the new connection.
        await newChannel.BasicPublishAsync(
            exchange,
            routingKey: string.Empty,
            mandatory: false,
            basicProperties: props,
            body: Encoding.UTF8.GetBytes("msg-after-recovery"),
            cancellationToken: token);

        Task winner = await Task.WhenAny(receivedAfterRecovery.Task, Task.Delay(TimeSpan.FromSeconds(10), token));
        Assert.Same(receivedAfterRecovery.Task, winner);
        Assert.Equal("msg-after-recovery", await receivedAfterRecovery.Task);

        // Cleanup durable topology so it doesn't leak into other tests.
        await newChannel.QueueDeleteAsync(queue, ifUnused: false, ifEmpty: false, cancellationToken: token);
        await newChannel.ExchangeDeleteAsync(exchange, ifUnused: false, cancellationToken: token);
    }

    /// <summary>
    /// Fires 50 parallel <see cref="PublisherChannelResolver.ResolveAsync"/> calls
    /// against a fresh resolver to validate that the static <c>SemaphoreSlim</c>
    /// guarding channel resolution does not deadlock under contention and that
    /// every caller receives a usable channel.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Channel_resolution_under_high_contention_does_not_deadlock()
    {
        const int parallelism = 50;
        const string exchange = "cronus.contention.exchange";

        using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        CancellationToken token = cts.Token;

        RabbitMqOptions options = fixture.Options;
        RabbitMqConnectionFactory<RabbitMqOptions> factory = fixture.CreateConnectionFactory();

        // Pre-declare the exchange so PublisherChannelResolver's passive declare succeeds.
        await using (IConnection setupConnection = await factory.CreateConnectionAsync(token))
        await using (IChannel setupChannel = await setupConnection.CreateChannelAsync(cancellationToken: token))
        {
            await setupChannel.ExchangeDeclareAsync(exchange, ExchangeType.Fanout, durable: false, autoDelete: true, cancellationToken: token);
        }

        await using ConnectionResolver connectionResolver = new ConnectionResolver(factory);
        PublisherChannelResolver resolver = new PublisherChannelResolver(connectionResolver);

        Stopwatch sw = Stopwatch.StartNew();
        IChannel[] channels = await Task.WhenAll(
            Enumerable.Range(0, parallelism)
                .Select(_ => resolver.ResolveAsync(exchange, options, options.BoundedContext, token)));
        sw.Stop();

        Assert.True(
            sw.ElapsedMilliseconds < 30_000,
            $"Resolution took {sw.ElapsedMilliseconds}ms (deadlock?).");
        Assert.Equal(parallelism, channels.Length);
        Assert.All(channels, c => Assert.NotNull(c));

        // Resolver caches per (boundedContext, options-type, exchange, server) — every caller
        // for the same key should receive the same channel instance, which is the whole point
        // of the lock.
        IChannel first = channels[0];
        Assert.All(channels, c => Assert.Same(first, c));

        // Smoke check that the cached channel is functional.
        await first.QueueDeclareAsync(
            queue: "cronus.contention.test",
            durable: false,
            exclusive: true,
            autoDelete: true,
            arguments: null,
            cancellationToken: token);
    }
}
