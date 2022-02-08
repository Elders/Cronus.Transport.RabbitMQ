using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace Elders.Cronus.Transport.RabbitMQ
{
    public class PublisherChannelResolver : ChannelResolverBase // channels per exchange
    {
        public PublisherChannelResolver(ConnectionResolver connectionResolver) : base(connectionResolver) { }

        public override IModel Resolve(string exchange, IRabbitMqOptions options, string boundedContext)
        {
            string theKey = $"{boundedContext}_{options.GetType().Name}_{exchange}";

            IModel channel = GetExistingChannel(theKey);

            if (channel is null || channel.IsClosed)
            {
                lock (@lock)
                {
                    channel = GetExistingChannel(theKey);

                    if (channel?.IsClosed == true)
                    {
                        channels.Remove(theKey);
                        channel = null;
                    }

                    if (channel is null)
                    {
                        var connection = connectionResolver.Resolve(boundedContext, options);
                        IModel scopedChannel = CreateModelForPublisher(connection);
                        try
                        {
                            scopedChannel.ExchangeDeclarePassive(exchange);
                        }
                        catch (OperationInterruptedException)
                        {
                            scopedChannel.Dispose();
                            scopedChannel = CreateModelForPublisher(connection);
                            scopedChannel.ExchangeDeclare(exchange, PipelineType.Headers.ToString(), true);
                        }

                        channels.Add(theKey, scopedChannel);
                    }
                }
            }

            return GetExistingChannel(theKey);
        }

        private IModel CreateModelForPublisher(IConnection connection)
        {
            IModel channel = connection.CreateModel();
            channel.ConfirmSelect();

            return channel;
        }
    }
}
