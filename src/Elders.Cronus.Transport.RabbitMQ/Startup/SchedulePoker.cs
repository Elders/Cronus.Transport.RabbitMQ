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

        public async Task PokeAsync(CancellationToken cancellationToken)
        {
            try
            {
                string queueName = $"{GetQueueName(boundedContext.CurrentValue.Name)}.Scheduled";

                while (cancellationToken.IsCancellationRequested == false)
                {
                    IConnection connection = connectionResolver.Resolve(queueName, rmqOptionsMonitor.CurrentValue);

                    using (IModel channel = connection.CreateModel())
                    {
                        try
                        {
                            consumer = new AsyncEventingBasicConsumer(channel);
                            consumer.Received += AsyncListener_Received;

                            string consumerTag = channel.BasicConsume(queue: queueName, autoAck: false, consumer: consumer);

                            await Task.Delay(30000, cancellationToken).ConfigureAwait(false);

                            consumer.Received -= AsyncListener_Received;
                        }
                        catch (Exception)
                        {
                            await Task.Delay(5000).ConfigureAwait(false);
                        }
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

