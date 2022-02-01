using System;
using System.Collections.Concurrent;
using RabbitMQ.Client;

namespace Elders.Cronus.Transport.RabbitMQ
{
    public class ConsumerPerQueueChannelResolver
    {
        private readonly ConcurrentDictionary<string, IModel> channelPerQueue;
        private readonly ConnectionResolver connectionResolver;
        private static readonly object @lock = new object();

        public ConsumerPerQueueChannelResolver(ConnectionResolver connectionResolver)
        {
            channelPerQueue = new ConcurrentDictionary<string, IModel>();
            this.connectionResolver = connectionResolver;
        }

        public IModel Resolve(string queue, IRabbitMqOptions options, string boundedContext)
        {
            IModel channel = GetExistingChannel(queue);

            if (channel is null || channel.IsClosed)
            {
                lock (@lock)
                {
                    channel = GetExistingChannel(queue);

                    if (channel is null || channel.IsClosed)
                    {
                        var connection = connectionResolver.Resolve(boundedContext, options);
                        channel = connection.CreateModel();

                        if (channelPerQueue.TryAdd(queue, channel) == false)
                        {
                            throw new Exception("Kak go napravi tova?");
                        }
                    }
                }
            }

            return channel;
        }

        private IModel GetExistingChannel(string queue)
        {
            channelPerQueue.TryGetValue(queue, out IModel channel);

            return channel;
        }
    }
}
