using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Threading.Tasks;
using System.Threading;

namespace Elders.Cronus.Transport.RabbitMQ.Startup
{
    public class SchedulePoker<T> //where T : IMessageHandler
    {
        private readonly IOptionsMonitor<RabbitMqOptions> rmqOptionsMonitor;
        private readonly ConnectionResolver connectionResolver;
        private readonly IOptionsMonitor<BoundedContext> boundedContext;
        AsyncEventingBasicConsumer consumer;

        public SchedulePoker(IOptionsMonitor<RabbitMqOptions> rmqOptionsMonitor, ConnectionResolver connectionResolver, IOptionsMonitor<BoundedContext> boundedContext)
        {
            this.rmqOptionsMonitor = rmqOptionsMonitor;
            this.connectionResolver = connectionResolver;
            this.boundedContext = boundedContext;
        }

        /// <summary>
        /// Periodically opens a short-lived consumer on the scheduled queue to nudge the broker into delivering due messages.
        /// </summary>
        /// <param name="cancellationToken">A token to stop the periodic poking loop.</param>
        public async Task PokeAsync(CancellationToken cancellationToken)
        {
            try
            {
                string queueName = $"{GetQueueName(boundedContext.CurrentValue.Name)}.Scheduled";

                while (cancellationToken.IsCancellationRequested == false)
                {
                    IConnection connection = await connectionResolver.ResolveAsync(queueName, rmqOptionsMonitor.CurrentValue, cancellationToken).ConfigureAwait(false);

                    await using IChannel channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
                    try
                    {
                        consumer = new AsyncEventingBasicConsumer(channel);
                        consumer.ReceivedAsync += AsyncListener_Received;

                        string consumerTag = await channel.BasicConsumeAsync(queue: queueName, autoAck: false, consumer: consumer, cancellationToken: cancellationToken).ConfigureAwait(false);

                        await Task.Delay(30000, cancellationToken).ConfigureAwait(false);

                        consumer.ReceivedAsync -= AsyncListener_Received;
                    }
                    catch (Exception)
                    {
                        await Task.Delay(5000, cancellationToken).ConfigureAwait(false);
                    }
                }
            }
            catch (Exception) { }
        }

        private Task AsyncListener_Received(object sender, BasicDeliverEventArgs @event)
        {
            return Task.CompletedTask;
        }

        private string GetQueueName(string boundedContext, bool useFanoutMode = false)
        {
            if (useFanoutMode)
            {
                return $"{boundedContext}.{typeof(T).Name}.{Environment.MachineName}";
            }
            else
            {
                string systemMarker = typeof(ISystemHandler).IsAssignableFrom(typeof(T)) ? "cronus." : string.Empty;
                return $"{boundedContext}.{systemMarker}{typeof(T).Name}";
            }
        }
    }
}
