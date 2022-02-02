using System.Collections.Generic;
using RabbitMQ.Client;

namespace Elders.Cronus.Transport.RabbitMQ
{
    public class PublisherChannelResolver
    {
        private readonly Dictionary<string, IModel> channelPerBoundedContext;
        private readonly ConnectionResolver connectionResolver;
        private static readonly object @lock = new object();

        public PublisherChannelResolver(ConnectionResolver connectionResolver)
        {
            channelPerBoundedContext = new Dictionary<string, IModel>();
            this.connectionResolver = connectionResolver;
        }

        public IModel Resolve(string boundedContext, IRabbitMqOptions options)
        {
            IModel channel = GetExistingChannel(boundedContext);

            if (channel is null || channel.IsClosed)
            {
                lock (@lock)
                {
                    channel = GetExistingChannel(boundedContext);

                    if (channel?.IsClosed == true)
                    {
                        channelPerBoundedContext.Remove(boundedContext);
                    }

                    if (channel is null)
                    {
                        var connection = connectionResolver.Resolve(boundedContext, options);
                        IModel scopedChannel = connection.CreateModel();
                        scopedChannel.ConfirmSelect();

                        channelPerBoundedContext.Add(boundedContext, scopedChannel);
                    }
                }
            }

            return GetExistingChannel(boundedContext);
        }

        private IModel GetExistingChannel(string boundedContext)
        {
            channelPerBoundedContext.TryGetValue(boundedContext, out IModel channel);

            return channel;
        }
    }
}
