using System;
using System.Collections.Generic;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace Elders.Cronus.Transport.RabbitMQ
{
    public abstract class ChannelResolver
    {
        protected readonly Dictionary<string, IModel> channels;
        protected readonly ConnectionResolver connectionResolver;
        protected static readonly object @lock = new object();

        public ChannelResolver(ConnectionResolver connectionResolver)
        {
            channels = new Dictionary<string, IModel>();
            this.connectionResolver = connectionResolver;
        }

        public virtual IModel Resolve(string resolveKey, IRabbitMqOptions options, string boundedContext)
        {
            IModel channel = GetExistingChannel(resolveKey);

            if (channel is null || channel.IsClosed)
            {
                lock (@lock)
                {
                    channel = GetExistingChannel(resolveKey);

                    if (channel?.IsClosed == true)
                    {
                        channels.Remove(resolveKey);
                        channel = null;
                    }

                    if (channel is null)
                    {
                        var connection = connectionResolver.Resolve(boundedContext, options);
                        IModel scopedChannel = connection.CreateModel();
                        scopedChannel.ConfirmSelect();

                        channels.Add(resolveKey, scopedChannel);
                    }
                }
            }

            return GetExistingChannel(resolveKey);
        }

        protected IModel GetExistingChannel(string boundedContext)
        {
            channels.TryGetValue(boundedContext, out IModel channel);

            return channel;
        }
    }

    public class PublisherChannelResolver : ChannelResolver // channels per exchange
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
                        IModel scopedChannel = connection.CreateModel();
                        scopedChannel.ConfirmSelect();

                        try
                        {
                            scopedChannel.ExchangeDeclarePassive(exchange);
                        }
                        catch (OperationInterruptedException ex)
                        {
                            scopedChannel.Dispose();
                            scopedChannel = connection.CreateModel();
                            scopedChannel.ExchangeDeclare(exchange, PipelineType.Headers.ToString(), true);
                        }

                        channels.Add(theKey, scopedChannel);
                    }
                }
            }

            return GetExistingChannel(theKey);
        }
    }

    public class ConsumerPerQueueChannelResolver : ChannelResolver // channels per queue
    {
        public ConsumerPerQueueChannelResolver(ConnectionResolver connectionResolver) : base(connectionResolver) { }
    }
}
