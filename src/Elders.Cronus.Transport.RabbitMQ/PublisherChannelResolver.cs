using System;
using System.Collections.Concurrent;
using RabbitMQ.Client;

namespace Elders.Cronus.Transport.RabbitMQ
{
    public class PublisherChannelResolver
    {
        private readonly ConcurrentDictionary<string, IModel> channelPerBoundedContext;
        private readonly ConnectionResolver connectionResolver;
        private static readonly object @lock = new object();

        public PublisherChannelResolver(ConnectionResolver connectionResolver)
        {
            channelPerBoundedContext = new ConcurrentDictionary<string, IModel>();
            this.connectionResolver = connectionResolver;
        }

        public IModel Resolve(string boundedContext, IRabbitMqOptions options)
        {
            IModel channel = GetExistingChannel(boundedContext);

            if (channel is null)
            {
                lock (@lock)
                {
                    channel = GetExistingChannel(boundedContext);

                    if (channel is null)
                    {
                        var connection = connectionResolver.Resolve(boundedContext, options);
                        channel = connection.CreateModel();
                        channel.ConfirmSelect();

                        if (channelPerBoundedContext.TryAdd(boundedContext, channel) == false)
                        {
                            throw new Exception("Kak go napravi tova?");
                        }
                    }
                }
            }

            return channel;
        }

        private IModel GetExistingChannel(string boundedContext)
        {
            channelPerBoundedContext.TryGetValue(boundedContext, out IModel channel);

            return channel;
        }
    }

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

            if (channel is null)
            {
                lock (@lock)
                {
                    channel = GetExistingChannel(queue);

                    if (channel is null)
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
